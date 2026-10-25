using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private ComboBox protocolSelector=null!;
    private readonly TextBlock browserCount=DesignSystem.Label("",true,11);
    private readonly StackPanel browserEmpty=new() { Spacing=10,Margin=new Thickness(18),VerticalAlignment=VerticalAlignment.Center };
    private readonly TextBlock browserEmptyTitle=DesignSystem.Label("",false,16);
    private readonly TextBlock browserEmptyHint=DesignSystem.Label("",true,12);
    private readonly DeskIcon browserEmptyIcon=DeskIcons.Create("connections",26,"ForegroundSecondary");
    private TextBox? connectionSearch;
    private Button clearBrowserFilters=null!,emptyBrowserAction=null!;
    private bool HasTagFilter=>vm.SelectedTags.Length>0 || vm.TagMode==TagFilterMode.Untagged;
    private bool HasBrowserFilter=>!string.IsNullOrWhiteSpace(vm.Search) || vm.ProtocolFilter!=null || HasTagFilter;
    // Check the live filter too: Enter can arrive before the queued tree rebuild.
    private bool IsBrowserProfileVisible(ServerProfile profile)=>
        vm.CompileBrowserFilter()(profile) &&
        (section=="connections" || section=="favorite" && profile.Favorite || section=="recent" && profile.LastConnected!=null);
    private void ClearBrowserFilters() { vm.Search=""; vm.ProtocolFilter=null; vm.SelectedTags=[]; vm.TagMode=TagFilterMode.Any; QueueTree(); connectionSearch?.Focus(); }
    private static ContextMenu DeferredBrowserMenu(Func<MenuItem[]> items)
    {
        var menu=new ContextMenu(); var populated=false;
        void Populate() { if(populated)return; menu.ItemsSource=items(); populated=true; }
        menu.Opening+=(_,_)=>Populate();
        // Avalonia's programmatic Open() can bypass Opening.
        menu.Opened+=(_,_)=>Populate(); return menu;
    }
    private void BuildBrowserContextMenu()
    {
        var clear=Menu("Wyczyść filtry",()=> { ClearBrowserFilters(); return Task.CompletedTask; });
        var expand=Menu("Rozwiń wszystkie foldery",()=> { SetBrowserFoldersExpanded(true); return Task.CompletedTask; });
        var collapse=Menu("Zwiń wszystkie foldery",()=> { SetBrowserFoldersExpanded(false); return Task.CompletedTask; });
        var menu=new ContextMenu { Name="ConnectionAreaMenu",ItemsSource=new Control[]{
            Menu("Nowe połączenie",()=>CreateProfileAsync(new ServerProfile())),
            Menu("Szybkie połączenie",QuickAsync),Menu("Nowy folder",FolderAsync),new Separator(),
            Menu("Import konfiguracji",ImportAsync),Menu("Eksport konfiguracji",ExportAsync),new Separator(),expand,collapse,new Separator(),clear} };
        bool Available()=>section is "connections" or "favorite" or "recent";
        void State() { clear.IsEnabled=HasBrowserFilter; expand.IsEnabled=collapse.IsEnabled=section=="connections" && BrowserFolders().Any(); }
        menu.Opening+=(_,e)=> { e.Cancel=!Available(); State(); };
        // Programmatic Open() can bypass Opening; keep state current there too.
        menu.Opened+=(_,_)=> { if(!Available())menu.Close(); else State(); };
        sidebar.Name="ConnectionSidebar"; sidebar.ContextMenu=menu;
    }
    private void BuildBrowserFilters()
    {
        var body=new StackPanel { Margin=new Thickness(10,0,10,8),Spacing=6 };
        protocolSelector=new ComboBox { Name="ConnectionProtocolFilter",ItemsSource=new[]{L.T("Wszystkie"),"SSH","RDP","VNC","Serial"},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch,MinHeight=30,Classes={"quiet-select"} };
        AutomationProperties.SetName(protocolSelector,L.T("Filtr protokołu"));
        ToolTip.SetTip(protocolSelector,L.T("Filtr protokołu"));
        protocolSelector.SelectionChanged+=(_,_)=>
        {
            var filter=protocolSelector.SelectedIndex<=0?null:(Protocol?)(protocolSelector.SelectedIndex-1);
            if(filter!=vm.ProtocolFilter) { vm.ProtocolFilter=filter; QueueTree(); }
        };
        BuildTagFilters(body,protocolSelector);
        var stats=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto"),ColumnSpacing=8 }; browserCount.Name="ConnectionResultCount"; browserCount.TextTrimming=TextTrimming.CharacterEllipsis; stats.Children.Add(browserCount);
        clearBrowserFilters=DeskIcons.Button("close","Wyczyść filtry",ClearBrowserFilters,24); clearBrowserFilters.Classes.Add("chrome"); Grid.SetColumn(clearBrowserFilters,1); stats.Children.Add(clearBrowserFilters); body.Children.Add(stats);
        clearBrowserFilters.Name="ClearConnectionFilters"; clearBrowserFilters.Width=double.NaN; clearBrowserFilters.Padding=new Thickness(6,2); clearBrowserFilters.Content=DeskIcons.Labelled("close",L.T("Wyczyść"),14);
        connectionTools.Children.Add(body);
        browserEmptyTitle.TextWrapping=browserEmptyHint.TextWrapping=TextWrapping.Wrap;
        browserEmpty.Children.Add(browserEmptyIcon); browserEmpty.Children.Add(browserEmptyTitle); browserEmpty.Children.Add(browserEmptyHint);
        emptyBrowserAction=Dialogs.Button("Wyczyść filtry",()=>
        {
            if(HasBrowserFilter)ClearBrowserFilters();
            else if((section is "favorite" or "recent") && vm.AllServers.Count>0)_=Safe(()=>SelectSectionAsync("connections"));
            else _=Safe(()=>EditAsync(null));
        }); emptyBrowserAction.HorizontalAlignment=HorizontalAlignment.Left; browserEmpty.Children.Add(emptyBrowserAction);
    }
    private void UpdateBrowserState(int count)
    {
        rootProfileDrop.IsVisible=section=="connections" && count>0;
        protocolSelector.SelectedIndex=vm.ProtocolFilter==null?0:(int)vm.ProtocolFilter+1;
        ToolTip.SetTip(protocolSelector,vm.ProtocolFilter==null?L.T("Wszystkie protokoły"):L.T("Filtr protokołu: ")+vm.ProtocolFilter);
        browserCount.Text=L.F($"Widoczne: {count} / {vm.AllServers.Count}");
        ToolTip.SetTip(browserCount,browserCount.Text);
        clearBrowserFilters.IsVisible=HasBrowserFilter;
        UpdateTagFilters();
        browserEmpty.IsVisible=(section is "connections" or "favorite" or "recent") && count==0 && tree.Items.Count==0;
        browserEmptyTitle.Text=HasBrowserFilter?L.T("Brak wyników"):section=="favorite"?L.T("Brak ulubionych"):section=="recent"?L.T("Brak ostatnich połączeń"):L.T("Dodaj pierwszy serwer");
        browserEmptyHint.Text=HasBrowserFilter?L.T("Zmień zapytanie lub wyczyść filtry."):section=="favorite"?L.T("Oznacz serwer gwiazdką w jego menu."):section=="recent"?L.T("Tutaj pojawią się potwierdzone połączenia SSH, RDP, VNC i Serial."):L.T("Zapisz połączenie SSH, RDP, VNC lub Serial.");
        var showConnections=!HasBrowserFilter && (section is "favorite" or "recent") && vm.AllServers.Count>0;
        browserEmptyIcon.Glyph=DeskIcons.Resolve(HasBrowserFilter?"search":section=="favorite"?"star":section=="recent"?"history":"connections");
        emptyBrowserAction.Content=DeskIcons.Labelled(HasBrowserFilter?"close":showConnections?"connections":"plus",HasBrowserFilter?L.T("Wyczyść filtry"):showConnections?L.T("Lista połączeń"):L.T("Nowe połączenie"));
        AutomationProperties.SetName(emptyBrowserAction,HasBrowserFilter?L.T("Wyczyść filtry pustej listy"):showConnections?L.T("Pokaż wszystkie połączenia"):L.T("Dodaj pierwszy serwer"));
        ToolTip.SetTip(emptyBrowserAction,AutomationProperties.GetName(emptyBrowserAction));
        emptyBrowserAction.IsVisible=true;
    }
}
