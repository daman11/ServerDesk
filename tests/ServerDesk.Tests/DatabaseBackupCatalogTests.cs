using System.Globalization;
using System.Security.Cryptography;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class DatabaseBackupCatalogTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-backup-catalog-"+Guid.NewGuid().ToString("N"));
    [Fact] public async Task Catalog_is_bounded_sorted_by_snapshot_time_and_excludes_incomplete_or_unrelated_files()
    {
        Directory.CreateDirectory(root); var start=new DateTimeOffset(2026,10,1,0,0,0,TimeSpan.Zero);
        for(var i=0;i<25;i++)
        {
            var name=$"serverdesk-{start.AddMinutes(i).ToString("yyyyMMdd-HHmmssfff",CultureInfo.InvariantCulture)}-v3-manual-{Guid.NewGuid():N}.db";
            File.WriteAllText(Path.Combine(root,name),"metadata fixture, not a verified database");
        }
        File.WriteAllText(Path.Combine(root,"serverdesk.db"),"live source");
        File.WriteAllText(Path.Combine(root,"serverdesk-incomplete.db.part"),"unfinished");
        File.WriteAllText(Path.Combine(root,"serverdesk-invalid.db"),"unrelated");
        Directory.CreateDirectory(Path.Combine(root,"nested")); File.WriteAllText(Path.Combine(root,"nested","serverdesk-hidden.db"),"nested");
        var result=await DatabaseBackupCatalog.ReadAsync(root);
        Assert.True(result.DirectoryExists); Assert.Equal(25,result.TotalCount); Assert.Equal(DatabaseBackupCatalog.VisibleLimit,result.Entries.Count);
        Assert.Equal(start.AddMinutes(24),result.Entries[0].CreatedUtc); Assert.Equal(start.AddMinutes(5),result.Entries[^1].CreatedUtc);
        Assert.All(result.Entries,e=> { Assert.True(e.Bytes>0); Assert.Equal(3,e.SchemaVersion); Assert.False(e.BeforeMigration); });
        Assert.Equal("live source",File.ReadAllText(Path.Combine(root,"serverdesk.db")));
    }
    [Fact] public async Task Missing_cancelled_and_occupied_directories_are_distinguished_without_creating_or_replacing_data()
    {
        var result=await DatabaseBackupCatalog.ReadAsync(root); Assert.False(result.DirectoryExists); Assert.Empty(result.Entries); Assert.False(Directory.Exists(root));
        using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>DatabaseBackupCatalog.ReadAsync(root,cancelled.Token));
        Directory.CreateDirectory(root); var occupied=Path.Combine(root,"occupied"); File.WriteAllText(occupied,"preserve");
        await Assert.ThrowsAsync<IOException>(()=>DatabaseBackupCatalog.ReadAsync(occupied)); Assert.Equal("preserve",File.ReadAllText(occupied));
    }
    [Fact] public async Task Real_manual_backups_are_listed_without_modifying_source_or_snapshot_bytes()
    {
        var repo=new ProfileRepository(new(root)); await repo.InitializeAsync(); await repo.SetSettingAsync("marker","keep");
        var backup=await repo.CreateDatabaseBackupAsync(); var digest=SHA256.HashData(await File.ReadAllBytesAsync(backup.Path));
        var entry=Assert.Single((await DatabaseBackupCatalog.ReadAsync(repo.DatabaseBackupDirectory)).Entries);
        Assert.Equal(backup.Path,entry.Path); Assert.Equal(backup.Bytes,entry.Bytes); Assert.Equal(backup.SchemaVersion,entry.SchemaVersion);
        Assert.Equal(digest,SHA256.HashData(await File.ReadAllBytesAsync(backup.Path))); Assert.Equal("keep",await repo.SettingAsync("marker"));
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
