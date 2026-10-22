using Avalonia.Controls;
using ServerDesk.Core;

namespace ServerDesk.App;

// Each built-in plugin registers its lifecycle here; UI entry points use stable
// catalogue IDs. Optional work never shares the terminal's cancellation source.
internal interface IWorkspacePlugin
{
    string Id { get; }
    Task ApplyAsync(MainWindow workspace,bool enabled);
}
internal sealed class LinuxMonitoringPlugin:IWorkspacePlugin
{
    public string Id=>PluginCatalog.LinuxMonitoring;
    public Task ApplyAsync(MainWindow workspace,bool enabled)=>Task.WhenAll(workspace.OpenSessions.Select(p=>p.ApplyMonitoringPluginAsync(enabled)));
}
internal sealed class CommandsPlugin:IWorkspacePlugin
{
    public string Id=>PluginCatalog.Commands;
    public Task ApplyAsync(MainWindow workspace,bool enabled)=>workspace.ApplyCommandsPluginAsync(enabled);
}
internal sealed class BitwardenPlugin:IWorkspacePlugin
{
    public string Id=>PluginCatalog.Bitwarden;
    public Task ApplyAsync(MainWindow workspace,bool enabled)=>workspace.ApplyBitwardenPluginAsync(enabled);
}
public sealed partial class MainWindow
{
    private readonly IWorkspacePlugin[] plugins=[new LinuxMonitoringPlugin(),new CommandsPlugin(),new BitwardenPlugin()];
    private readonly HashSet<string> disabledPlugins=new(StringComparer.Ordinal);
    private readonly SemaphoreSlim pluginGate=new(1);
    private readonly Dictionary<string,bool> appliedPlugins=new(StringComparer.Ordinal);
    private MenuItem? commandsMenu;
    internal bool IsPluginEnabled(string id)=>PluginCatalog.IsKnown(id)&&!disabledPlugins.Contains(id);
    internal async Task ApplyPluginsAsync(ApplicationPreferences value)
    {
        value.Validate(); await pluginGate.WaitAsync(lifetime.Token);
        try
        {
            var changed=plugins.Where(p=>!appliedPlugins.TryGetValue(p.Id,out var enabled)||enabled!=PluginCatalog.IsEnabled(value,p.Id)||p.Id==PluginCatalog.Bitwarden && bitwardenOptions!=value.Bitwarden).ToArray();
            preferences=preferences with { DisabledPlugins=value.DisabledPlugins,Bitwarden=value.Bitwarden };
            disabledPlugins.Clear(); disabledPlugins.UnionWith(value.DisabledPlugins);
            RefreshPluginPresentation();
            await Task.WhenAll(changed.Select(async p=> { var enabled=IsPluginEnabled(p.Id); await p.ApplyAsync(this,enabled); appliedPlugins[p.Id]=enabled; }));
            UpdateStatus();
        }
        finally { pluginGate.Release(); }
    }
    private void RefreshPluginPresentation()
    {
        var commands=IsPluginEnabled(PluginCatalog.Commands);
        if(commandsMenu!=null)commandsMenu.IsVisible=commands;
        if(activityButtons.TryGetValue("commands",out var activity))activity.IsVisible=commands;
        UpdateActivity();
        foreach(var pane in sessions)pane.UpdatePluginPresentation();
    }
    internal async Task ApplyCommandsPluginAsync(bool enabled)
    {
        if(!enabled)
        {
            var old=commandPanel; var initialization=commandPanelInitialization; commandPanelInitialization=Task.CompletedTask;
            commandPanel=null; CloseCommandsPanel(); commandPanelHost.Child=null;
            old?.Dispose();
            await Task.WhenAll(sessions.Select(p=>p.StopCommandPluginAsync()).Append(DrainPanel()));
            async Task DrainPanel() { try { await Task.WhenAll(initialization,old?.Shutdown??Task.CompletedTask); } catch(OperationCanceledException) { } }
        }
        else foreach(var pane in sessions)pane.ResumeCommandPlugin();
    }
}
