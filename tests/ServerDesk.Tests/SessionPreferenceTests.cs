using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class SessionPreferenceTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-session-preferences-"+Guid.NewGuid());
    private ProfileRepository Repo()=>new(new AppPaths(root));
    [Fact] public void Missing_new_json_fields_preserve_legacy_defaults()
    {
        var p=JsonSerializer.Deserialize<ApplicationPreferences>("{\"Language\":\"en\",\"InterfaceFontSize\":16}")!;
        p.Validate(); Assert.Equal("en",p.Language); Assert.Equal(16,p.InterfaceFontSize);
        Assert.Equal(10000,p.TerminalScrollbackLines); Assert.Equal(TerminalCursorShape.Bar,p.TerminalCursor); Assert.True(p.TerminalCursorBlink);
        Assert.Equal("",p.SftpLocalDirectory); Assert.False(p.SftpShowHidden); Assert.Equal(FileSort.Name,p.SftpLocalSort); Assert.Equal(FileSort.Name,p.SftpRemoteSort);
        Assert.False(p.SftpLocalDescending); Assert.False(p.SftpRemoteDescending); Assert.True(p.SftpStartRemote);
    }
    [Fact] public async Task New_preferences_roundtrip_together_without_resetting_existing_settings()
    {
        var repo=Repo(); await repo.InitializeAsync();
        var settings=new ApplicationSettings("nord","One Dark",19,15,true,new() { Language="en",InterfaceFontSize=15,TerminalScrollbackLines=0,TerminalCursor=TerminalCursorShape.Underline,TerminalCursorBlink=false,SftpLocalDirectory=Path.GetFullPath(root),SftpShowHidden=true,SftpLocalSort=FileSort.Modified,SftpRemoteSort=FileSort.Size,SftpLocalDescending=true,SftpRemoteDescending=true,SftpStartRemote=false });
        await repo.SaveApplicationSettingsAsync(settings); Assert.Equal(settings,await Repo().ApplicationSettingsAsync());
    }
    [Theory][InlineData(0)][InlineData(100000)]
    public void History_boundaries_are_supported(int lines)=>new ApplicationPreferences { TerminalScrollbackLines=lines }.Validate();
    [Theory][InlineData(-1)][InlineData(100001)][InlineData(int.MaxValue)]
    public async Task Invalid_history_is_rejected_before_any_setting_is_written(int lines)
    {
        var repo=Repo(); await repo.InitializeAsync(); var before=await repo.ApplicationSettingsAsync();
        await Assert.ThrowsAsync<ArgumentException>(()=>repo.SaveApplicationSettingsAsync(before with { Theme="light",Preferences=before.Preferences with { TerminalScrollbackLines=lines } }));
        Assert.Equal(before,await repo.ApplicationSettingsAsync());
    }
    [Fact] public void Unknown_enums_and_relative_or_invalid_paths_are_rejected()
    {
        Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { TerminalCursor=(TerminalCursorShape)99 }.Validate());
        Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { SftpLocalSort=(FileSort)99 }.Validate());
        Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { SftpRemoteSort=(FileSort)(-1) }.Validate());
        foreach(var path in new[]{"relative",".","~/files",null!,Path.GetFullPath(root)+"\0",Path.GetFullPath(root)+new string('x',4096)})
            Assert.Equal(nameof(ApplicationPreferences.SftpLocalDirectory),Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { SftpLocalDirectory=path }.Validate()).ParamName);
        // Saving a preference must not probe disks or require a folder to exist.
        new ApplicationPreferences { SftpLocalDirectory=Path.GetFullPath(root)+"-missing" }.Validate();
    }
    [Fact] public async Task Local_startup_uses_configured_directory_or_notified_home_fallback()
    {
        var home=Directory.CreateDirectory(Path.Combine(root,"home żółć")).FullName;
        var selected=Directory.CreateDirectory(Path.Combine(root,"selected folder")).FullName;
        Assert.Equal(new FileBrowserStartup(home,false),await FileBrowserStartup.ResolveAsync(new(),home));
        Assert.Equal(new FileBrowserStartup(selected,false),await FileBrowserStartup.ResolveAsync(new() { SftpLocalDirectory=selected+Path.DirectorySeparatorChar },home));
        Assert.Equal(new FileBrowserStartup(home,true),await FileBrowserStartup.ResolveAsync(new() { SftpLocalDirectory=Path.Combine(root,"missing") },home));
        var file=Path.Combine(root,"file"); await File.WriteAllTextAsync(file,"");
        Assert.Equal(new FileBrowserStartup(home,true),await FileBrowserStartup.ResolveAsync(new() { SftpLocalDirectory=file },home));
    }
    [Fact] public async Task Startup_cancels_and_does_not_fall_back_to_the_install_directory()
    {
        await Assert.ThrowsAsync<DirectoryNotFoundException>(()=>FileBrowserStartup.ResolveAsync(new(),Path.Combine(root,"missing")));
        using var cts=new CancellationTokenSource(); cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>FileBrowserStartup.ResolveAsync(new(),root,cts.Token));
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
