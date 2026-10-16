using Microsoft.Data.Sqlite;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;
public sealed class WorkspacePersistenceTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-workspace-"+Guid.NewGuid());
    private ProfileRepository Repo()=>new(new AppPaths(root));
    private static WorkspaceSnapshot Snapshot(params SavedSession[] entries)=>new(1,WorkspaceMode.Columns,.63,entries.ToList(),entries.FirstOrDefault(e=>e.Dock==SessionDock.Primary)?.Key,entries.FirstOrDefault(e=>e.Dock==SessionDock.Secondary)?.Key,entries.LastOrDefault()?.Key);
    [Fact] public async Task SavesOnlyExistingIdentitiesAndUpdatesLayoutTogether()
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=new ServerProfile { Name="server",Host="secret-host",Username="private-user" }; await repo.SaveAsync(p);
        var first=new SavedSession(Guid.NewGuid(),p.Id,SessionDock.Primary,1); var missing=new SavedSession(Guid.NewGuid(),Guid.NewGuid(),SessionDock.Secondary);
        await repo.SaveWorkspaceSnapshotAsync(Snapshot(first,missing));
        var json=(await Repo().SettingAsync("lastWorkspace"))!; var restored=WorkspaceSnapshot.Parse(json);
        Assert.Equal(first,Assert.Single(restored.Sessions)); Assert.Null(restored.SelectedSecondary); Assert.Null(restored.Focused); Assert.DoesNotContain(p.Host,json);
        Assert.Equal("Columns",await repo.SettingAsync("workspaceMode")); Assert.Equal("0.63",await repo.SettingAsync("splitRatio"));
        await repo.DeleteAsync(p.Id); await repo.SaveWorkspaceSnapshotAsync(Snapshot(first)); Assert.Empty(WorkspaceSnapshot.Parse((await repo.SettingAsync("lastWorkspace"))!).Sessions);
    }
    [Fact] public async Task OptOutSurvivesQueuedWritesAndReenableUsesCurrentSnapshot()
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=new ServerProfile { Name="server",Host="localhost",Username="admin" }; await repo.SaveAsync(p);
        var snapshot=Snapshot(new SavedSession(Guid.NewGuid(),p.Id,SessionDock.Primary)); await repo.SaveWorkspaceSnapshotAsync(snapshot);
        await repo.SetSettingAsync("rememberWorkspace","False"); await repo.SaveWorkspaceSnapshotAsync(snapshot); Assert.Equal("",await repo.SettingAsync("lastWorkspace"));
        await repo.SetSettingAsync("rememberWorkspace","True"); await repo.SaveWorkspaceSnapshotAsync(Snapshot()); Assert.Empty(WorkspaceSnapshot.Parse((await repo.SettingAsync("lastWorkspace"))!).Sessions);
    }
    [Fact] public async Task InvalidSnapshotRollsBackAllSettings()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.SaveWorkspaceSnapshotAsync(Snapshot()); var previous=await repo.SettingAsync("lastWorkspace");
        await Assert.ThrowsAsync<InvalidDataException>(()=>repo.SaveWorkspaceSnapshotAsync(Snapshot() with { Ratio=double.NaN,Mode=WorkspaceMode.Rows }));
        Assert.Equal(previous,await repo.SettingAsync("lastWorkspace")); Assert.Equal("Columns",await repo.SettingAsync("workspaceMode")); Assert.Equal("0.63",await repo.SettingAsync("splitRatio"));
    }
    [Fact] public async Task LimitAppliesAfterFilteringQuickConnectAndKeepsValidSelections()
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=new ServerProfile { Name="server",Host="localhost",Username="admin" }; await repo.SaveAsync(p);
        var entries=Enumerable.Range(0,140).Select(_=>new SavedSession(Guid.NewGuid(),Guid.NewGuid(),SessionDock.Primary)).Concat(Enumerable.Range(0,130).Select(_=>new SavedSession(Guid.NewGuid(),p.Id,SessionDock.Primary))).ToArray();
        await repo.SaveWorkspaceSnapshotAsync(Snapshot(entries)); var saved=WorkspaceSnapshot.Parse((await repo.SettingAsync("lastWorkspace"))!);
        Assert.Equal(128,saved.Sessions.Count); Assert.All(saved.Sessions,s=>Assert.Equal(p.Id,s.ProfileId)); Assert.Null(saved.SelectedPrimary); Assert.Null(saved.Focused);
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
