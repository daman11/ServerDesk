using Microsoft.Data.Sqlite;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class DatabaseBackupVerificationTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-backup-verification-"+Guid.NewGuid().ToString("N"),"żółć # %");
    private static async Task<byte[]> ReadLiveBytes(string path)
    { await using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete); using var bytes=new MemoryStream(); await file.CopyToAsync(bytes); return bytes.ToArray(); }
    private async Task<DatabaseBackupEntry> Snapshot(int actual=3,int? named=null)
    {
        Directory.CreateDirectory(root); var path=Path.Combine(root,$"serverdesk-20261017-120000000-v{named??actual}-manual-{Guid.NewGuid():N}.db");
        using(var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=path,Pooling=false }.ToString()))
        { await db.OpenAsync(); using var command=db.CreateCommand(); command.CommandText=$"CREATE TABLE settings(key TEXT PRIMARY KEY,value TEXT); INSERT INTO settings VALUES('fixture','keep'); PRAGMA user_version={actual}"; await command.ExecuteNonQueryAsync(); }
        return new(path,DateTimeOffset.UtcNow,new FileInfo(path).Length,named??actual,false);
    }
    [Theory] [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task Supported_schema_checks_are_read_only_and_do_not_migrate_or_create_sidecars(int schema)
    {
        var entry=await Snapshot(schema); var before=await File.ReadAllBytesAsync(entry.Path); var files=Directory.GetFiles(root);
        Assert.Equal(schema,await DatabaseBackupCatalog.VerifyAsync(entry)); Assert.Equal(before,await File.ReadAllBytesAsync(entry.Path)); Assert.Equal(files,Directory.GetFiles(root));
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task A_real_backup_including_readonly_files_and_URI_characters_is_checked_without_touching_live_data(bool readOnly)
    {
        var repo=new ProfileRepository(new(root)); await repo.InitializeAsync(); await repo.SetSettingAsync("marker","preserve"); var backup=await repo.CreateDatabaseBackupAsync();
        var entry=Assert.Single((await DatabaseBackupCatalog.ReadAsync(repo.DatabaseBackupDirectory)).Entries); var bytes=await File.ReadAllBytesAsync(entry.Path); var live=await ReadLiveBytes(Path.Combine(root,"serverdesk.db"));
        var attributes=File.GetAttributes(backup.Path); if(readOnly)File.SetAttributes(backup.Path,attributes|FileAttributes.ReadOnly);
        var preparedAttributes=File.GetAttributes(backup.Path);
        try { Assert.Equal(3,await DatabaseBackupCatalog.VerifyAsync(entry)); Assert.Equal(bytes,await File.ReadAllBytesAsync(entry.Path)); Assert.Equal(live,await ReadLiveBytes(Path.Combine(root,"serverdesk.db"))); Assert.Equal(preparedAttributes,File.GetAttributes(backup.Path)); }
        finally { File.SetAttributes(backup.Path,attributes); }
        Assert.Equal("preserve",await repo.SettingAsync("marker")); Assert.Single(Directory.GetFiles(repo.DatabaseBackupDirectory));
    }
    [Theory] [InlineData("text")] [InlineData("empty")] [InlineData("truncated")]
    public async Task Invalid_or_incomplete_SQLite_files_are_preserved(string kind)
    {
        var entry=await Snapshot(); var bytes=kind=="empty"?[]:kind=="text"?"not SQLite"u8.ToArray():(await File.ReadAllBytesAsync(entry.Path))[..128]; await File.WriteAllBytesAsync(entry.Path,bytes); entry=entry with { Bytes=bytes.Length };
        await Assert.ThrowsAsync<InvalidDataException>(()=>DatabaseBackupCatalog.VerifyAsync(entry)); Assert.Equal(bytes,await File.ReadAllBytesAsync(entry.Path)); Assert.Single(Directory.GetFiles(root));
    }
    [Theory] [InlineData(4,4)] [InlineData(3,2)]
    public async Task Newer_or_misnamed_schemas_are_rejected_without_changes(int actual,int named)
    {
        var entry=await Snapshot(actual,named); var bytes=await File.ReadAllBytesAsync(entry.Path); await Assert.ThrowsAsync<InvalidDataException>(()=>DatabaseBackupCatalog.VerifyAsync(entry)); Assert.Equal(bytes,await File.ReadAllBytesAsync(entry.Path)); Assert.Single(Directory.GetFiles(root));
    }
    [Theory] [InlineData("-wal")] [InlineData("-shm")] [InlineData("-journal")]
    public async Task A_snapshot_with_journals_is_not_treated_as_a_standalone_backup(string suffix)
    {
        var entry=await Snapshot(); var bytes=await File.ReadAllBytesAsync(entry.Path); await File.WriteAllTextAsync(entry.Path+suffix,"preserve journal");
        await Assert.ThrowsAsync<InvalidDataException>(()=>DatabaseBackupCatalog.VerifyAsync(entry)); Assert.Equal(bytes,await File.ReadAllBytesAsync(entry.Path)); Assert.Equal("preserve journal",await File.ReadAllTextAsync(entry.Path+suffix));
    }
    [Fact] public async Task Changed_missing_and_cancelled_snapshots_do_not_create_or_replace_files()
    {
        var entry=await Snapshot(); var bytes=await File.ReadAllBytesAsync(entry.Path); await Assert.ThrowsAsync<InvalidDataException>(()=>DatabaseBackupCatalog.VerifyAsync(entry with { Bytes=entry.Bytes+1 }));
        using var cancellation=new CancellationTokenSource(); cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>DatabaseBackupCatalog.VerifyAsync(entry,cancellation.Token)); Assert.Equal(bytes,await File.ReadAllBytesAsync(entry.Path));
        File.Delete(entry.Path); await Assert.ThrowsAsync<FileNotFoundException>(()=>DatabaseBackupCatalog.VerifyAsync(entry)); Assert.Empty(Directory.GetFiles(root));
    }
    [Fact] public async Task Unrelated_filenames_and_metadata_are_rejected_before_opening_SQLite()
    {
        var entry=await Snapshot(); var bytes=await File.ReadAllBytesAsync(entry.Path); await Assert.ThrowsAsync<InvalidDataException>(()=>DatabaseBackupCatalog.VerifyAsync(entry with { SchemaVersion=2 }));
        var unrelated=Path.Combine(root,"serverdesk.db"); File.Move(entry.Path,unrelated); await Assert.ThrowsAsync<InvalidDataException>(()=>DatabaseBackupCatalog.VerifyAsync(entry with { Path=unrelated })); Assert.Equal(bytes,await File.ReadAllBytesAsync(unrelated));
    }
    [Fact] public async Task A_locked_Windows_snapshot_is_not_rewritten()
    {
        if(!OperatingSystem.IsWindows())return; var entry=await Snapshot(); var bytes=await File.ReadAllBytesAsync(entry.Path);
        using(var locked=new FileStream(entry.Path,FileMode.Open,FileAccess.Read,FileShare.None))await Assert.ThrowsAnyAsync<IOException>(()=>DatabaseBackupCatalog.VerifyAsync(entry)); Assert.Equal(bytes,await File.ReadAllBytesAsync(entry.Path));
    }
    [Fact] public async Task Unix_symbolic_links_are_rejected_without_following_the_target()
    {
        if(OperatingSystem.IsWindows())return; var entry=await Snapshot(); var target=entry.Path+".original"; File.Move(entry.Path,target); File.CreateSymbolicLink(entry.Path,target);
        var bytes=await File.ReadAllBytesAsync(target); await Assert.ThrowsAsync<InvalidDataException>(()=>DatabaseBackupCatalog.VerifyAsync(entry)); Assert.Equal(bytes,await File.ReadAllBytesAsync(target));
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); var parent=Path.GetDirectoryName(root)!; if(Directory.Exists(parent))Directory.Delete(parent,true); }
}
