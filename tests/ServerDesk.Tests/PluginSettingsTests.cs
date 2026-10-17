using System.Text.Json;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class PluginSettingsTests
{
    [Fact] public void Old_settings_keep_both_plugins_enabled()
    {
        var value=JsonSerializer.Deserialize<ApplicationPreferences>("{\"ShowMetrics\":false,\"Language\":\"en\"}")!; value.Validate();
        Assert.All(PluginCatalog.BuiltIn,p=>Assert.True(PluginCatalog.IsEnabled(value,p.Id)));
        Assert.False(value.ShowMetrics); Assert.Equal("en",value.Language); Assert.False(PluginCatalog.IsEnabled(value,"not-installed"));
    }
    [Fact] public void Selection_is_immutable_and_preserves_record_equality_across_json()
    {
        var ids=new[]{PluginCatalog.Commands,"future-addon"}; var value=new ApplicationPreferences { DisabledPlugins=new(ids) }; ids[0]="mutated";
        var copy=JsonSerializer.Deserialize<ApplicationPreferences>(JsonSerializer.Serialize(value))!; copy.Validate();
        Assert.Equal(value,copy); Assert.Equal(value.GetHashCode(),copy.GetHashCode()); Assert.False(PluginCatalog.IsEnabled(copy,PluginCatalog.Commands)); Assert.True(PluginCatalog.IsEnabled(copy,PluginCatalog.LinuxMonitoring));
        Assert.Contains("future-addon",copy.DisabledPlugins); Assert.DoesNotContain("mutated",copy.DisabledPlugins);
        Assert.Equal(new PluginSelection(["b","a"]),new PluginSelection(["a","b"]));
    }
    [Theory][InlineData("")][InlineData("../plugin")][InlineData("Commands")][InlineData("bad\nname")][InlineData("name.dll/path")]
    public void Unsafe_plugin_ids_cannot_be_persisted(string id)=>Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { DisabledPlugins=new([id]) }.Validate());
    [Fact] public void Duplicate_null_and_oversized_selections_are_rejected()
    {
        Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { DisabledPlugins=null! }.Validate());
        Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { DisabledPlugins=new(["commands","commands"]) }.Validate());
        Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { DisabledPlugins=new([null!]) }.Validate());
        Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { DisabledPlugins=new([new string('a',65)]) }.Validate());
        Assert.Throws<ArgumentException>(()=>new ApplicationPreferences { DisabledPlugins=new(Enumerable.Range(0,129).Select(i=>"plugin-"+i)) }.Validate());
    }
    [Fact] public async Task Plugin_changes_roundtrip_without_a_database_migration_or_partial_save()
    {
        var root=Path.Combine(Path.GetTempPath(),"serverdesk-plugin-settings-"+Guid.NewGuid().ToString("N")); var repo=new ProfileRepository(new(root));
        try
        {
            await repo.InitializeAsync(); var settings=(await repo.ApplicationSettingsAsync()) with { Preferences=new() { DisabledPlugins=new([PluginCatalog.Commands,"future-addon"]),MetricCpu=false,TerminalScrollbackLines=2345 } };
            await repo.SaveApplicationSettingsAsync(settings); Assert.Equal(settings,await new ProfileRepository(new(root)).ApplicationSettingsAsync());
            await Assert.ThrowsAsync<ArgumentException>(()=>repo.SaveApplicationSettingsAsync(settings with { Theme="light",Preferences=settings.Preferences with { DisabledPlugins=new(["../unsafe"]) } }));
            Assert.Equal(settings,await repo.ApplicationSettingsAsync());
        }
        finally { DatabaseTestCleanup.Clear(root); if(Directory.Exists(root))Directory.Delete(root,true); }
    }
}
