using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Chrome;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Iciclecreek.Terminal;
using ServerDesk.Core;

namespace ServerDesk.App;
public sealed partial class MainWindow
{
    private readonly TreeView tree=new();
    private readonly TextBlock sectionTitle=DesignSystem.Label(L.T("POŁĄCZENIA"),true,12);
    private readonly TextBlock connectionStatus=DesignSystem.Label(L.T("Brak aktywnej sesji"),true,12);
    private readonly TextBlock transferStatus=DesignSystem.Label(L.T("Brak transferów"),true,12);
    private readonly ServerMetricsBar metricsBar=new();
    private readonly TextBlock encoding=DesignSystem.Label("UTF-8",true,12);
    private readonly TextBlock workspaceTitle=DesignSystem.Label(L.T("Obszar pracy"),true,DesignTokens.Caption);
    private readonly DispatcherTimer statusTimer=new() { Interval=TimeSpan.FromSeconds(1) };
    private readonly Dictionary<string,Button> activityButtons=[];
    private readonly DockPanel sidebar=new();
    private readonly StackPanel connectionTools=new();
    private readonly StackPanel connectionHeaderActions=new() { Orientation=Orientation.Horizontal };
    private readonly Grid connectionBrowser=new();
    private readonly ComboBox sortSelector=new() { ItemsSource=new[]{L.T("Nazwa (A–Z)"),L.T("Adres (A–Z)"),L.T("Protokół"),L.T("Ostatnie połączenie")},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch,Margin=new Thickness(10,0,10,8),Classes={"quiet-select"} };
    private string section="connections";
    private double sidebarWidth=272;
    private bool treePending;
    private string connectedKey="";
    private readonly HashSet<string> collapsed=[];
    private void BuildWorkspace()
    {
        var titlebar=new Grid { Name="WorkspaceTitleBar",ColumnDefinitions=new ColumnDefinitions("Auto,*,Auto,Auto"),Margin=new Thickness(12,0,0,0),ColumnSpacing=12,ClipToBounds=true,Background=Brushes.Transparent };
        WindowDecorationProperties.SetElementRole(titlebar,WindowDecorationsElementRole.TitleBar);
        var appTitle=DesignSystem.Label("ServerDesk",false,14); appTitle.Name="ApplicationTitle";
        titlebar.Children.Add(new StackPanel { Orientation=Orientation.Horizontal,Spacing=DesignTokens.Medium,Children={new BrandMark { Width=20,Height=20 },appTitle} });
        workspaceTitle.HorizontalAlignment=HorizontalAlignment.Center; workspaceTitle.TextTrimming=TextTrimming.CharacterEllipsis; Grid.SetColumn(workspaceTitle,1); titlebar.Children.Add(workspaceTitle);
        var titleActions=new StackPanel { Name="WorkspaceTitleActions",Orientation=Orientation.Horizontal,Spacing=DesignTokens.Small };
        WindowDecorationProperties.SetElementRole(titleActions,WindowDecorationsElementRole.User);
        titleActions.Children.Add(DeskIcons.Button("sidebar","Panel połączeń · Ctrl+B",ToggleSidebar));
        titleActions.Children.Add(DeskIcons.Button("plus","Nowe połączenie · Ctrl+Shift+N",()=>_ = Safe(()=>EditAsync(null))));
        titleActions.Children.Add(DeskIcons.Button("search","Otwarte sesje · Ctrl+Shift+L",()=>_ = Safe(()=>SwitchSessionAsync())));
        var docking=new Button { Content=DeskIcons.Labelled("layers",L.T("Układ")),Tag="Układ",Classes={"icon","chrome"} };
        docking.ContextMenu=new ContextMenu { ItemsSource=new[]{Menu("Jeden panel",()=> { SetWorkspaceMode(WorkspaceMode.Single); return Task.CompletedTask; }),Menu("Panele obok siebie",()=> { SetWorkspaceMode(WorkspaceMode.Columns); return Task.CompletedTask; }),Menu("Panele jeden nad drugim",()=> { SetWorkspaceMode(WorkspaceMode.Rows); return Task.CompletedTask; }),Menu("Trzy panele (1 + 2)",()=> { SetWorkspaceMode(WorkspaceMode.Three); return Task.CompletedTask; }),Menu("Cztery panele (2 × 2)",()=> { SetWorkspaceMode(WorkspaceMode.Grid); return Task.CompletedTask; }),Menu("Wyrównaj wielkość paneli",()=> { EqualizeWorkspace(); return Task.CompletedTask; }),Menu("Przywróć wszystkie odpięte sesje",()=> { foreach(var pane in Panes.Where(p=>dockState.Location(p.WorkspaceId)==SessionDock.Floating).ToArray())DockSession(pane,SessionDock.Primary); return Task.CompletedTask; })} };
        docking.Click+=(_,_)=>docking.ContextMenu.Open(docking); titleActions.Children.Add(docking);
        var more=new Button { Content=DeskIcons.Labelled("more","Menu"),Tag="Menu",Classes={"icon","chrome"} };
        more.ContextMenu=new ContextMenu { ItemsSource=new[]{Menu("Szybkie połączenie",QuickAsync),Menu("Nowy folder",FolderAsync),Menu("Import konfiguracji",ImportAsync),Menu("Eksport konfiguracji",ExportAsync),Menu("Paleta poleceń · Ctrl+Shift+P",CommandsAsync),Menu("Ustawienia",SettingsAsync),Menu("Historia połączeń",()=>HistoryAsync()),Menu("Network Tools",()=>SelectSectionAsync("network")),Menu("O aplikacji",AboutAsync)} };
        more.Click+=(_,_)=>more.ContextMenu.Open(more); titleActions.Children.Add(more); Grid.SetColumn(titleActions,2); titlebar.Children.Add(titleActions);
        BuildWindowCaptions(titlebar);
        var header=BuildWorkspaceHeader(titlebar); Grid.SetColumnSpan(header,4); layout.Children.Add(header);
        var activity=new DockPanel(); DesignSystem.Resource(activity,DockPanel.BackgroundProperty,"BackgroundPrimary"); Grid.SetRow(activity,1); layout.Children.Add(activity);
        var bottom=new StackPanel(); DockPanel.SetDock(bottom,Dock.Bottom); activity.Children.Add(bottom);
        var activityList=new StackPanel(); activity.Children.Add(activityList);
        foreach(var item in new[]{("connections",L.T("Połączenia")),("favorite",L.T("Ulubione")),("recent",L.T("Ostatnie")),("transfers",L.T("Transfery")),("network", "Network Tools"),("settings",L.T("Ustawienia"))})
        {
            var button=DeskIcons.Button(item.Item1,item.Item2,()=>_ = Safe(()=>SelectSectionAsync(item.Item1)),DesignTokens.ActivityButton); button.Margin=new Thickness(3); button.Classes.Add("activity");
            activityButtons[item.Item1]=button; (item.Item1=="settings"?bottom:activityList).Children.Add(button);
        }
        DesignSystem.Resource(sidebar,DockPanel.BackgroundProperty,"BackgroundSecondary"); Grid.SetRow(sidebar,1); Grid.SetColumn(sidebar,1); layout.Children.Add(sidebar);
        var sideHeader=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto"),Height=44,Margin=new Thickness(12,0,6,0) }; sideHeader.Children.Add(sectionTitle);
        var add=DeskIcons.Button("plus","Dodaj połączenie",()=>_ = Safe(()=>EditAsync(null))); connectionHeaderActions.Children.Add(add);
        var newFolder=DeskIcons.Button("folder","Dodaj folder",()=>_ = Safe(FolderAsync)); connectionHeaderActions.Children.Add(newFolder); Grid.SetColumn(connectionHeaderActions,1); sideHeader.Children.Add(connectionHeaderActions); DockPanel.SetDock(sideHeader,Dock.Top); sidebar.Children.Add(sideHeader);
        connectionTools.Name="ConnectionBrowserTools"; DockPanel.SetDock(connectionTools,Dock.Top); sidebar.Children.Add(connectionTools);
        var search=new TextBox { PlaceholderText=L.T("Nazwa, adres, login lub tag"),Margin=new Thickness(10,0,10,8) }; search.Bind(TextBox.TextProperty,new Binding(nameof(vm.Search)) { Mode=BindingMode.TwoWay }); connectionTools.Children.Add(search);
        ToolTip.SetTip(search,L.T("Szukaj po nazwie, adresie, loginie, tagach, opisie, folderze i protokole. Kilka słów zawęża wyniki. Escape czyści zapytanie."));
        search.KeyDown+=(_,e)=> { if(e.Key==Key.Escape && !string.IsNullOrEmpty(search.Text)) { search.Text=""; e.Handled=true; } };
        search.InnerLeftContent=new Border { Child=DeskIcons.Create("search",14,"ForegroundSecondary"),Margin=new Thickness(8,0,0,0) };
        var clearSearch=DeskIcons.Button("close","Wyczyść wyszukiwanie",()=>search.Text="",24); clearSearch.IsVisible=false; search.InnerRightContent=clearSearch;
        search.TextChanged+=(_,_)=>clearSearch.IsVisible=!string.IsNullOrEmpty(search.Text); Avalonia.Automation.AutomationProperties.SetName(search,L.T("Szukaj połączeń"));
        BuildBrowserFilters();
        connectionTools.Children.Add(BuildConnectionSorting());
        tree.SelectionChanged+=(_,_)=> { if(tree.SelectedItem is TreeViewItem node) { if(node.Tag is ServerProfile p) { vm.SelectedServer=p; servers.SelectedItem=p; } else if(node.Tag is string f) { vm.SelectedServer=null; vm.SelectedFolder=f; } } };
        tree.DoubleTapped+=(_,_)=> { if(tree.SelectedItem is TreeViewItem { Tag:ServerProfile p })_ = Safe(()=>ConnectAsync(p)); };
        tree.KeyDown+=(_,e)=> { if(e.Key==Key.Enter && tree.SelectedItem is TreeViewItem { Tag:ServerProfile p }) { e.Handled=true; _ = Safe(()=>ConnectAsync(p)); } };
        tree.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled); connectionBrowser.Children.Add(tree); connectionBrowser.Children.Add(browserEmpty);
        BuildTransferOverview(); BuildNetworkBrowser(); sidebar.Children.Add(new Grid { Children={connectionBrowser,transferOverview,networkBrowserHost } });
        vm.Servers.CollectionChanged+=(_,_)=>QueueTree();
        var splitter=new GridSplitter { ResizeDirection=GridResizeDirection.Columns,HorizontalAlignment=HorizontalAlignment.Stretch }; Grid.SetColumn(splitter,2); Grid.SetRow(splitter,1); layout.Children.Add(splitter);
        splitter.DragCompleted+=(_,_)=> { if(sidebar.IsVisible)sidebarWidth=layout.ColumnDefinitions[1].ActualWidth; };
        BuildDockWorkspace(); Grid.SetColumn(sessionHost,3); Grid.SetRow(sessionHost,1); layout.Children.Add(sessionHost);
        var status=new Grid { Name="WorkspaceStatusBar",ColumnDefinitions=new ColumnDefinitions("180,*,Auto,Auto"),Margin=new Thickness(10,0),ColumnSpacing=10,ClipToBounds=true }; workspaceStatus=status;
        var statusRule=new Border { Height=1,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Margin=new Thickness(-10,0) }; DesignSystem.Resource(statusRule,Border.BackgroundProperty,"BorderDefault"); Grid.SetColumnSpan(statusRule,4); status.Children.Add(statusRule);
        status.Children.Add(connectionStatus); Grid.SetColumn(metricsBar,1); status.Children.Add(metricsBar); Grid.SetColumn(transferStatus,2); status.Children.Add(transferStatus); Grid.SetColumn(encoding,3); status.Children.Add(encoding);
        connectionStatus.TextTrimming=TextTrimming.CharacterEllipsis;
        Grid.SetRow(status,2); Grid.SetColumnSpan(status,4); layout.Children.Add(status); Content=layout;
        AddHandler(KeyDownEvent,HandleKeys,RoutingStrategies.Tunnel); SizeChanged+=(_,_)=>UpdateStatus(); statusTimer.Tick+=(_,_)=>UpdateStatus(); statusTimer.Start(); UpdateActivity(); UpdateStatus();
    }
    private void QueueTree()
    { if(treePending)return; treePending=true; Dispatcher.UIThread.Post(()=> { treePending=false; BuildTree(); },DispatcherPriority.Background); }
    private void BuildTree()
    {
        var selected=vm.SelectedServer?.Id; var matches=ProfileSearch.Compile(vm.Search); var profiles=vm.AllServers.Where(p=>matches(p) && (vm.ProtocolFilter==null || p.Protocol==vm.ProtocolFilter));
        if(section=="favorite")profiles=profiles.Where(p=>p.Favorite);
        if(section=="recent")profiles=profiles.Where(p=>p.LastConnected!=null);
        profiles=ProfileOrdering.Sort(profiles,vm.SortMode,vm.SortDescending).ToArray();
        var counts=new Dictionary<string,int>(); foreach(var item in profiles) { var path=item.Folder; while(path.Length>0) { counts[path]=counts.GetValueOrDefault(path)+1; var split=path.LastIndexOf('/'); path=split<0?"":path[..split]; } }
        var roots=new List<TreeViewItem>(); var folders=new Dictionary<string,TreeViewItem>();
        TreeViewItem Folder(string path)
        {
            if(folders.TryGetValue(path,out var found))return found;
            var slash=path.LastIndexOf('/'); var node=new TreeViewItem { Header=Row(FolderIcons.Resolve(vm.FolderIcons.GetValueOrDefault(path)),slash<0?path:path[(slash+1)..],count:counts.GetValueOrDefault(path)),Tag=path,IsExpanded=!string.IsNullOrWhiteSpace(vm.Search) || !collapsed.Contains(path) };
            node.PropertyChanged+=(_,e)=> { if(e.Property==TreeViewItem.IsExpandedProperty) { if(node.IsExpanded)collapsed.Remove(path); else collapsed.Add(path); } };
            node.ContextMenu=new ContextMenu { ItemsSource=new[]{Menu("Nowy serwer w folderze",()=>EditAsync(new ServerProfile { Folder=path })),Menu("Zmień ikonę folderu",()=>EditFolderIconAsync(path)),Menu("Zmień nazwę",async()=> { vm.SelectedFolder=path; await RenameFolderAsync(); }),Menu("Usuń pusty folder",async()=> { vm.SelectedFolder=path; await DeleteFolderAsync(); })} };
            DragDrop.SetAllowDrop(node,true);
            DragDrop.AddDragOverHandler(node,(_,e)=> { e.DragEffects=e.DataTransfer.Contains(ServerDrag)?DragDropEffects.Move:DragDropEffects.None; e.Handled=true; });
            DragDrop.AddDropHandler(node,(_,e)=> { if(e.DataTransfer.TryGetValue(ServerDrag) is {} p)_ = Safe(async()=> { await repository.MoveToFolderAsync(p.Id,path); await vm.LoadAsync(); }); e.Handled=true; });
            folders[path]=node; if(slash<0)roots.Add(node); else Folder(path[..slash]).Items.Add(node); return node;
        }
        if(section=="connections" && !HasBrowserFilter)foreach(var path in vm.Folders.Where(f=>f is not ("Wszystkie" or "★ Ulubione" or "◷ Ostatnie")))Folder(path);
        foreach(var p in profiles)
        {
            var connected=sessions.Any(s=>s.Profile.Id==p.Id && (s.IsConnected || s.IsRdpLoggedIn));
            var sortDetail=vm.SortMode==ProfileSort.Host?SortAddress(p):vm.SortMode==ProfileSort.LastConnected?p.LastConnected?.ToLocalTime().ToString("g")??"—":null;
            var row=Row(ProfileIcons.Resolve(p),p.Name,p.Favorite,connected,preferences.ShowProtocolBadges?p.Protocol.ToString():null,p,detail:sortDetail);
            var node=new TreeViewItem { Header=row,Tag=p }; ToolTip.SetTip(node,$"{p.Protocol} · {(p.Protocol==Protocol.SSH?p.Username+"@":"")}{p.Endpoint}\n{p.Description}");
            var context=new ContextMenu(); node.ContextMenu=context; var populated=false;
            void PopulateContext()
            {
                if(populated)return; populated=true;
                context.ItemsSource=new[]{Menu("Połącz",()=>ConnectAsync(p)),Menu("Edytuj",()=>EditAsync(p)),Menu("Duplikuj",async()=> { vm.SelectedServer=p; await DuplicateAsync(); }),Menu("Ulubiony",async()=> { vm.SelectedServer=p; await FavoriteAsync(); }),Menu("Przenieś do folderu",async()=> { vm.SelectedServer=p; await MoveAsync(); }),Menu("Usuń",async()=> { vm.SelectedServer=p; await DeleteAsync(); }),Menu("Usuń zapisane hasło",async()=> { vm.SelectedServer=p; await ForgetSecretAsync(); }),Menu("Historia tego serwera",()=>HistoryAsync(p.Id)),NetworkContextMenu(p)};
            }
            context.Opening+=(_,_)=>PopulateContext();
            // Programmatic Open() bypasses Opening in Avalonia; cover it too.
            context.Opened+=(_,_)=>PopulateContext();
            DragSupport.Source(node,ServerDrag,()=>p,DragDropEffects.Move);
            if(p.Folder.Length>0 && section=="connections")Folder(p.Folder).Items.Add(node); else roots.Add(node);
            if(p.Id==selected)node.IsSelected=true;
        }
        tree.ItemsSource=OrderFolders(roots).ToArray(); UpdateBrowserState(profiles.Count()); UpdateWelcome();
    }
    private static Control Row(string icon,string name,bool favorite=false,bool connected=false,string? protocol=null,ServerProfile? profile=null,int? count=null,string? detail=null)
    {
        var row=new Grid { ColumnDefinitions=new ColumnDefinitions(profile==null?"0,20,*,Auto,Auto,Auto":"3,20,*,Auto,Auto,Auto"),ColumnSpacing=DesignTokens.IconGap,MinHeight=24 };
        if(profile!=null)row.Children.Add(new Border { Width=3,Height=18,CornerRadius=new CornerRadius(2),Background=ProfileVisual.ColorBrush(profile.Color) });
        Control glyph=profile==null?DeskIcons.Create(icon,16,"ForegroundSecondary"):new ProfileVisual(profile); Grid.SetColumn(glyph,1); row.Children.Add(glyph); var text=DesignSystem.Label(name); text.FontWeight=profile==null?FontWeight.SemiBold:FontWeight.Normal; text.TextTrimming=TextTrimming.CharacterEllipsis;
        Grid.SetColumn(text,2); row.Children.Add(text);
        if(!string.IsNullOrEmpty(detail))
        {
            row.RowDefinitions=new RowDefinitions("Auto,Auto"); row.RowSpacing=2;
            var secondary=DesignSystem.Label(detail,true,DesignTokens.Micro); secondary.Name="ConnectionSortValue"; secondary.TextTrimming=TextTrimming.CharacterEllipsis;
            Grid.SetRow(secondary,1); Grid.SetColumn(secondary,2); Grid.SetColumnSpan(secondary,4); row.Children.Add(secondary); Grid.SetRowSpan(glyph,2);
            if(profile!=null)Grid.SetRowSpan(row.Children[0],2);
        }
        if(protocol!=null) { var badge=new Border { Child=DesignSystem.Label(protocol,true,DesignTokens.Micro),Padding=new Thickness(5,1),CornerRadius=new CornerRadius(4),VerticalAlignment=VerticalAlignment.Center }; DesignSystem.Resource(badge,Border.BackgroundProperty,"BackgroundTertiary"); Grid.SetColumn(badge,3); row.Children.Add(badge); }
        if(favorite) { var star=DeskIcons.Create("favorite",12); Grid.SetColumn(star,4); row.Children.Add(star); }
        if(connected) { var dot=DeskIcons.Create("circle-check",12,"Success"); Grid.SetColumn(dot,5); row.Children.Add(dot); }
        if(count!=null) { var total=DesignSystem.Label(count.Value.ToString(),true,11); Grid.SetColumn(total,5); row.Children.Add(total); }
        return row;
    }
    private Control TabHeader(SessionPane p)
    {
        var header=new StackPanel { Orientation=Orientation.Horizontal,Spacing=DesignTokens.IconGap };
        var color=new Border { Name="SessionColorMarker",Width=6,Height=6,CornerRadius=new CornerRadius(3),VerticalAlignment=VerticalAlignment.Center }; color.Bind(Border.BackgroundProperty,new Binding(nameof(SessionPane.PresentationProfile)+".Color") { Source=p,Converter=new FuncValueConverter<string,IBrush>(value=>ProfileVisual.ColorBrush(value??"")) }); color.Bind(Visual.IsVisibleProperty,new Binding(nameof(SessionPane.PresentationProfile)+".Color") { Source=p,Converter=new FuncValueConverter<string,bool>(value=>!string.IsNullOrEmpty(value)) }); ToolTip.SetTip(color,L.T("Kolor serwera")); header.Children.Add(color);
        var profileIcon=new ProfileVisual(p.PresentationProfile); profileIcon.Bind(ProfileVisual.ProfileProperty,new Binding(nameof(SessionPane.PresentationProfile)) { Source=p }); header.Children.Add(profileIcon);
        var status=new SessionStatusIcon(); status.Bind(SessionStatusIcon.StateProperty,new Binding(nameof(SessionPane.VisualState)) { Source=p }); header.Children.Add(status);
        var name=new TextBlock { Name="SessionTabName",VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis }; DesignSystem.Resource(name,TextBlock.MaxWidthProperty,"SessionTabMaxWidth"); name.Bind(TextBlock.TextProperty,new Binding(nameof(SessionPane.PresentationProfile)+".Name") { Source=p }); DesignSystem.Resource(name,TextBlock.FontSizeProperty,"FontBody"); header.Children.Add(name);
        header.VerticalAlignment=VerticalAlignment.Center; var close=DeskIcons.Button("close","Zamknij "+p.Profile.Name,()=>_ = Safe(()=>CloseSessionAsync(p)),24); header.Children.Add(close); ToolTip.SetTip(header,$"{p.Profile.Name} · {p.Profile.Host}");
        return header;
    }
    private async Task SelectSectionAsync(string id)
    {
        if(id=="settings") { await SettingsAsync(); return; }
        if(section==id && sidebar.IsVisible) { ToggleSidebar(); return; }
        section=id; if(!sidebar.IsVisible)ToggleSidebar();
        connectionTools.IsVisible=connectionHeaderActions.IsVisible=connectionBrowser.IsVisible=id is not ("transfers" or "network"); transferOverview.IsVisible=id=="transfers"; networkBrowserHost.IsVisible=networkBrowser.IsVisible=id=="network";
        sectionTitle.Text=id switch { "favorite"=>L.T("ULUBIONE"),"recent"=>L.T("OSTATNIE"),"transfers"=>L.T("TRANSFERY"),"network"=>"NETWORK TOOLS",_=>L.T("POŁĄCZENIA") }; UpdateActivity(); QueueTree(); UpdateStatus();
    }
    private void UpdateActivity()
    {
        foreach(var (id,button) in activityButtons)button.Classes.Set("active",id==section);
    }
    private void ToggleSidebar()
    {
        if(sidebar.IsVisible) { sidebarWidth=Math.Clamp(layout.ColumnDefinitions[1].ActualWidth,220,500); sidebar.IsVisible=false; layout.ColumnDefinitions[1].Width=new GridLength(0); layout.ColumnDefinitions[2].Width=new GridLength(0); }
        else { sidebar.IsVisible=true; layout.ColumnDefinitions[1].Width=new GridLength(sidebarWidth); layout.ColumnDefinitions[2].Width=new GridLength(4); }
    }
    private void UpdateStatus()
    {
        var active=ActiveSession;
        metricsBar.Update(active,Bounds.Width,preferences); encoding.IsVisible=!metricsBar.IsVisible && ActivePane is not NetworkToolPane;
        transferStatus.IsVisible=preferences.ShowTransferCount;
        if(workspaceStatus!=null) { workspaceStatus.ColumnDefinitions[0].Width=metricsBar.IsVisible?new GridLength(Math.Clamp(Bounds.Width*.2,150,260)):GridLength.Star; workspaceStatus.ColumnDefinitions[1].Width=metricsBar.IsVisible?GridLength.Star:GridLength.Auto; }
        var detail=active?.ConnectionStatus.TrimStart('●','○',' '); connectionStatus.Text=active==null?vm.Status:$"{active.Profile.Protocol} · {(detail!.Contains(active.Profile.Host)?"":active.Profile.Host+":"+active.Profile.Port+" · ")}{detail.Replace(active.Profile.Protocol+" · ","")}"; ToolTip.SetTip(connectionStatus,connectionStatus.Text);
        if(ActivePane is NetworkToolPane tool) { connectionStatus.Text=tool.DisplayName+" · "+tool.StatusText; ToolTip.SetTip(connectionStatus,connectionStatus.Text); }
        var live=string.Join(",",sessions.Where(s=>s.IsConnected || s.IsRdpLoggedIn).Select(s=>s.Profile.Id)); if(live!=connectedKey) { connectedKey=live; QueueTree(); }
        transferStatus.Text=L.F($"Transfery: {sessions.Sum(s=>s.ActiveTransfers)}"); if(section=="transfers")UpdateTransferOverview();
    }
    private void HandleKeys(object? sender,KeyEventArgs e)
    {
        var ctrl=e.KeyModifiers.HasFlag(KeyModifiers.Control); var shift=e.KeyModifiers.HasFlag(KeyModifiers.Shift); if(!ctrl)return;
        { var origin=e.Source as Visual; while(origin!=null) { if(origin is VncSurface desktop && (!shift || desktop.IsComposingAltGr))return; origin=origin.GetVisualParent(); } }
        if(e.Key==Key.Tab) { CycleSession(shift); e.Handled=true; }
        else if(shift && e.Key==Key.N) { e.Handled=true; _ = Safe(()=>EditAsync(null)); }
        else if(shift && e.Key==Key.P) { e.Handled=true; _ = Safe(CommandsAsync); }
        else if(shift && e.Key==Key.L) { e.Handled=true; _ = Safe(()=>SwitchSessionAsync(sender as Window)); }
        else if(e.Key is Key.B or Key.W)
        {
            // Ctrl+B and Ctrl+W belong to shells/editors whenever the terminal owns keyboard focus.
            var source=e.Source as Visual; while(source!=null) { if(source is TerminalControl or VncSurface && !shift)return; source=source.GetVisualParent(); }
            e.Handled=true; if(e.Key==Key.B)ToggleSidebar(); else if(ActivePane is {} active)_ = Safe(()=>CloseWorkspacePaneAsync(active));
        }
    }
    private async Task HistoryAsync(Guid? profileId=null) { var profile=await Dialogs.History(this,repository,profileId); if(profile!=null)await ConnectAsync(profile); }
    private Task AboutAsync()=>Dialogs.Message(this,L.T("O ServerDesk"),L.T("Publiczna beta · licencja MIT")+"\n\n"+L.F($"ServerDesk {typeof(App).Assembly.GetName().Version?.ToString(3)}\nSSH · SFTP · monitoring · VNC · Serial\nRDP w zakładce na Windows i osobnym oknie na Linux\nSkróty terminala Ctrl+B i Ctrl+W są przekazywane do aplikacji zdalnej. Użyj Ctrl+Shift+B / Ctrl+Shift+W, aby sterować panelem i zakładką z terminala."));
    private async Task CommandsAsync()
    {
        var commands=new Dictionary<string,Func<Task>> { ["Network Tools"]=()=>SelectSectionAsync("network"),[L.T("Nowe połączenie")]=()=>EditAsync(null),[L.T("Szybkie połączenie")]=QuickAsync,[L.T("Otwarte sesje · Ctrl+Shift+L")]=()=>SwitchSessionAsync(),[L.T("Nowy folder")]=FolderAsync,[L.T("Import konfiguracji")]=ImportAsync,[L.T("Eksport konfiguracji")]=ExportAsync,[L.T("Ustawienia wyglądu")]=SettingsAsync,[L.T("Historia połączeń")]=()=>HistoryAsync(),[L.T("Pokaż / ukryj panel boczny")]=()=> { ToggleSidebar(); return Task.CompletedTask; } };
        foreach(var tool in Enum.GetValues<NetworkToolKind>())commands["Network Tools · "+NetworkToolCatalog.Name(tool)]=()=> { OpenNetworkTool(tool); return Task.CompletedTask; };
        foreach(var (label,mode) in new[]{("Jeden panel",WorkspaceMode.Single),("Panele obok siebie",WorkspaceMode.Columns),("Panele jeden nad drugim",WorkspaceMode.Rows),("Trzy panele (1 + 2)",WorkspaceMode.Three),("Cztery panele (2 × 2)",WorkspaceMode.Grid)})commands[L.T(label)]=()=> { SetWorkspaceMode(mode); return Task.CompletedTask; };
        commands[L.T("Wyrównaj wielkość paneli")]=()=> { EqualizeWorkspace(); return Task.CompletedTask; };
        var chosen=await Dialogs.Commands(this,commands.Keys); if(chosen!=null)await commands[chosen]();
    }
}
