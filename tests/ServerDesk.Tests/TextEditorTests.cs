using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class TextEditorTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-editor-"+Guid.NewGuid());
    [Fact] public void Legacy_preferences_keep_automatic_editor_and_empty_custom_path()
    {
        var value=JsonSerializer.Deserialize<ApplicationPreferences>("{\"InterfaceFontSize\":15}")!;
        value.Validate(); Assert.Equal(TextEditorKind.Automatic,value.SftpTextEditor); Assert.Equal("",value.SftpTextEditorPath);
    }
    [Fact] public async Task Editor_and_all_theme_ids_roundtrip_without_changing_other_preferences()
    {
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync();
        foreach(var theme in ApplicationThemes.Ids)
        {
            var value=new ApplicationSettings(theme,"Nord",17,8,true,new() { Language="en",SftpTextEditor=TextEditorKind.Custom,SftpTextEditorPath=Path.Combine(root,"Editor folder","editor.exe"),SftpShowHidden=true });
            await repo.SaveApplicationSettingsAsync(value); Assert.Equal(value,await repo.ApplicationSettingsAsync());
        }
    }
    [Theory] [InlineData("")] [InlineData("relative/editor")] [InlineData("editor --wait")] [InlineData(null)]
    public async Task Invalid_custom_editor_cannot_partially_save(string? path)
    {
        var repo=new ProfileRepository(new AppPaths(root)); await repo.InitializeAsync(); var original=await repo.ApplicationSettingsAsync();
        await Assert.ThrowsAsync<ArgumentException>(()=>repo.SaveApplicationSettingsAsync(original with { Theme="ocean",Preferences=original.Preferences with { SftpTextEditor=TextEditorKind.Custom,SftpTextEditorPath=path! } }));
        Assert.Equal(original,await repo.ApplicationSettingsAsync());
    }
    [Fact] public void Unknown_editor_and_control_characters_are_rejected()
    {
        Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { SftpTextEditor=(TextEditorKind)99 }.Validate());
        foreach(var suffix in new[]{"\0","\n","\r"})Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { SftpTextEditorPath=Path.GetFullPath(root)+suffix }.Validate());
        Assert.False(ApplicationThemes.IsSupported("unknown"));
    }
    [Fact] public void Editor_launch_keeps_spaces_unicode_quotes_and_shell_metacharacters_in_one_literal_argument()
    {
        Directory.CreateDirectory(root); var executable=Path.Combine(root,"my editor.exe"); File.WriteAllText(executable,"");
        var preferences=new ApplicationPreferences { SftpTextEditor=TextEditorKind.Custom,SftpTextEditorPath=executable };
        var file=Path.Combine(root,"--config żółć & $(command) 'quoted'.txt");
        var start=TextEditorLauncher.CreateStartInfo(preferences,file);
        Assert.Equal(executable,start.FileName); Assert.False(start.UseShellExecute); Assert.Equal("",start.Arguments); Assert.Equal([file],start.ArgumentList);
        Assert.Throws<ArgumentException>(()=>TextEditorLauncher.CreateStartInfo(preferences,"relative.txt"));
    }
    [Fact] public void Missing_editor_fails_without_association_or_shell_fallback()
    {
        var preferences=new ApplicationPreferences { SftpTextEditor=TextEditorKind.Custom,SftpTextEditorPath=Path.Combine(root,"missing.exe") };
        Assert.Throws<FileNotFoundException>(()=>TextEditorLauncher.CreateStartInfo(preferences,Path.Combine(root,"remote.exe")));
    }
    public void Dispose() { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
