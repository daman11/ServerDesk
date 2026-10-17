using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class ApplicationSettingsTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-settings-"+Guid.NewGuid());
    private ProfileRepository Repo()=>new(new AppPaths(root));
    [Fact] public async Task Legacy_settings_are_read_without_losing_existing_values()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.SetSettingAsync("theme","nord"); await repo.SetSettingAsync("terminalFont","19"); await repo.SetSettingAsync("monitorInterval","20"); await repo.SetSettingAsync("rememberWorkspace","False"); await repo.SetSettingAsync("sidebar","320.5");
        var value=await repo.ApplicationSettingsAsync(); Assert.Equal("nord",value.Theme); Assert.Equal(19,value.TerminalFont); Assert.Equal(20,value.MonitorInterval); Assert.False(value.RememberWorkspace); Assert.Equal(320,value.Preferences.SidebarWidth);
    }
    [Fact] public async Task Complete_settings_roundtrip_and_opt_out_clears_workspace()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.SetSettingAsync("lastWorkspace","old snapshot");
        var value=new ApplicationSettings("light","Nord",17,15,false,new() { InterfaceFontSize=16,InterfaceFontFamily="Inter",ControlCornerRadius=0,ConnectionRowPadding=10,MenuItemPadding=2,ShowConnectionAddresses=true,ShowTabIcons=false,ShowTabCloseButtons=false,Density=InterfaceDensity.Comfortable,AccentColor="#69CFB6",TerminalFontFamily="Consolas",SidebarWidth=350,SidebarOnStartup=SidebarStartup.Hide,TabLabelWidth=180,ShowProtocolBadges=false,ShowMetrics=false,MetricSystem=false });
        await repo.SaveApplicationSettingsAsync(value); Assert.Equal(value,await Repo().ApplicationSettingsAsync()); Assert.Equal("",await repo.SettingAsync("lastWorkspace"));
    }
    [Fact] public async Task Invalid_preference_does_not_partially_save_other_settings()
    {
        var repo=Repo(); await repo.InitializeAsync(); var original=new ApplicationSettings("dark","Nord",14,5,true,new()); await repo.SaveApplicationSettingsAsync(original);
        await Assert.ThrowsAsync<ArgumentException>(()=>repo.SaveApplicationSettingsAsync(original with { Theme="light",Preferences=new() { AccentColor="invalid" } }));
        Assert.Equal(original,await repo.ApplicationSettingsAsync());
    }
    [Fact] public async Task Invalid_persisted_preferences_allow_recovery_with_defaults()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.SetSettingAsync("applicationPreferences","{broken"); Assert.Equal(new ApplicationPreferences(),await repo.PreferencesAsync());
        await repo.SetSettingAsync("applicationPreferences","{\"InterfaceFontSize\":100}"); Assert.Equal(new ApplicationPreferences(),await repo.PreferencesAsync());
    }
    [Theory]
    [InlineData("#12AAbb")]
    [InlineData("")]
    public void Valid_colors_are_accepted(string value)=>new ApplicationPreferences { AccentColor=value }.Validate();
    [Fact] public void Unknown_fonts_and_invalid_bounds_are_rejected()
    {
        Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { TerminalFontFamily="injected" }.Validate());
        Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { SidebarWidth=100 }.Validate());
        Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { Density=(InterfaceDensity)999 }.Validate());
    }
    [Fact] public async Task Older_preferences_keep_existing_values_and_gain_customization_defaults()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.SetSettingAsync("applicationPreferences","{\"InterfaceFontSize\":16,\"AccentColor\":\"#123456\"}");
        Assert.Equal(new ApplicationPreferences { InterfaceFontSize=16,AccentColor="#123456" },await repo.PreferencesAsync());
    }
    [Theory]
    [InlineData(-1,1,7)] [InlineData(13,1,7)]
    [InlineData(6,-1,7)] [InlineData(6,11,7)]
    [InlineData(6,1,1)] [InlineData(6,1,13)]
    public async Task Customization_bounds_reject_the_entire_save(int radius,int row,int menu)
    {
        var repo=Repo(); await repo.InitializeAsync(); var before=await repo.ApplicationSettingsAsync();
        await Assert.ThrowsAsync<ArgumentException>(()=>repo.SaveApplicationSettingsAsync(before with { Theme="light",Preferences=before.Preferences with { ControlCornerRadius=radius,ConnectionRowPadding=row,MenuItemPadding=menu } }));
        Assert.Equal(before,await repo.ApplicationSettingsAsync());
    }
    [Theory] [InlineData(null)] [InlineData("file:///untrusted.ttf")] [InlineData("Unknown font")]
    public void Unknown_interface_font_is_rejected(string? font)=>Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { InterfaceFontFamily=font! }.Validate());
    [Fact] public void All_interface_font_presets_and_size_boundaries_are_valid()
    {
        foreach(var font in ApplicationPreferences.InterfaceFonts)
        {
            new ApplicationPreferences { InterfaceFontFamily=font,ControlCornerRadius=0,ConnectionRowPadding=0,MenuItemPadding=2 }.Validate();
            new ApplicationPreferences { InterfaceFontFamily=font,ControlCornerRadius=12,ConnectionRowPadding=10,MenuItemPadding=12 }.Validate();
        }
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
