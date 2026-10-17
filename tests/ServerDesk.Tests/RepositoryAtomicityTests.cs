using System.Text.Json;
using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class RepositoryAtomicityTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-atomicity-"+Guid.NewGuid().ToString("N"));
    private async Task<ProfileRepository> Repository()
    {
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync();
        await repo.AddFolderAsync("Keep"); await repo.SetFolderIconAsync("Keep","shield"); return repo;
    }
    private async Task Execute(string sql)
    {
        using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=Path.Combine(root,"serverdesk.db"),Pooling=false }.ToString());
        await db.OpenAsync(); using var cmd=db.CreateCommand(); cmd.CommandText=sql; await cmd.ExecuteNonQueryAsync();
    }
    private static async Task<string> State(ProfileRepository repo)=>JsonSerializer.Serialize(new
    {
        Profiles=(await repo.ListAsync()).OrderBy(p=>p.Id),
        Folders=(await repo.FoldersAsync()).Order(StringComparer.Ordinal),
        Icons=(await repo.FolderIconsAsync()).OrderBy(p=>p.Key,StringComparer.Ordinal)
    });
    private const string RejectAncestor="CREATE TRIGGER fail_write BEFORE INSERT ON folders WHEN NEW.path='New/Blocked' BEGIN SELECT RAISE(ABORT,'fixture: folder write failed'); END";
    private const string DropFailure="DROP TRIGGER fail_write";

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Saving_a_profile_and_its_ancestors_rolls_back_together_and_can_be_retried(bool updating)
    {
        var repo=await Repository(); var original=new ServerProfile { Name="Original",Host="example.test",Username="fixture",Folder="Keep" };
        if(updating)await repo.SaveAsync(original);
        var before=await State(repo); var changed=original with { Name="Edited",Folder="New/Blocked/Leaf",Color="#112233" };
        await Execute(RejectAncestor);
        var error=await Assert.ThrowsAsync<SqliteException>(()=>repo.SaveAsync(changed)); Assert.Equal(19,error.SqliteErrorCode);
        Assert.Equal(before,await State(repo)); Assert.Equal(before,await State(new(new AppPaths(root))));
        await Execute(DropFailure); await repo.SaveAsync(changed);
        Assert.Equal(changed,Assert.Single(await repo.ListAsync()));
        Assert.Contains("New",await repo.FoldersAsync()); Assert.Contains(changed.Folder,await repo.FoldersAsync());
        Assert.Equal("shield",(await repo.FolderIconsAsync())["Keep"]);
    }

    [Fact] public async Task Creating_a_nested_folder_does_not_leave_ancestors_after_a_write_failure()
    {
        var repo=await Repository(); var before=await State(repo); await Execute(RejectAncestor);
        await Assert.ThrowsAsync<SqliteException>(()=>repo.AddFolderAsync("New/Blocked/Leaf")); Assert.Equal(before,await State(repo));
        await Execute(DropFailure); await repo.AddFolderAsync("New/Blocked/Leaf");
        Assert.Equal(new[]{"Keep","New","New/Blocked","New/Blocked/Leaf"},await repo.FoldersAsync());
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Moving_a_profile_keeps_source_and_folders_intact_when_any_write_fails(bool failProfile)
    {
        var repo=await Repository(); var original=new ServerProfile { Name="Latest edit",Host="example.test",Username="fixture",Folder="Keep",Color="#112233",Description="Keep this edit" };
        await repo.SaveAsync(original); var before=await State(repo);
        await Execute(failProfile?"CREATE TRIGGER fail_write BEFORE UPDATE ON profiles BEGIN SELECT RAISE(ABORT,'fixture: profile write failed'); END":RejectAncestor);
        await Assert.ThrowsAsync<SqliteException>(()=>repo.MoveToFolderAsync(original.Id,"New/Blocked/Leaf"));
        Assert.Equal(before,await State(repo));
        await Execute(DropFailure); await repo.MoveToFolderAsync(original.Id,"New/Blocked/Leaf");
        Assert.Equal(original with { Folder="New/Blocked/Leaf" },Assert.Single(await repo.ListAsync()));
        Assert.Contains("New/Blocked",await repo.FoldersAsync()); Assert.Equal("shield",(await repo.FolderIconsAsync())["Keep"]);
        await repo.MoveToFolderAsync(original.Id,""); Assert.Equal(original with { Folder="" },Assert.Single(await repo.ListAsync()));
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Renaming_a_subtree_rolls_back_icons_profiles_and_ancestors_and_can_be_retried(bool failProfile)
    {
        var repo=await Repository(); var first=new ServerProfile { Name="One",Host="one.example.test",Username="fixture",Folder="Source/Child" };
        var second=first with { Id=Guid.NewGuid(),Name="Two",Folder="Source/Child/Leaf" };
        await repo.SaveAsync(first); await repo.SaveAsync(second);
        await repo.SetFolderIconAsync("Source","cloud"); await repo.SetFolderIconAsync("Source/Child","database");
        var before=await State(repo);
        await Execute(failProfile?"CREATE TRIGGER fail_write BEFORE UPDATE ON profiles WHEN NEW.id='"+second.Id+"' BEGIN SELECT RAISE(ABORT,'fixture: profile write failed'); END":RejectAncestor);
        await Assert.ThrowsAsync<SqliteException>(()=>repo.RenameFolderAsync("Source","New/Blocked/Renamed")); Assert.Equal(before,await State(repo));
        await Execute(DropFailure); await repo.RenameFolderAsync("Source","New/Blocked/Renamed");
        var profiles=await repo.ListAsync();
        Assert.Contains(first with { Folder="New/Blocked/Renamed/Child" },profiles); Assert.Contains(second with { Folder="New/Blocked/Renamed/Child/Leaf" },profiles);
        var icons=await repo.FolderIconsAsync(); Assert.Equal("cloud",icons["New/Blocked/Renamed"]); Assert.Equal("database",icons["New/Blocked/Renamed/Child"]); Assert.Equal("shield",icons["Keep"]);
        Assert.DoesNotContain((await repo.FoldersAsync()),f=>f=="Source" || f.StartsWith("Source/",StringComparison.Ordinal));
        Assert.Contains("New",await repo.FoldersAsync()); Assert.Contains("New/Blocked",await repo.FoldersAsync());
    }

    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
