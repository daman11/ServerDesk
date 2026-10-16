using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.Text.Json;
using Xunit;

namespace ServerDesk.Tests;
public sealed class WindowPlacementTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-placement-"+Guid.NewGuid());
    private ProfileRepository Repo()=>new(new AppPaths(root));
    private static WindowGeometry Fit(WindowGeometry geometry,params ScreenArea[] screens)=>geometry.Fit(screens,900,600,2400,1600,16,16);
    private static void Inside(WindowGeometry fit,ScreenArea s,double frameWidth=16,double frameHeight=16)
    {
        Assert.True(fit.X>=s.X && fit.Y>=s.Y);
        Assert.True(fit.X+Math.Ceiling((fit.Width+frameWidth)*s.Scale)<=(long)s.X+s.Width);
        Assert.True(fit.Y+Math.Ceiling((fit.Height+frameHeight)*s.Scale)<=(long)s.Y+s.Height);
    }
    [Theory] [InlineData(1)] [InlineData(1.25)] [InlineData(1.5)] [InlineData(1.75)] [InlineData(2)]
    public void Whole_window_fits_even_when_saved_corner_is_inside_screen(double scale)
    {
        var screen=new ScreenArea(0,40,1366,688,scale); var fit=Fit(new(1200,600,1320,840),screen); Inside(fit,screen);
        Assert.True(fit.Width<=1320 && fit.Height<=840);
    }
    [Fact] public void Negative_monitor_coordinates_and_secondary_scale_are_preserved()
    {
        var a=new ScreenArea(0,0,1920,1040); var b=new ScreenArea(-2560,-120,2560,1400,1.5);
        var original=new WindowGeometry(-2300,20,1100,700); Assert.Equal(original,Fit(original,a,b)); Inside(Fit(new(-200,1000,1320,840),b),b);
    }
    [Fact] public void Best_overlap_keeps_window_on_correct_monitor_when_origin_is_in_gap()
    {
        var a=new ScreenArea(0,0,1920,1040); var b=new ScreenArea(2100,0,1920,1040);
        var fit=Fit(new(2050,100,1000,650),a,b); Inside(fit,b);
    }
    [Fact] public void Disconnected_monitor_recenters_on_primary()
    {
        var screen=new ScreenArea(0,0,1920,1040); var fit=Fit(new(9000,-9000,1000,650),screen); Inside(fit,screen); Assert.Equal((1920-1016)/2,fit.X); Assert.Equal((1040-666)/2,fit.Y);
    }
    [Fact] public void Invalid_screens_are_ignored_and_geometry_is_stable_across_repeated_fit()
    {
        var s=new ScreenArea(80,60,1600,900,1.25); var fit=Fit(new(2000,1500,1320,840),new(0,0,0,0),new(0,0,1000,800,double.NaN),s); Inside(fit,s); Assert.Equal(fit,Fit(fit,s));
    }
    [Fact] public void Floating_window_titlebar_and_frame_scale_with_monitor()
    {
        var s=new ScreenArea(-1920,40,1920,1000,2); var fit=new WindowGeometry(-300,800,1000,720).Fit([s],frameWidth:16,frameHeight:40); Inside(fit,s,16,40);
    }
    [Fact] public async Task Placement_saves_atomically_and_takes_precedence_over_legacy_keys()
    {
        var repo=Repo(); await repo.InitializeAsync(); var expected=new MainWindowPlacement(1,new(-1800,70,1100,700),true);
        await repo.SetSettingAsync("x","99999"); await repo.SetSettingAsync("y","99999"); await repo.SaveMainWindowPlacementAsync(expected);
        Assert.Equal(expected,await Repo().MainWindowPlacementAsync());
        await Assert.ThrowsAsync<InvalidDataException>(()=>repo.SaveMainWindowPlacementAsync(expected with { NormalBounds=new(0,0,double.NaN,700) })); Assert.Equal(expected,await repo.MainWindowPlacementAsync());
    }
    [Theory] [InlineData("NaN")] [InlineData("Infinity")] [InlineData("broken")]
    public async Task Legacy_invalid_size_falls_back_without_breaking_startup(string value)
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.SetSettingAsync("x","-1800"); await repo.SetSettingAsync("y","80"); await repo.SetSettingAsync("width",value); await repo.SetSettingAsync("height",value);
        var p=await repo.MainWindowPlacementAsync(); Assert.Equal(new(-1800,80,1320,840),p!.NormalBounds); Assert.False(p.Maximized);
    }
    [Fact] public async Task Corrupt_placement_falls_back_to_legacy_or_centered_default()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.SetSettingAsync("mainWindowPlacement","{bad"); Assert.Null(await repo.MainWindowPlacementAsync());
        await repo.SetSettingAsync("x","10"); await repo.SetSettingAsync("y","20"); Assert.NotNull(await repo.MainWindowPlacementAsync());
        await repo.SetSettingAsync("mainWindowPlacement",JsonSerializer.Serialize(new { Version=1,NormalBounds=(object?)null })); Assert.NotNull(await repo.MainWindowPlacementAsync());
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
