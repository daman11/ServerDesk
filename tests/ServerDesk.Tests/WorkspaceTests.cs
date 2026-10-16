using ServerDesk.Core;
using Xunit;

namespace ServerDesk.Tests;
public sealed class WorkspaceTests
{
    [Fact]
    public void Docking_splitting_and_merging_preserve_session_identities_and_focus()
    {
        var state=new WorkspaceState(); var a=Guid.NewGuid(); var b=Guid.NewGuid(); var c=Guid.NewGuid(); state.Add(a); state.Add(b); state.Add(c);
        state.Move(b,SessionDock.Secondary); Assert.Equal(WorkspaceMode.Columns,state.Mode); Assert.Equal(b,state.SelectedSecondary); Assert.Equal(c,state.SelectedPrimary);
        state.Move(c,SessionDock.Floating); Assert.Equal(c,state.Focused); Assert.Equal(a,state.SelectedPrimary);
        state.Select(b); state.SetMode(WorkspaceMode.Single); Assert.Equal(b,state.SelectedPrimary); Assert.Empty(state.Secondary); Assert.Equal(new[]{a,b},state.Primary); Assert.Equal(c,Assert.Single(state.Floating));
        state.Move(c,SessionDock.Primary,a); Assert.Equal(new[]{c,a,b},state.Primary); Assert.Empty(state.Floating); Assert.Equal(c,state.SelectedPrimary);
    }
    [Fact]
    public void Removing_selected_session_selects_a_neighbour_and_preserves_other_panel()
    {
        var state=new WorkspaceState(); var a=Guid.NewGuid(); var b=Guid.NewGuid(); var c=Guid.NewGuid(); state.Add(a); state.Add(b); state.Add(c,SessionDock.Secondary);
        state.Remove(b); Assert.Equal(a,state.SelectedPrimary); Assert.Equal(c,state.SelectedSecondary); Assert.Equal(c,state.Focused);
        state.Remove(c); Assert.Equal(a,state.Focused); Assert.Empty(state.Secondary);
    }
    [Fact]
    public void Invalid_drop_target_and_duplicate_add_cannot_lose_a_session()
    {
        var state=new WorkspaceState(); var id=Guid.NewGuid(); state.Add(id);
        Assert.Throws<InvalidOperationException>(()=>state.Add(id)); Assert.Throws<ArgumentOutOfRangeException>(()=>state.Move(id,(SessionDock)99));
        Assert.Throws<InvalidOperationException>(()=>state.Move(id,SessionDock.Secondary,Guid.NewGuid())); Assert.Equal(id,Assert.Single(state.Primary));
    }
    [Fact]
    public void Repeated_layout_changes_keep_every_session_in_exactly_one_location()
    {
        var state=new WorkspaceState(); var ids=Enumerable.Range(0,8).Select(_=>Guid.NewGuid()).ToArray(); foreach(var id in ids)state.Add(id);
        for(var i=0;i<120;i++)
        {
            state.Move(ids[i%ids.Length],(SessionDock)(i%3)); state.SetMode((WorkspaceMode)((i/3)%3));
            var all=state.Primary.Concat(state.Secondary).Concat(state.Floating).ToArray(); Assert.Equal(ids.Length,all.Length); Assert.Equal(ids.Order(),all.Distinct().Order());
            Assert.True(state.SelectedPrimary==null || state.Primary.Contains(state.SelectedPrimary.Value)); Assert.True(state.SelectedSecondary==null || state.Secondary.Contains(state.SelectedSecondary.Value));
        }
    }
    [Theory]
    [InlineData(-1,.2)] [InlineData(2,.8)] [InlineData(double.NaN,.5)] [InlineData(double.PositiveInfinity,.5)] [InlineData(.6,.6)]
    public void Splitter_ratio_is_bounded(double value,double expected) { var state=new WorkspaceState(); state.SetRatio(value); Assert.Equal(expected,state.Ratio); }
}
