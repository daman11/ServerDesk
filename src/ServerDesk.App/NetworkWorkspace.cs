using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;
internal static class NetworkToolCatalog
{
    internal static string Name(NetworkToolKind tool)=>tool switch { NetworkToolKind.Trace=>"Traceroute / MTR",NetworkToolKind.Dns=>"DNS Lookup",NetworkToolKind.Ports=>"Port Scanner",NetworkToolKind.Wake=>"Wake-on-LAN",NetworkToolKind.Http=>"HTTP Client",NetworkToolKind.Tls=>"SSL/TLS Inspector",NetworkToolKind.Subnet=>"Subnet Calculator",NetworkToolKind.Snmp=>"SNMP Browser",_=>"Ping" };
    internal static string Icon(NetworkToolKind tool)=>tool switch { NetworkToolKind.Ping=>"chart-no-axes-combined",NetworkToolKind.Trace=>"router",NetworkToolKind.Dns=>"globe",NetworkToolKind.Ports=>"network",NetworkToolKind.Wake=>"connect",NetworkToolKind.Http=>"file-json",NetworkToolKind.Tls=>"shield",NetworkToolKind.Subnet=>"layers",_=>"server" };
    internal static string Hint(NetworkToolKind tool)=>L.T(tool switch { NetworkToolKind.Ping=>"Opóźnienie, straty pakietów i wykres ICMP.",NetworkToolKind.Trace=>"Hopy i cykliczne statystyki trasy ICMP.",NetworkToolKind.Dns=>"Rekordy i porównanie resolverów DNS.",NetworkToolKind.Ports=>"Porty TCP jednego wskazanego hosta.",NetworkToolKind.Wake=>"Wyślij Magic Packet do urządzenia.",NetworkToolKind.Http=>"Zapytania REST, nagłówki i odpowiedzi.",NetworkToolKind.Tls=>"Certyfikat, łańcuch i uzgodnione TLS.",NetworkToolKind.Subnet=>"Adresacja i podział prefiksów IPv4/IPv6.",_=>"Odczyt OID przez SNMP v2c lub v3." });
}
public sealed partial class MainWindow
{
    private readonly List<NetworkToolPane> networkPanes=[];
    private IEnumerable<WorkspacePane> Panes=>sessions.Cast<WorkspacePane>().Concat(networkPanes);
    private WorkspacePane? ActivePane=>Panes.FirstOrDefault(p=>p.WorkspaceId==dockState.Focused);
    private NetworkToolsStore? networkStore;
    private readonly StackPanel networkBrowser=new() { IsVisible=false,Margin=new Thickness(10,0,10,10),Spacing=6 };
    private readonly ScrollViewer networkBrowserHost=new() { IsVisible=false,Name="NetworkToolsSidebar" };
    private NetworkToolsStore NetworkStore=>networkStore??=new(repository,credentials);
    private void BuildNetworkBrowser()
    {
        networkBrowserHost.Content=networkBrowser;
        var hint=DesignSystem.Label(L.T("Narzędzia działają także bez zapisanych serwerów."),true,12); hint.TextWrapping=Avalonia.Media.TextWrapping.Wrap; networkBrowser.Children.Add(hint);
        foreach(var tool in Enum.GetValues<NetworkToolKind>())
        {
            var button=Dialogs.Button(NetworkToolCatalog.Name(tool),()=>OpenNetworkTool(tool)); button.Content=DeskIcons.Labelled(NetworkToolCatalog.Icon(tool),NetworkToolCatalog.Name(tool)); button.HorizontalAlignment=HorizontalAlignment.Stretch; button.HorizontalContentAlignment=HorizontalAlignment.Left; ToolTip.SetTip(button,NetworkToolCatalog.Hint(tool)); networkBrowser.Children.Add(button);
        }
    }
    internal NetworkToolPane OpenNetworkTool(NetworkToolKind tool,ServerProfile? profile=null)
    {
        var pane=new NetworkToolPane(this,tool,NetworkStore,new NetworkToolsService(),vm.AllServers.ToArray(),profile?.Host??""); RegisterPane(pane); pane.FocusContent(); return pane;
    }
    private MenuItem NetworkContextMenu(ServerProfile profile)=>new() { Header="Network Tools",Icon=DeskIcons.Create("network",16),ItemsSource=Enum.GetValues<NetworkToolKind>().Select(tool=>Menu(NetworkToolCatalog.Name(tool),()=> { OpenNetworkTool(tool,profile); return Task.CompletedTask; })).ToArray() };
    private Control WorkspaceTabHeader(WorkspacePane pane)
    {
        if(pane is SessionPane session)return TabHeader(session);
        var tool=(NetworkToolPane)pane; var title=DesignSystem.Label(tool.DisplayName); DesignSystem.Resource(title,TextBlock.MaxWidthProperty,"SessionTabMaxWidth"); title.TextTrimming=Avalonia.Media.TextTrimming.CharacterEllipsis; title.Bind(TextBlock.TextProperty,new Binding(nameof(NetworkToolPane.Caption)) { Source=tool });
        var state=new BusyIndicator { Width=14,Height=14,Classes={"busy"} }; state.Bind(Visual.IsVisibleProperty,new Binding(nameof(NetworkToolPane.IsRunning)) { Source=tool }); state.Glyph="circle-dashed";
        var close=DeskIcons.Button("close",L.T("Zamknij ")+tool.DisplayName,()=>_=Safe(()=>CloseWorkspacePaneAsync(pane)),24);
        var header=new StackPanel { Orientation=Orientation.Horizontal,Spacing=6,Children={DeskIcons.Create(NetworkToolCatalog.Icon(tool.Tool)),state,title,close} };
        header.Bind(ToolTip.TipProperty,new Binding(nameof(NetworkToolPane.Caption)) { Source=tool });
        close.Bind(ToolTip.TipProperty,new Binding(nameof(NetworkToolPane.Caption)) { Source=tool,StringFormat=L.T("Zamknij ")+"{0}" });
        close.Bind(Avalonia.Automation.AutomationProperties.NameProperty,new Binding(nameof(NetworkToolPane.Caption)) { Source=tool,StringFormat=L.T("Zamknij ")+"{0}" }); return header;
    }
    private async Task CloseWorkspacePaneAsync(WorkspacePane pane)
    {
        if(pane is SessionPane session) { await CloseSessionAsync(session); return; }
        if(!Panes.Contains(pane))return; RemovePane(pane); await ((NetworkToolPane)pane).Shutdown;
    }
}
