using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;
[CollectionDefinition("Localization",DisableParallelization=true)] public sealed class LocalizationCollection { }
[Collection("Localization")]
public sealed class LocalizationTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-languages-"+Guid.NewGuid());
    private readonly CultureInfo original=CultureInfo.CurrentUICulture;
    private readonly CultureInfo? originalDefault=CultureInfo.DefaultThreadCurrentUICulture;
    private ProfileRepository Repo()=>new(new AppPaths(root));
    [Fact] public async Task Existing_preferences_default_to_Polish_without_losing_values()
    {
        var repo=Repo(); await repo.InitializeAsync(); await repo.SetSettingAsync("applicationPreferences","{\"InterfaceFontSize\":16,\"SidebarWidth\":350}"); var p=await repo.PreferencesAsync(); Assert.Equal("pl",p.Language); Assert.Equal(16,p.InterfaceFontSize); Assert.Equal(350,p.SidebarWidth);
    }
    [Fact] public async Task Language_roundtrips_with_all_preferences_and_rejects_unknown_values_atomically()
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=(await repo.ApplicationSettingsAsync()) with { Preferences=new() { Language="en",InterfaceFontSize=16 } }; await repo.SaveApplicationSettingsAsync(p);
        Assert.Equal(p,await Repo().ApplicationSettingsAsync()); await Assert.ThrowsAsync<ArgumentException>(()=>repo.SaveApplicationSettingsAsync(p with { Preferences=p.Preferences with { Language="xx" },Theme="light" })); Assert.Equal(p,await repo.ApplicationSettingsAsync());
    }
    [Fact] public void English_and_Polish_switch_without_changing_protocol_keys_or_user_arguments()
    {
        AppText.SetLanguage("en"); Assert.Equal("Connect",AppText.T("Połącz")); Assert.Equal("Connection history",AppText.T("Historia połączeń"));
        var name="Połącz {user} żółć"; Assert.Equal("Disconnect "+name+" and cancel transfers?",AppText.F($"Rozłączyć {name} i anulować transfery?"));
        Assert.Equal("SSH",AppText.T("SSH")); AppText.SetLanguage("pl"); Assert.Equal("Połącz",AppText.T("Połącz")); Assert.Throws<ArgumentException>(()=>AppText.SetLanguage("de")); Assert.Equal("pl",AppText.Language);
    }
    [Fact] public void Catalogs_are_complete_and_all_numbered_placeholders_are_preserved()
    {
        var assembly=typeof(AppText).Assembly; using var stream=assembly.GetManifestResourceStream("ServerDesk.Core.Localization.pl.json")!; var polish=JsonSerializer.Deserialize<Dictionary<string,string>>(stream)!;
        Assert.True(AppText.EnglishMessages.Count>700); Assert.Equal(polish.Keys.Order(),AppText.EnglishMessages.Keys.Order());
        foreach(var (key,value) in AppText.EnglishMessages)
        {
            Assert.False(string.IsNullOrWhiteSpace(value),key); Assert.Equal(key,polish[key]);
            string[] Tokens(string text)=>Regex.Matches(text,@"(?<!\{)\{\d+(?:,[^}:]+)?(?::[^}]+)?\}").Select(m=>m.Value).Order().ToArray();
            Assert.Equal(Tokens(key),Tokens(value));
            if(Tokens(key).Length>0) { _=CompositeFormat.Parse(key); _=CompositeFormat.Parse(value); }
        }
        Assert.Equal("CONNECTIONS",AppText.EnglishMessages["POŁĄCZENIA"]); Assert.Equal("Connections",AppText.EnglishMessages["Połączenia"]);
    }
    [Theory]
    [InlineData("pl","en","Restart now")]
    [InlineData("en","pl","Uruchom ponownie")]
    public void Pending_restart_uses_target_language_without_switching_runtime_cultures(string running,string target,string expected)
    {
        AppText.SetLanguage(running); var current=CultureInfo.CurrentUICulture; var defaults=CultureInfo.DefaultThreadCurrentUICulture;
        Assert.Equal(expected,AppText.T("Uruchom ponownie",target));
        var name="English żółć {0}";
        Assert.Contains(name,AppText.F($"Język po restarcie: {name}. Możesz uruchomić aplikację ponownie teraz lub później.",target));
        Assert.Equal(running,AppText.Language); Assert.Equal(current,CultureInfo.CurrentUICulture); Assert.Equal(defaults,CultureInfo.DefaultThreadCurrentUICulture);
        Assert.Throws<ArgumentException>(()=>AppText.T("Uruchom ponownie","xx"));
    }
    [Fact] public async Task Changing_language_keeps_profile_names_folders_and_export_data_verbatim()
    {
        var repo=Repo(); await repo.InitializeAsync(); var p=new ServerProfile { Name="Połącz",Host="localhost",Username="Ustawienia",Folder="Ustawienia",Description="Nie tłumacz",Tags="Polski" }; await repo.SaveAsync(p); AppText.SetLanguage("en");
        var saved=Assert.Single(await repo.ListAsync()); Assert.Equal(p,saved); using var stream=new MemoryStream(); await repo.ExportAsync(stream); using var document=JsonDocument.Parse(stream.ToArray()); Assert.Equal("Połącz",document.RootElement.GetProperty("Servers")[0].GetProperty("Name").GetString());
    }
    [Fact] public void Validation_uses_the_selected_language()
    {
        AppText.SetLanguage("en"); Assert.Equal("Invalid interface settings.",Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { InterfaceFontSize=100 }.Validate()).Message);
        Assert.Equal("Invalid VNC authentication method.",Assert.Throws<ArgumentException>(()=>new VncOptions { Authentication=(VncAuthentication)99 }.Validate()).Message);
    }
    public void Dispose() { AppText.SetLanguage("pl"); CultureInfo.CurrentUICulture=original; CultureInfo.DefaultThreadCurrentUICulture=originalDefault; DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
}
