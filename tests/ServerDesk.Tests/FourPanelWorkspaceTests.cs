using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class FourPanelWorkspaceTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-grid-"+Guid.NewGuid());
    [Fact] public void All_four_groups_keep_independent_selection_and_order_when_reordered_or_closed()
    {
        var state=new WorkspaceState(); var ids=Enumerable.Range(0,8).Select(_=>Guid.NewGuid()).ToArray();
        for(var i=0;i<ids.Length;i++)state.Add(ids[i],WorkspaceState.DockedGroups[i/2]);
        Assert.Equal(WorkspaceMode.Grid,state.Mode);
        foreach(var dock in WorkspaceState.DockedGroups)Assert.Equal(state.Items(dock)[1],state.Selected(dock));
        state.Move(ids[7],SessionDock.Quaternary,ids[6]); Assert.Equal(new[]{ids[7],ids[6]},state.Quaternary); Assert.Equal(ids[7],state.SelectedQuaternary);
        state.Remove(ids[7]); Assert.Equal(ids[6],state.SelectedQuaternary); Assert.Equal(ids[5],state.SelectedTertiary);
        Assert.Equal(ids.Take(7).Order(),state.All.Order());
    }
    [Fact] public void Shrinking_grid_merges_only_hidden_groups_and_preserves_focus_and_floating_tabs()
    {
        var state=new WorkspaceState(); var ids=Enumerable.Range(0,5).Select(_=>Guid.NewGuid()).ToArray();
        for(var i=0;i<4;i++)state.Add(ids[i],WorkspaceState.DockedGroups[i]); state.Add(ids[4],SessionDock.Floating); state.Select(ids[3]);
        state.SetMode(WorkspaceMode.Three); Assert.Equal(ids[3],state.Focused); Assert.Equal(ids[3],state.SelectedPrimary); Assert.Equal(ids[2],Assert.Single(state.Tertiary)); Assert.Empty(state.Quaternary);
        state.SetMode(WorkspaceMode.Rows); Assert.Empty(state.Tertiary); Assert.Equal(ids[1],state.SelectedSecondary); Assert.Equal(ids[4],Assert.Single(state.Floating));
        state.Select(ids[1]); state.SetMode(WorkspaceMode.Single); Assert.Equal(ids[1],state.SelectedPrimary); Assert.Equal(4,state.Primary.Count); Assert.Equal(5,state.All.Distinct().Count());
    }
    [Fact] public void Invalid_drops_and_modes_leave_every_tab_and_selection_intact()
    {
        var state=new WorkspaceState(); var id=Guid.NewGuid(); state.Add(id,SessionDock.Quaternary);
        Assert.Throws<ArgumentOutOfRangeException>(()=>state.Move(id,(SessionDock)99)); Assert.Throws<InvalidOperationException>(()=>state.Move(id,SessionDock.Tertiary,Guid.NewGuid()));
        Assert.Throws<ArgumentOutOfRangeException>(()=>state.SetMode((WorkspaceMode)99)); Assert.Equal(WorkspaceMode.Grid,state.Mode); Assert.Equal(id,state.SelectedQuaternary); Assert.Equal(id,Assert.Single(state.All));
    }
    [Fact] public void Repeated_four_group_moves_and_all_layouts_keep_identity_and_valid_visible_selections()
    {
        var state=new WorkspaceState(); var ids=Enumerable.Range(0,20).Select(_=>Guid.NewGuid()).ToArray(); foreach(var id in ids)state.Add(id);
        var docks=WorkspaceState.DockedGroups.Append(SessionDock.Floating).ToArray();
        for(var i=0;i<500;i++)
        {
            state.Move(ids[i%ids.Length],docks[i%5]); state.SetMode((WorkspaceMode)((i/5)%5));
            Assert.Equal(ids.Order(),state.All.Order()); Assert.Equal(ids.Length,state.All.Distinct().Count());
            foreach(var dock in WorkspaceState.DockedGroups)
            { Assert.True(state.Selected(dock)==null || state.Items(dock).Contains(state.Selected(dock)!.Value)); if(!state.VisibleDocks.Contains(dock))Assert.Empty(state.Items(dock)); }
        }
    }
    [Fact] public void Legacy_snapshot_keeps_floating_value_and_defaults_the_new_axis_and_selections()
    {
        var key=Guid.NewGuid(); var profile=Guid.NewGuid();
        var old=WorkspaceSnapshot.Parse($"{{\"Version\":1,\"Mode\":1,\"Ratio\":0.6,\"Sessions\":[{{\"Key\":\"{key}\",\"ProfileId\":\"{profile}\",\"Dock\":2}}],\"Focused\":\"{key}\"}}");
        Assert.Equal(SessionDock.Floating,Assert.Single(old.Sessions).Dock); Assert.Equal(.5,old.RowRatio); Assert.Null(old.SelectedTertiary); Assert.Null(old.SelectedQuaternary);
    }
    [Fact] public async Task Four_group_snapshot_and_new_preferences_roundtrip_atomically()
    {
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); var profile=new ServerProfile { Name="grid",Host="fixture.invalid",Username="fixture" }; await repo.SaveAsync(profile);
        var entries=WorkspaceState.DockedGroups.Select(d=>new SavedSession(Guid.NewGuid(),profile.Id,d)).ToList();
        var snapshot=new WorkspaceSnapshot(1,WorkspaceMode.Grid,.62,entries,entries[0].Key,entries[1].Key,entries[3].Key,entries[2].Key,entries[3].Key,.37);
        await repo.SaveWorkspaceSnapshotAsync(snapshot); var saved=WorkspaceSnapshot.Parse((await repo.SettingAsync("lastWorkspace"))!); Assert.Equal(snapshot.Serialize(),saved.Serialize()); Assert.Equal("0.37",await repo.SettingAsync("rowSplitRatio"));
        var settings=(await repo.ApplicationSettingsAsync()) with { Preferences=new() { WorkspaceOnStartup=WorkspaceStartup.Grid,NewTabTarget=NewTabPlacement.LeastPopulated,TabStripHeight=48,PaneSeparatorSize=12,ShowPanelNumbers=false } };
        await repo.SaveApplicationSettingsAsync(settings); Assert.Equal(settings,await new ProfileRepository(new AppPaths(root)).ApplicationSettingsAsync());
        await Assert.ThrowsAsync<ArgumentException>(()=>repo.SaveApplicationSettingsAsync(settings with { Theme="light",Preferences=settings.Preferences with { PaneSeparatorSize=13 } })); Assert.Equal(settings,await repo.ApplicationSettingsAsync());
        await Assert.ThrowsAsync<InvalidDataException>(()=>repo.SaveWorkspaceSnapshotAsync(snapshot with { RowRatio=double.NaN })); Assert.Equal(snapshot.Serialize(),await repo.SettingAsync("lastWorkspace"));
    }
    [Fact] public void Invalid_hidden_groups_and_dangling_new_selections_are_rejected_or_filtered()
    {
        var profile=Guid.NewGuid(); var fourth=new SavedSession(Guid.NewGuid(),profile,SessionDock.Quaternary); var snapshot=new WorkspaceSnapshot(1,WorkspaceMode.Grid,.5,[fourth],Focused:fourth.Key,SelectedQuaternary:fourth.Key);
        Assert.Throws<InvalidDataException>(()=>(snapshot with { Mode=WorkspaceMode.Three }).Validate()); Assert.Throws<InvalidDataException>(()=>(snapshot with { SelectedTertiary=fourth.Key }).Validate());
        var filtered=snapshot.AvailableProfiles([]); Assert.Null(filtered.SelectedQuaternary); Assert.Null(filtered.Focused); Assert.Empty(filtered.Sessions);
    }
    [Theory][InlineData(-1,.2)][InlineData(1.1,.8)][InlineData(double.NaN,.5)][InlineData(double.PositiveInfinity,.5)][InlineData(.34,.34)]
    public void Row_axis_is_bounded_independently(double value,double expected) { var state=new WorkspaceState(); state.SetRatio(.63); state.SetRowRatio(value); Assert.Equal(expected,state.RowRatio); Assert.Equal(.63,state.Ratio); }
    [Fact] public void Legacy_preferences_and_validation_keep_previous_workspace_behavior()
    {
        var legacy=JsonSerializer.Deserialize<ApplicationPreferences>("{\"InterfaceFontSize\":16}")!; Assert.Equal(WorkspaceStartup.Remember,legacy.WorkspaceOnStartup); Assert.Equal(NewTabPlacement.FocusedPanel,legacy.NewTabTarget); Assert.Equal(35,legacy.TabStripHeight); Assert.Equal(4,legacy.PaneSeparatorSize); Assert.True(legacy.ShowPanelNumbers);
        foreach(var invalid in new[]{legacy with { WorkspaceOnStartup=(WorkspaceStartup)99 },legacy with { NewTabTarget=(NewTabPlacement)99 },legacy with { TabStripHeight=29 },legacy with { TabStripHeight=49 },legacy with { PaneSeparatorSize=3 }})Assert.Throws<ArgumentException>(invalid.Validate);
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
