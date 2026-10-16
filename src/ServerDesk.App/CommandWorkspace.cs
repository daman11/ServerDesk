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
    private double commandWorkspaceWidth;
    internal SessionPane? CommandTarget=>ActiveSession;
    internal async Task OpenCommandsPanelAsync()
    {
        if(!workspaceReady)return;
        if(commandPanel==null) { commandPanel=new(this,new(repository,credentials)); commandPanelHost.Child=commandPanel; await commandPanel.InitializeAsync(); }
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
        commandWorkspace.Children.Add(sessionHost); Grid.SetColumn(commandPanelSplitter,1); commandWorkspace.Children.Add(commandPanelSplitter);
        Grid.SetColumn(commandPanelHost,2); commandWorkspace.Children.Add(commandPanelHost);
    }
    private void UpdateCommandTarget() { if(commandPanelHost.IsVisible)commandPanel?.RefreshTarget(); }
    internal async Task<bool> CommandsAutomaticAsync(ServerProfile profile)
    {
        try
        {
            var state=await new ServerDesk.Infrastructure.CommandStore(repository,credentials).ReadStateAsync(lifetime.Token);
            if(state.Preferences.Pinned)return false;
            var settings=state.Hosts.GetValueOrDefault(profile.Id,state.Preferences); return settings.Automatic&&!settings.Pinned;
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
