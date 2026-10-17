using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using ServerDesk.Core;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private readonly Grid commandWorkspace=new() { ColumnDefinitions=new("*,0,0") };
    private readonly Border commandPanelHost=new() { IsVisible=false,ClipToBounds=true };
    private readonly GridSplitter commandPanelSplitter=new() { IsVisible=false,ResizeDirection=GridResizeDirection.Columns,HorizontalAlignment=HorizontalAlignment.Stretch };
    private CommandPanel? commandPanel;
    private Task commandPanelInitialization=Task.CompletedTask;
    private double commandWorkspaceWidth;
    internal SessionPane? CommandTarget=>ActiveSession;
    internal async Task OpenCommandsPanelAsync()
    {
        if(!workspaceReady || closing || closePending || !IsPluginEnabled(PluginCatalog.Commands))return;
        var panel=commandPanel;
        if(panel==null)
        {
            panel=new(this,new(repository,credentials)); commandPanel=panel; commandPanelHost.Child=panel;
            try { await (commandPanelInitialization=panel.InitializeAsync()); }
            catch(OperationCanceledException) when(!IsPluginEnabled(PluginCatalog.Commands)||closing) { return; }
        }
        if(!IsPluginEnabled(PluginCatalog.Commands)||!ReferenceEquals(panel,commandPanel))return;
        if(Bounds.Width<1150 && sidebar.IsVisible)ToggleSidebar();
        commandPanelHost.IsVisible=commandPanelSplitter.IsVisible=true;
        commandWorkspace.ColumnDefinitions[1].Width=new GridLength(4);
        commandWorkspace.ColumnDefinitions[2].Width=new GridLength(Math.Clamp(Bounds.Width*.4,390,560));
        UpdateActivity(); commandPanel.RefreshTarget(); commandPanel.FocusSearch();
    }
    internal void CloseCommandsPanel()
    {
        commandPanel?.ClearSelection();
        commandPanelHost.IsVisible=commandPanelSplitter.IsVisible=false; commandWorkspace.ColumnDefinitions[1].Width=commandWorkspace.ColumnDefinitions[2].Width=new GridLength(0); UpdateActivity();
    }
    private void BuildCommandWorkspace()
    {
        DesignSystem.Resource(commandPanelSplitter,GridSplitter.BackgroundProperty,"BorderStrong");
        commandWorkspace.Children.Add(sessionHost); Grid.SetColumn(commandPanelSplitter,1); commandWorkspace.Children.Add(commandPanelSplitter);
        Grid.SetColumn(commandPanelHost,2); commandWorkspace.Children.Add(commandPanelHost);
    }
    private void UpdateCommandTarget() { if(commandPanelHost.IsVisible)commandPanel?.RefreshTarget(); }
    internal async Task<bool> CommandsAutomaticAsync(ServerProfile profile,CancellationToken ct=default)
    {
        if(!IsPluginEnabled(PluginCatalog.Commands))return false;
        try
        {
            using var linked=CancellationTokenSource.CreateLinkedTokenSource(ct,lifetime.Token);
            var state=await new ServerDesk.Infrastructure.CommandStore(repository,credentials).ReadStateAsync(linked.Token);
            if(state.Preferences.Pinned)return false;
            var settings=state.Hosts.GetValueOrDefault(profile.Id,state.Preferences); return IsPluginEnabled(PluginCatalog.Commands)&&!linked.IsCancellationRequested&&settings.Automatic&&!settings.Pinned;
        }
        catch { return false; }
    }
    private void ResizeCommandWorkspace()
    {
        if(!commandPanelHost.IsVisible||Math.Abs(Bounds.Width-commandWorkspaceWidth)<1)return;
        commandWorkspaceWidth=Bounds.Width;
        if(Bounds.Width<1150&&sidebar.IsVisible)ToggleSidebar();
        commandWorkspace.ColumnDefinitions[2].Width=new GridLength(Math.Clamp(Bounds.Width*.4,390,560));
    }
}
