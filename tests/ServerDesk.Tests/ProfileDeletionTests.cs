using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class ProfileDeletionTests:IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-profile-delete-"+Guid.NewGuid().ToString("N"),"Profile ąę ze spacjami");
    private async Task<string?> Json(ProfileRepository repository,Guid id)
    {
        using var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=repository.DatabasePath,Pooling=false }.ToString()); await db.OpenAsync();
        using var query=db.CreateCommand(); query.CommandText="SELECT json FROM profiles WHERE id=$id"; query.Parameters.AddWithValue("$id",id.ToString()); return await query.ExecuteScalarAsync() as string;
    }
    [Theory][InlineData(Protocol.SSH)][InlineData(Protocol.RDP)]
    public async Task Deletion_is_durable_and_scoped_even_with_missing_optional_directories_and_a_live_snapshot(Protocol protocol)
    {
        var paths=new AppPaths(root); var repo=new ProfileRepository(paths); await repo.InitializeAsync();
        var target=new ServerProfile { Name="Usuwany",Host="192.0.2.10",Username="fixture",Protocol=protocol,Description="Notes for target",PrivateKeyPath=Path.Combine(root,"missing-keys","id_ed25519"),Folder="Test/Keep folder" };
        var other=target with { Id=Guid.NewGuid(),Name="Keep",Host="192.0.2.11",Description="Keep these notes",Favorite=true };
        await repo.SaveAsync(target); await repo.SaveAsync(other); await repo.ConnectedAsync(target); await repo.ConnectedAsync(other);
        await repo.SetFolderIconAsync("Test","database"); await repo.SetSettingAsync("custom.setting","keep settings");
        var otherJson=await Json(repo,other.Id); var history=await repo.HistoryAsync(); var folders=await repo.FoldersAsync(); var icons=await repo.FolderIconsAsync();
        var unrelated=Path.Combine(root,"user-file.txt"); await File.WriteAllTextAsync(unrelated,"keep this file");
        var first=new SavedSession(Guid.NewGuid(),target.Id,SessionDock.Primary); var second=first with { Key=Guid.NewGuid(),ProfileId=other.Id };
        var snapshot=new WorkspaceSnapshot(1,WorkspaceMode.Single,.5,[first,second],first.Key,null,first.Key);
        await repo.SaveWorkspaceSnapshotAsync(snapshot);
        await repo.DeleteAsync(target.Id); await repo.DeleteAsync(target.Id);
        // Existing sessions may still record history and save a stale workspace.
        await repo.ConnectedAsync(target); await repo.SaveWorkspaceSnapshotAsync(snapshot);
        var reopened=new ProfileRepository(new AppPaths(root)); await reopened.InitializeAsync();
        Assert.Equal(other.Id,Assert.Single(await reopened.ListAsync()).Id); Assert.Null(await Json(reopened,target.Id)); Assert.Equal(otherJson,await Json(reopened,other.Id));
        Assert.Equal(folders,await reopened.FoldersAsync()); Assert.Equal(icons,await reopened.FolderIconsAsync()); Assert.Equal("keep settings",await reopened.SettingAsync("custom.setting")); Assert.Equal("keep this file",await File.ReadAllTextAsync(unrelated));
        var remaining=WorkspaceSnapshot.Parse((await reopened.SettingAsync("lastWorkspace"))!); Assert.Equal(other.Id,Assert.Single(remaining.Sessions).ProfileId);
        var after=await reopened.HistoryAsync(); Assert.Equal(history.Count+1,after.Count); Assert.All(after.Where(h=>h.ProfileId==target.Id),h=> { Assert.Null(h.Profile); Assert.False(h.CanReconnect); Assert.NotNull(h.Snapshot); });
        foreach(var optional in new[]{"credentials","notes","history","sessions"})Assert.False(Directory.Exists(Path.Combine(root,optional)));
    }
    [Fact] public async Task Database_failure_does_not_report_or_apply_a_successful_delete()
    {
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); var target=new ServerProfile { Name="Keep",Host="192.0.2.1",Username="fixture" }; await repo.SaveAsync(target);
        using(var db=new SqliteConnection(new SqliteConnectionStringBuilder { DataSource=repo.DatabasePath,Pooling=false }.ToString()))
        {
            await db.OpenAsync(); using var cmd=db.CreateCommand(); cmd.CommandText="CREATE TRIGGER reject_profile_delete BEFORE DELETE ON profiles BEGIN SELECT RAISE(ABORT,'Fixture database failure'); END;"; await cmd.ExecuteNonQueryAsync();
        }
        await Assert.ThrowsAsync<SqliteException>(()=>repo.DeleteAsync(target.Id)); Assert.Equal(target,Assert.Single(await new ProfileRepository(new AppPaths(root)).ListAsync()));
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
