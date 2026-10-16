using Avalonia.Controls;
using Avalonia;
using ServerDesk.Core;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private ApplicationPreferences preferences=new();
    private Grid workspaceStatus=null!;
    internal ApplicationPreferences Preferences=>preferences;
    internal ApplicationPreferences CurrentPreferences=>preferences with { SidebarWidth=(int)Math.Clamp(sidebarWidth,220,500) };
    internal void PreviewPreferences(ApplicationPreferences value)
    {
        value.Validate(); preferences=value; DesignSystem.ApplyPreferences(value);
        sidebarWidth=value.SidebarWidth;
        if(sidebar.IsVisible)layout.ColumnDefinitions[1].Width=new GridLength(sidebarWidth);
        foreach(var pane in sessions)pane.ApplySessionPreferences(value);
        if(dockTabs.Count>0)LayoutDockWorkspace();
        if(workspaceStatus!=null) { workspaceStatus.IsVisible=value.ShowStatusBar; layout.RowDefinitions[2].Height=new GridLength(value.ShowStatusBar?Math.Max(26,value.InterfaceFontSize+12):0); }
        QueueTree(); UpdateStatus();
    }
}
