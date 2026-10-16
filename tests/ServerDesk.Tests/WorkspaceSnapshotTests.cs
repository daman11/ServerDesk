using ServerDesk.Core;
using Xunit;

namespace ServerDesk.Tests;
public sealed class WorkspaceSnapshotTests
{
    private static WorkspaceSnapshot Sample()
    {
        var profile=Guid.NewGuid(); var a=new SavedSession(Guid.NewGuid(),profile,SessionDock.Primary,1); var b=new SavedSession(Guid.NewGuid(),profile,SessionDock.Floating,2,new(-1600,70,920,680));
        return new(1,WorkspaceMode.Columns,.6,[a,b],a.Key,null,b.Key);
    }
    [Fact]
    public void Round_trip_keeps_duplicate_profiles_as_independent_cards_and_layout()
    {
        var snapshot=Sample(); var restored=WorkspaceSnapshot.Parse(snapshot.Serialize());
        Assert.Equal(2,restored.Sessions.Count); Assert.Equal(restored.Sessions[0].ProfileId,restored.Sessions[1].ProfileId); Assert.NotEqual(restored.Sessions[0].Key,restored.Sessions[1].Key); Assert.Equal(snapshot.Focused,restored.Focused); Assert.Equal(snapshot.Sessions[1].Window,restored.Sessions[1].Window); Assert.Equal(1,restored.Sessions[0].Panel);
        Assert.DoesNotContain("Host",snapshot.Serialize()); Assert.DoesNotContain("Secret",snapshot.Serialize()); Assert.DoesNotContain("PrivateKey",snapshot.Serialize());
    }
    [Fact]
    public void Deleted_profiles_and_dangling_selections_are_removed_together()
    {
        var snapshot=Sample(); var c=new SavedSession(Guid.NewGuid(),Guid.NewGuid(),SessionDock.Secondary); snapshot=snapshot with { Sessions=[..snapshot.Sessions,c],SelectedSecondary=c.Key,Focused=c.Key };
        var available=snapshot.AvailableProfiles([snapshot.Sessions[0].ProfileId]); Assert.Equal(2,available.Sessions.Count); Assert.Null(available.SelectedSecondary); Assert.Null(available.Focused); Assert.NotNull(available.SelectedPrimary);
    }
    [Fact]
    public void Corrupt_unsupported_or_oversized_snapshots_cannot_restore()
    {
        var sample=Sample(); Assert.Throws<InvalidDataException>(()=>(sample with { Version=2 }).Validate()); Assert.Throws<InvalidDataException>(()=>(sample with { Ratio=double.NaN }).Validate());
        Assert.Throws<InvalidDataException>(()=>(sample with { Sessions=[sample.Sessions[0],sample.Sessions[0]] }).Validate()); Assert.Throws<InvalidDataException>(()=>(sample with { SelectedPrimary=sample.Sessions[1].Key }).Validate());
        Assert.Throws<InvalidDataException>(()=>(sample with { Sessions=[new(Guid.NewGuid(),Guid.NewGuid(),SessionDock.Primary,3)] }).Validate());
        Assert.Throws<InvalidDataException>(()=>WorkspaceSnapshot.Parse(new string(' ',WorkspaceSnapshot.MaximumJsonCharacters+1)));
    }
    [Fact]
    public void Normal_window_on_negative_coordinate_monitor_stays_on_that_monitor()
    {
        var geometry=new WindowGeometry(-1500,80,800,600); var fit=geometry.Fit([new(-1920,0,1920,1080),new(0,0,1920,1080)]); Assert.Equal(geometry,fit);
    }
    [Fact]
    public void Removed_monitor_and_high_DPI_fit_keep_window_inside_available_area()
    {
        var fit=new WindowGeometry(99999,-8000,1900,1200).Fit([new(0,0,1600,1000,2)]);
        Assert.InRange(fit.X,0,1600); Assert.InRange(fit.Y,0,1000); Assert.True(fit.Width*2+fit.X<=1600); Assert.True(fit.Height*2+fit.Y+32<=1000); Assert.True(fit.Width<=800);
    }
    [Fact]
    public void Geometry_and_small_screens_do_not_allow_nonfinite_sizes()
    {
        Assert.Throws<InvalidDataException>(()=>new WindowGeometry(0,0,double.NaN,600).Validate()); var fit=new WindowGeometry(10,10,1000,800).Fit([new(0,0,480,360,2)]); Assert.Equal(240,fit.Width); Assert.Equal(148,fit.Height);
    }
}
