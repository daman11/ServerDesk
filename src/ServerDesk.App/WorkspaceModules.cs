using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private Border? activityPluginGroup;
    private readonly DeskIcon sidebarModuleIcon=DeskIcons.Create("connections",17,"ForegroundSecondary");
    private readonly TextBlock sidebarModuleHint=DesignSystem.Hint(L.T("Serwery i foldery"));

    private Control BuildActivityNavigation()
    {
        var rail=new DockPanel { Name="ModuleNavigation" }; DesignSystem.Resource(rail,DockPanel.BackgroundProperty,"BackgroundPrimary");
        Border Group(string name,string caption,params (string Id,string Title)[] entries)
        {
            var buttons=new StackPanel();
            foreach(var (id,title) in entries)
            {
                var button=DeskIcons.Button(id,L.T(title),()=>_=Safe(()=>SelectSectionAsync(id)),DesignTokens.ActivityButton-2);
                button.Margin=new Thickness(1); button.Classes.Add("activity"); activityButtons[id]=button; buttons.Children.Add(button);
            }
            var group=new Border { Name=name,Child=buttons,Margin=new Thickness(2,2,2,10),Background=Brushes.Transparent };
            AutomationProperties.SetName(group,L.T(caption)); ToolTip.SetTip(group,L.T(caption)); return group;
        }
        var settings=Group("ActivityApplication","Ustawienia aplikacji",("settings","Ustawienia")); DockPanel.SetDock(settings,Dock.Bottom); rail.Children.Add(settings);
        activityPluginGroup=Group("ActivityPlugins","Pluginy",("commands","Komendy"));
        var groups=new StackPanel { Margin=new Thickness(0,3,0,0),Children={
            Group("ActivitySessions","Połączenia",("connections","Połączenia"),("favorite","Ulubione"),("recent","Ostatnie")),
            Group("ActivityTransfers","Transfery",("transfers","Transfery")),
            Group("ActivityTools","Narzędzia sieciowe",("network","Narzędzia sieciowe")),activityPluginGroup} };
        rail.Children.Add(new ScrollViewer { Content=groups,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden });
        return rail;
    }

    private Control BuildSidebarModuleHeader()
    {
        sectionTitle.Name="SidebarModuleTitle"; sectionTitle.TextWrapping=TextWrapping.Wrap; sectionTitle.FontWeight=FontWeight.SemiBold;
        var row=new Grid { ColumnDefinitions=new("20,*,Auto"),ColumnSpacing=7,Children={sidebarModuleIcon,sectionTitle,connectionHeaderActions} };
        Grid.SetColumn(sectionTitle,1); Grid.SetColumn(connectionHeaderActions,2);
        sidebarModuleHint.Name="SidebarModuleHint"; sidebarModuleHint.IsVisible=false;
        var header=new Border { Name="SidebarModuleHeader",Child=row,Padding=new Thickness(10,10,6,8),Margin=new Thickness(0,0,0,4) };
        UpdateSidebarModule(); return header;
    }

    private void UpdateSidebarModule()
    {
        var (icon,title,hint)=section switch {
            "favorite"=>("star","Ulubione","Serwery oznaczone gwiazdką"),
            "recent"=>("history","Ostatnie","Ostatnio używane połączenia"),
            "transfers"=>("transfers","Transfery","Zadania SFTP otwartych sesji"),
            "network"=>("network","Narzędzia sieciowe","Diagnostyka sieci"),
            _=>("connections","Połączenia","Serwery i foldery") };
        sidebarModuleIcon.Glyph=DeskIcons.Resolve(icon); sectionTitle.Text=L.T(title); sidebarModuleHint.Text=L.T(hint); ToolTip.SetTip(sectionTitle,sidebarModuleHint.Text);
    }
}
