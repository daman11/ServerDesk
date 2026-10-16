using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class DatabaseBackupTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-backups-"+Guid.NewGuid());
    private string DatabasePath=>Path.Combine(root,"serverdesk.db");
    private ProfileRepository Repo()=>new(new AppPaths(root));
    private SqliteConnection Connect(string? path=null)=>new(new SqliteConnectionStringBuilder { DataSource=path??DatabasePath,Pooling=false }.ToString());
    private async Task Execute(string sql,string? path=null)
    { using var db=Connect(path); await db.OpenAsync(); using var cmd=db.CreateCommand(); cmd.CommandText=sql; await cmd.ExecuteNonQueryAsync(); }
    private async Task<object?> Scalar(string sql,string? path=null)
    { using var db=Connect(path); await db.OpenAsync(); using var cmd=db.CreateCommand(); cmd.CommandText=sql; return await cmd.ExecuteScalarAsync(); }
    private async Task Legacy(int version=2)
    {
        _=new AppPaths(root);
        await Execute($"CREATE TABLE history(id INTEGER PRIMARY KEY,profile TEXT NOT NULL,time TEXT NOT NULL); INSERT INTO history VALUES(7,'legacy','2026-01-01'); CREATE TABLE settings(key TEXT PRIMARY KEY,value TEXT NOT NULL); INSERT INTO settings VALUES('theme','light'); PRAGMA user_version={version}");
    }

    [Fact] public async Task Fresh_and_current_databases_do_not_create_automatic_backups()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.InitializeAsync(); await Repo().InitializeAsync();
        Assert.Equal(3L,await Scalar("PRAGMA user_version")); Assert.False(Directory.Exists(repo.DatabaseBackupDirectory));
    }

    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task Migration_keeps_a_valid_pre_migration_snapshot_and_is_idempotent(int version)
    {
        await Legacy(version); var repo=Repo(); await repo.InitializeAsync(); await repo.InitializeAsync();
        var backup=Assert.Single(Directory.GetFiles(repo.DatabaseBackupDirectory,"*.db"));
        Assert.Contains("before-migration",Path.GetFileName(backup));
        Assert.Equal((long)version,await Scalar("PRAGMA user_version",backup)); Assert.Equal("ok",await Scalar("PRAGMA quick_check(1)",backup));
        Assert.Equal(0L,await Scalar("SELECT count(*) FROM pragma_table_info('history') WHERE name='snapshot'",backup));
        Assert.Equal(1L,await Scalar("SELECT count(*) FROM history WHERE id=7",backup));
        Assert.Equal(3L,await Scalar("PRAGMA user_version")); Assert.Equal("light",await repo.SettingAsync("theme"));
        Assert.Equal(1L,await Scalar("SELECT count(*) FROM history WHERE id=7"));
        Assert.Empty(Directory.GetFiles(repo.DatabaseBackupDirectory,"*.part*"));
    }

    [Fact] public async Task Backup_failure_prevents_migration_and_preserves_existing_files()
    {
        await Legacy(); var repo=Repo(); await File.WriteAllTextAsync(repo.DatabaseBackupDirectory,"keep this file");
        await Assert.ThrowsAsync<DatabaseBackupException>(()=>repo.InitializeAsync());
        Assert.Equal("keep this file",await File.ReadAllTextAsync(repo.DatabaseBackupDirectory));
        Assert.Equal(2L,await Scalar("PRAGMA user_version"));
        Assert.Equal(0L,await Scalar("SELECT count(*) FROM pragma_table_info('history') WHERE name='snapshot'"));
        Assert.Equal(1L,await Scalar("SELECT count(*) FROM history WHERE id=7"));
    }

    [Fact] public async Task Manual_snapshot_includes_committed_WAL_without_checkpointing_the_source()
    {
        var repo=Repo(); await repo.InitializeAsync();
        using var writer=Connect(); await writer.OpenAsync(); using var cmd=writer.CreateCommand();
        cmd.CommandText="PRAGMA journal_mode=WAL; PRAGMA wal_autocheckpoint=0; INSERT INTO settings VALUES('wal-marker','committed');"; await cmd.ExecuteNonQueryAsync();
        Assert.True(new FileInfo(DatabasePath+"-wal").Length>0);
        var backup=await repo.CreateDatabaseBackupAsync();
        Assert.True(backup.Bytes>0); Assert.Equal(3,backup.SchemaVersion); Assert.Equal("ok",await Scalar("PRAGMA quick_check(1)",backup.Path));
        Assert.Equal("committed",await Scalar("SELECT value FROM settings WHERE key='wal-marker'",backup.Path));
        Assert.Equal("committed",await repo.SettingAsync("wal-marker"));
        Assert.False(File.Exists(backup.Path+"-wal"));
    }

    [Fact] public async Task Concurrent_manual_snapshots_are_distinct_and_never_replace_previous_copies()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.SetSettingAsync("theme","light");
        var first=await repo.CreateDatabaseBackupAsync(); var original=await File.ReadAllBytesAsync(first.Path);
        await repo.SetSettingAsync("theme","dark");
        var copies=await Task.WhenAll(Enumerable.Range(0,4).Select(_=>repo.CreateDatabaseBackupAsync()));
        Assert.Equal(4,copies.Select(c=>c.Path).Distinct().Count()); Assert.Equal(original,await File.ReadAllBytesAsync(first.Path));
        Assert.Equal("light",await Scalar("SELECT value FROM settings WHERE key='theme'",first.Path));
        foreach(var copy in copies)Assert.Equal("dark",await Scalar("SELECT value FROM settings WHERE key='theme'",copy.Path));
        Assert.Empty(Directory.GetFiles(repo.DatabaseBackupDirectory,"*.part*"));
    }

    [Fact] public async Task Cancelled_operations_do_not_migrate_or_publish_a_backup()
    {
        await Legacy(); var repo=Repo(); using var cancellation=new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>repo.InitializeAsync(cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>repo.CreateDatabaseBackupAsync(cancellation.Token));
        Assert.Equal(2L,await Scalar("PRAGMA user_version")); Assert.False(Directory.Exists(repo.DatabaseBackupDirectory));
    }

    [Fact] public async Task A_newer_schema_is_rejected_without_creating_a_backup_or_downgrading()
    {
        await Legacy(4); var repo=Repo();
        await Assert.ThrowsAsync<InvalidDataException>(()=>repo.InitializeAsync());
        await Assert.ThrowsAsync<InvalidDataException>(()=>repo.CreateDatabaseBackupAsync());
        Assert.Equal(4L,await Scalar("PRAGMA user_version")); Assert.False(Directory.Exists(repo.DatabaseBackupDirectory));
    }

    [Fact] public async Task Corrupt_source_is_not_replaced_by_a_new_database()
    {
        var repo=Repo(); var corrupt=System.Text.Encoding.UTF8.GetBytes("Not a SQLite database: preserve this original"); await File.WriteAllBytesAsync(DatabasePath,corrupt);
        await Assert.ThrowsAsync<SqliteException>(()=>repo.InitializeAsync());
        await Assert.ThrowsAnyAsync<Exception>(()=>repo.CreateDatabaseBackupAsync());
        DatabaseTestCleanup.Clear(root); Assert.Equal(corrupt,await File.ReadAllBytesAsync(DatabasePath));
        Assert.Empty(Directory.Exists(repo.DatabaseBackupDirectory)?Directory.GetFiles(repo.DatabaseBackupDirectory):[]);
    }

    [Fact] public async Task Missing_source_is_not_created_by_a_manual_backup()
    {
        var repo=Repo(); await Assert.ThrowsAsync<SqliteException>(()=>repo.CreateDatabaseBackupAsync());
        Assert.False(File.Exists(DatabasePath)); Assert.False(Directory.Exists(repo.DatabaseBackupDirectory));
    }

    [Fact] public async Task Snapshot_can_be_restored_to_a_separate_closed_data_directory()
    {
        var repo=Repo(); await repo.InitializeAsync();
        var profile=new ServerProfile { Name="Żółty serwer",Host="example.test",Username="admin",Folder="Lab/Linux" }; await repo.SaveAsync(profile); await repo.SetSettingAsync("theme","light");
        var backup=await repo.CreateDatabaseBackupAsync();
        var restoredRoot=Path.Combine(root,"restored"); var restored=new ProfileRepository(new AppPaths(restoredRoot));
        File.Copy(backup.Path,Path.Combine(restoredRoot,"serverdesk.db"));
        try
        {
            await restored.InitializeAsync(); Assert.Equal(profile,Assert.Single(await restored.ListAsync()));
            Assert.Equal("light",await restored.SettingAsync("theme")); Assert.Contains("Lab/Linux",await restored.FoldersAsync());
        }
        finally { DatabaseTestCleanup.Clear(restoredRoot); }
    }

    [Fact] public async Task Manual_failure_keeps_the_live_database_usable()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.SetSettingAsync("theme","dark");
        await File.WriteAllTextAsync(repo.DatabaseBackupDirectory,"occupied");
        await Assert.ThrowsAsync<DatabaseBackupException>(()=>repo.CreateDatabaseBackupAsync());
        Assert.Equal("dark",await repo.SettingAsync("theme")); Assert.Equal(3L,await Scalar("PRAGMA user_version"));
    }

    public void Dispose()
    { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
