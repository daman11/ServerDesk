using Avalonia.VisualTree;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Chrome;
using Avalonia.Controls.Templates;
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
    private bool rebuildingTree;
    private bool syncingBrowserSelection;
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
        more.ContextMenu=new ContextMenu { ItemsSource=new[]{Menu("Znajdź połączenie",FindConnectionAsync,shortcut:"Ctrl+Shift+O"),Menu("Szybkie połączenie",QuickAsync),Menu("Nowy folder",FolderAsync),Menu("Import konfiguracji",ImportAsync),Menu("Eksport konfiguracji",ExportAsync),Menu("Paleta akcji",CommandsAsync,shortcut:"Ctrl+Shift+P"),Menu("Ustawienia",SettingsAsync),Menu("Historia połączeń",()=>HistoryAsync()),Menu("Narzędzia sieciowe",()=>SelectSectionAsync("network")),commandsMenu=Menu("Komendy",OpenCommandsPanelAsync,shortcut:"Ctrl+Shift+K"),Menu("Pomoc",HelpAsync,shortcut:"F1"),Menu("O aplikacji",AboutAsync)} };
        more.Click+=(_,_)=>more.ContextMenu.Open(more); titleActions.Children.Add(more); Grid.SetColumn(titleActions,2); titlebar.Children.Add(titleActions);
        BuildWindowCaptions(titlebar);
        var header=BuildWorkspaceHeader(titlebar); Grid.SetColumnSpan(header,4); layout.Children.Add(header);
        var activity=BuildActivityNavigation(); Grid.SetRow(activity,1); layout.Children.Add(activity);
        DesignSystem.Resource(sidebar,DockPanel.BackgroundProperty,"BackgroundSecondary"); Grid.SetRow(sidebar,1); Grid.SetColumn(sidebar,1); layout.Children.Add(sidebar);
        var add=DeskIcons.Button("plus","Dodaj połączenie",()=>_ = Safe(()=>EditAsync(null))); connectionHeaderActions.Children.Add(add);
        var newFolder=DeskIcons.Button("folder","Dodaj folder",()=>_ = Safe(FolderAsync)); connectionHeaderActions.Children.Add(newFolder);
        var sideHeader=BuildSidebarModuleHeader(); DockPanel.SetDock(sideHeader,Dock.Top); sidebar.Children.Add(sideHeader);
        BuildBrowserContextMenu();
        connectionTools.Name="ConnectionBrowserTools"; DockPanel.SetDock(connectionTools,Dock.Top); sidebar.Children.Add(connectionTools);
        var search=connectionSearch=new TextBox { Name="ConnectionSearch",PlaceholderText=L.T("Nazwa, adres, login lub tag"),Margin=new Thickness(10,0,10,8) }; search.Bind(TextBox.TextProperty,new Binding(nameof(vm.Search)) { Mode=BindingMode.TwoWay }); connectionTools.Children.Add(search);
        ToolTip.SetTip(search,L.T("Ctrl+Shift+O: znajdź połączenie. Szukaj po nazwie, adresie, loginie, tagach, opisie, folderze i protokole. ↓ / Enter przenosi do wyników; Escape czyści zapytanie."));
        search.KeyDown+=(_,e)=>
        {
            if(e.KeyModifiers!=KeyModifiers.None)return;
            if(e.Key==Key.Escape && !string.IsNullOrEmpty(search.Text)) { search.Text=""; e.Handled=true; }
            else if(e.Key is Key.Down or Key.Enter) { FocusBrowserResults(); e.Handled=true; }
        };
        search.InnerLeftContent=new Border { Child=DeskIcons.Create("search",14,"ForegroundSecondary"),Margin=new Thickness(8,0,0,0) };
        var clearSearch=DeskIcons.Button("close","Wyczyść wyszukiwanie",()=> { search.Text=""; search.Focus(); },24); clearSearch.IsVisible=false; search.InnerRightContent=clearSearch;
        search.TextChanged+=(_,_)=>clearSearch.IsVisible=!string.IsNullOrEmpty(search.Text); Avalonia.Automation.AutomationProperties.SetName(search,L.T("Szukaj połączeń"));
        BuildBrowserFilters();
        connectionTools.Children.Add(BuildConnectionSorting());
        tree.SelectionChanged+=(_,_)=>
        {
            if(rebuildingTree || tree.SelectedItem is not TreeViewItem node)return;
            // Selecting a folder filters the legacy list, but does not change this
            // tree. Rebuilding here detaches the row before right-button release.
            syncingBrowserSelection=true;
            try { if(node.Tag is ServerProfile p) { vm.SelectedServer=p; servers.SelectedItem=p; } else if(node.Tag is string f) { vm.SelectedServer=null; vm.SelectedFolder=f; } }
            finally { syncingBrowserSelection=false; }
        };
        tree.DoubleTapped+=(_,_)=> { if(tree.SelectedItem is TreeViewItem { Tag:ServerProfile p } && IsBrowserProfileVisible(p))_ = Safe(()=>ConnectAsync(p)); };
        tree.KeyDown+=HandleBrowserKeys;
        tree.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,ScrollBarVisibility.Disabled); connectionBrowser.Children.Add(tree); connectionBrowser.Children.Add(browserEmpty);
        BuildProfileDropTargets();
        BuildTransferOverview(); BuildNetworkBrowser(); sidebar.Children.Add(new Grid { Children={connectionBrowser,transferOverview,networkBrowserHost } });
        vm.Servers.CollectionChanged+=(_,_)=> { if(!syncingBrowserSelection)QueueTree(); };
        var splitter=new GridSplitter { ResizeDirection=GridResizeDirection.Columns,HorizontalAlignment=HorizontalAlignment.Stretch }; Grid.SetColumn(splitter,2); Grid.SetRow(splitter,1); layout.Children.Add(splitter);
        splitter.DragCompleted+=(_,_)=> { if(sidebar.IsVisible)sidebarWidth=layout.ColumnDefinitions[1].ActualWidth; };
        BuildDockWorkspace(); BuildCommandWorkspace(); Grid.SetColumn(commandWorkspace,3); Grid.SetRow(commandWorkspace,1); layout.Children.Add(commandWorkspace);
        var status=new Grid { Name="WorkspaceStatusBar",ColumnDefinitions=new ColumnDefinitions("180,*,Auto,Auto"),Margin=new Thickness(10,0),ColumnSpacing=10,ClipToBounds=true }; workspaceStatus=status;
        var statusRule=new Border { Height=1,VerticalAlignment=VerticalAlignment.Top,IsHitTestVisible=false,Margin=new Thickness(-10,0) }; DesignSystem.Resource(statusRule,Border.BackgroundProperty,"BorderDefault"); Grid.SetColumnSpan(statusRule,4); status.Children.Add(statusRule);
        status.Children.Add(connectionStatus); Grid.SetColumn(metricsBar,1); status.Children.Add(metricsBar); Grid.SetColumn(transferStatus,2); status.Children.Add(transferStatus); Grid.SetColumn(encoding,3); status.Children.Add(encoding);
        connectionStatus.TextTrimming=TextTrimming.CharacterEllipsis;
        Grid.SetRow(status,2); Grid.SetColumnSpan(status,4); layout.Children.Add(status); Content=layout;
        AddHandler(KeyDownEvent,HandleKeys,RoutingStrategies.Tunnel); SizeChanged+=(_,_)=> { ResizeCommandWorkspace(); UpdateStatus(); }; statusTimer.Tick+=(_,_)=>UpdateStatus(); statusTimer.Start(); UpdateActivity(); UpdateStatus();
    }
    private void QueueTree()
    {
        if(section is "connections" or "favorite" or "recent" && vm.SelectedServer is {} selected && !IsBrowserProfileVisible(selected))
        { vm.SelectedServer=null; servers.SelectedItem=null; }
        if(treePending)return; treePending=true; Dispatcher.UIThread.Post(()=> { treePending=false; BuildTree(); },DispatcherPriority.Background);
    }
    private void BuildTree()
    {
        var selected=vm.SelectedServer?.Id; var selectedFolder=selected==null && tree.SelectedItem is not TreeViewItem { Tag:ServerProfile } && RealFolder()?vm.SelectedFolder:null;
        var searching=HasBrowserFilter; TreeViewItem? selection=null;
        var profiles=vm.AllServers.Where(vm.CompileBrowserFilter());
        if(section=="favorite")profiles=profiles.Where(p=>p.Favorite);
        if(section=="recent")profiles=profiles.Where(p=>p.LastConnected!=null);
        profiles=ProfileOrdering.Sort(profiles,vm.SortMode,vm.SortDescending).ToArray();
        if(selected!=null && !profiles.Any(p=>p.Id==selected)) { selected=null; vm.SelectedServer=null; servers.SelectedItem=null; }
        var connectedIds=sessions.Where(s=>s.IsConnected || s.IsRdpLoggedIn).Select(s=>s.Profile.Id).ToHashSet();
        var sortMode=vm.SortMode; var badges=preferences.ShowProtocolBadges;
        // Tree items keep their identity and drag handlers; construct expensive row
        // visuals only when Avalonia presents them, rather than inside closed folders.
        var profileTemplate=new FuncDataTemplate<ServerProfile>((p,_)=>
        {
            if(p==null)return null;
            var detail=sortMode==ProfileSort.Host?SortAddress(p):sortMode==ProfileSort.LastConnected?p.LastConnected?.ToLocalTime().ToString("g")??"—":null;
            if(preferences.ShowConnectionAddresses && sortMode!=ProfileSort.Host)detail=detail==null?SortAddress(p):SortAddress(p)+" · "+detail;
            var row=Row(ProfileIcons.Resolve(p),p.Name,p.Favorite,connectedIds.Contains(p.Id),badges?p.Protocol.ToString():null,p,detail:detail);
            return WithConnectionTags(row,p);
        });
        var counts=new Dictionary<string,int>(); foreach(var item in profiles) { var path=item.Folder; while(path.Length>0) { counts[path]=counts.GetValueOrDefault(path)+1; var split=path.LastIndexOf('/'); path=split<0?"":path[..split]; } }
        var roots=new List<TreeViewItem>(); var folders=new Dictionary<string,TreeViewItem>();
        TreeViewItem Folder(string path)
        {
            if(folders.TryGetValue(path,out var found))return found;
            var slash=path.LastIndexOf('/'); var node=new TreeViewItem { Tag=path,IsExpanded=searching || !collapsed.Contains(path),Classes={"connection-entry","connection-folder"} };
            // Search expansion is temporary, including clicks on search results.
            // Detached/rebuilt search nodes must never rewrite saved collapse state.
            node.PropertyChanged+=(_,e)=> { if(e.Property==TreeViewItem.IsExpandedProperty && !rebuildingTree && !searching && !HasBrowserFilter) { if(node.IsExpanded)collapsed.Remove(path); else collapsed.Add(path); } };
            if(path==selectedFolder)selection=node;
            node.ContextMenu=DeferredBrowserMenu(()=>new[]{Menu("Nowe połączenie w folderze",()=>CreateProfileAsync(new ServerProfile { Folder=path })),Menu("Zmień ikonę folderu",()=>EditFolderIconAsync(path)),Menu("Zmień nazwę",async()=> { vm.SelectedFolder=path; await RenameFolderAsync(); },shortcut:"F2"),Menu("Usuń pusty folder",async()=> { vm.SelectedFolder=path; await DeleteFolderAsync(); },shortcut:"Delete")});
            ConfigureProfileDrop(node,path);
            folders[path]=node; if(slash<0)roots.Add(node); else Folder(path[..slash]).Items.Add(node); return node;
        }
        if(section=="connections" && !HasBrowserFilter)foreach(var path in vm.Folders.Where(f=>f is not ("Wszystkie" or "★ Ulubione" or "◷ Ostatnie")))Folder(path);
        foreach(var p in profiles)
        {
            var node=new TreeViewItem { Header=p,HeaderTemplate=profileTemplate,Tag=p,Classes={"connection-entry"} }; ToolTip.SetTip(node,$"{p.Protocol} · {(p.Protocol==Protocol.SSH?p.Username+"@":"")}{p.Endpoint}\n{p.Description}"+(string.IsNullOrWhiteSpace(p.Tags)?"":"\n"+L.T("Tagi")+": "+ProfileTags.Format(ProfileTags.Parse(p.Tags))));
            Avalonia.Automation.AutomationProperties.SetName(node,p.Name);
            node.ContextMenu=DeferredBrowserMenu(()=>new[]{Menu("Połącz",()=>ConnectAsync(p)),Menu("Edytuj",()=>EditAsync(p),shortcut:"F2"),Menu("Duplikuj",async()=> { vm.SelectedServer=p; await DuplicateAsync(); },shortcut:"Ctrl+D"),Menu("Ulubiony",async()=> { vm.SelectedServer=p; await FavoriteAsync(); }),Menu("Przenieś do folderu",async()=> { vm.SelectedServer=p; await MoveAsync(); }),Menu("Usuń",async()=> { vm.SelectedServer=p; await DeleteAsync(); },shortcut:"Delete"),Menu("Usuń zapisane hasło",async()=> { vm.SelectedServer=p; await ForgetSecretAsync(); }),Menu("Historia tego serwera",()=>HistoryAsync(p.Id)),NetworkContextMenu(p)});
            DragSupport.Source(node,ServerDrag,()=>p,DragDropEffects.Move);
            if(p.Folder.Length>0 && section=="connections")Folder(p.Folder).Items.Add(node); else roots.Add(node);
            if(p.Id==selected)selection=node;
        }
        foreach(var (path,node) in folders)node.Header=FolderHeader(node,path,FolderIcons.Resolve(vm.FolderIcons.GetValueOrDefault(path)),counts.GetValueOrDefault(path));
        rebuildingTree=true;
        try { tree.ItemsSource=OrderFolders(roots).ToArray(); tree.SelectedItem=selection; }
        finally { rebuildingTree=false; }
        if(selection!=null && Equals(selection.Tag is ServerProfile placedProfile?placedProfile.Id:selection.Tag,browserReveal))
        {
            // Wait for rows to be laid out; scrolling during ItemsSource replacement
            // can leave the old offset, with the moved item outside the viewport.
            var reveal=selection;
            Dispatcher.UIThread.Post(()=> { if(ReferenceEquals(tree.SelectedItem,reveal) && reveal.IsAttachedToVisualTree())reveal.BringIntoView(); },DispatcherPriority.Loaded);
        }
        browserReveal=null;
        UpdateBrowserState(profiles.Count()); UpdateWelcome();
    }
    private static Control Row(string icon,string name,bool favorite=false,bool connected=false,string? protocol=null,ServerProfile? profile=null,string? detail=null)
    {
        var row=new Grid { ColumnDefinitions=new ColumnDefinitions(profile==null?"0,36,*,Auto,Auto":"3,36,*,Auto,Auto"),ColumnSpacing=DesignTokens.IconGap,MinHeight=24 };
        if(profile!=null)row.Children.Add(new Border { Width=3,Height=18,CornerRadius=new CornerRadius(2),Background=ProfileVisual.ColorBrush(profile.Color) });
        // Keep the status slot beside the server icon even while disconnected.
        // The protocol stays at the right edge, independent of status/favorites.
        var glyph=new Grid { ColumnDefinitions=new ColumnDefinitions("20,12"),ColumnSpacing=4,VerticalAlignment=VerticalAlignment.Center };
        glyph.Children.Add(profile==null?DeskIcons.Create(icon,16,"ForegroundSecondary"):new ProfileVisual(profile));
        if(connected) { var dot=DeskIcons.Create("circle-check",12,"Success"); Grid.SetColumn(dot,1); glyph.Children.Add(dot); }
        Grid.SetColumn(glyph,1); row.Children.Add(glyph); var text=DesignSystem.Label(name); text.FontWeight=profile==null?FontWeight.SemiBold:FontWeight.Normal; text.TextTrimming=TextTrimming.CharacterEllipsis;
        Grid.SetColumn(text,2); row.Children.Add(text);
        if(!string.IsNullOrEmpty(detail))
        {
            row.RowDefinitions=new RowDefinitions("Auto,Auto"); row.RowSpacing=2;
            var secondary=DesignSystem.Label(detail,true,DesignTokens.Micro); secondary.Name="ConnectionSortValue"; secondary.TextTrimming=TextTrimming.CharacterEllipsis;
            Grid.SetRow(secondary,1); Grid.SetColumn(secondary,2); Grid.SetColumnSpan(secondary,3); row.Children.Add(secondary); Grid.SetRowSpan(glyph,2);
            if(profile!=null)Grid.SetRowSpan(row.Children[0],2);
        }
        if(favorite) { var star=DeskIcons.Create("favorite",12); Grid.SetColumn(star,3); row.Children.Add(star); }
        if(protocol!=null) { var badge=DesignSystem.Label(protocol,true,DesignTokens.Micro); badge.Margin=new Thickness(3,0); Grid.SetColumn(badge,4); row.Children.Add(badge); }
        return row;
    }
    private Control TabHeader(SessionPane p)
    {
        var header=new StackPanel { Orientation=Orientation.Horizontal,Spacing=DesignTokens.IconGap };
        var color=new Border { Name="SessionColorMarker",Width=6,Height=6,CornerRadius=new CornerRadius(3),VerticalAlignment=VerticalAlignment.Center }; color.Bind(Border.BackgroundProperty,new Binding(nameof(SessionPane.PresentationProfile)+".Color") { Source=p,Converter=new FuncValueConverter<string,IBrush>(value=>ProfileVisual.ColorBrush(value??"")) }); color.Bind(Visual.IsVisibleProperty,new Binding(nameof(SessionPane.PresentationProfile)+".Color") { Source=p,Converter=new FuncValueConverter<string,bool>(value=>!string.IsNullOrEmpty(value)) }); ToolTip.SetTip(color,L.T("Kolor serwera")); header.Children.Add(color);
        var profileIcon=new ProfileVisual(p.PresentationProfile) { Name="WorkspaceTabIcon" }; profileIcon.Bind(ProfileVisual.ProfileProperty,new Binding(nameof(SessionPane.PresentationProfile)) { Source=p }); DesignSystem.Resource(profileIcon,Visual.IsVisibleProperty,"ShowTabIcons"); header.Children.Add(profileIcon);
        var status=new SessionStatusIcon(); status.Bind(SessionStatusIcon.StateProperty,new Binding(nameof(SessionPane.VisualState)) { Source=p }); header.Children.Add(status);
        var name=new TextBlock { Name="SessionTabName",VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis }; DesignSystem.Resource(name,TextBlock.MaxWidthProperty,"SessionTabMaxWidth"); name.Bind(TextBlock.TextProperty,new Binding(nameof(SessionPane.PresentationProfile)+".Name") { Source=p }); DesignSystem.Resource(name,TextBlock.FontSizeProperty,"FontBody"); header.Children.Add(name);
        header.VerticalAlignment=VerticalAlignment.Center; var close=DeskIcons.Button("close","Zamknij "+p.Profile.Name,()=>_ = Safe(()=>CloseSessionAsync(p)),24); close.Name="WorkspaceTabClose"; DesignSystem.Resource(close,Visual.IsVisibleProperty,"ShowTabCloseButtons"); header.Children.Add(close); ToolTip.SetTip(header,$"{p.Profile.Name} · {p.Profile.Host}");
        return header;
    }
    private async Task SelectSectionAsync(string id)
    {
        if(id=="commands") { if(commandPanelHost.IsVisible)CloseCommandsPanel(); else await OpenCommandsPanelAsync(); return; }
        if(id=="settings") { await SettingsAsync(); return; }
        if(id=="help") { await HelpAsync(); return; }
        if(section==id && sidebar.IsVisible) { ToggleSidebar(); return; }
        section=id; if(!sidebar.IsVisible)ToggleSidebar();
        connectionTools.IsVisible=connectionHeaderActions.IsVisible=connectionBrowser.IsVisible=id is not ("transfers" or "network"); transferOverview.IsVisible=id=="transfers"; networkBrowserHost.IsVisible=networkBrowser.IsVisible=id=="network";
        UpdateSidebarModule(); UpdateActivity(); QueueTree(); UpdateStatus();
    }
    private void UpdateActivity()
    {
        foreach(var (id,button) in activityButtons)button.Classes.Set("active",id=="commands"?commandPanelHost.IsVisible:id=="help"?ActivePane is HelpPane:id==section);
        if(activityPluginGroup!=null)activityPluginGroup.IsVisible=activityButtons["commands"].IsVisible;
    }
    private void ToggleSidebar()
    {
        if(sidebar.IsVisible) { sidebarWidth=Math.Clamp(layout.ColumnDefinitions[1].ActualWidth,220,500); sidebar.IsVisible=false; layout.ColumnDefinitions[1].Width=new GridLength(0); layout.ColumnDefinitions[2].Width=new GridLength(0); }
        else { sidebar.IsVisible=true; layout.ColumnDefinitions[1].Width=new GridLength(sidebarWidth); layout.ColumnDefinitions[2].Width=new GridLength(4); }
    }
    private void UpdateStatus()
    {
        UpdateCommandTarget();
        var active=ActiveSession;
        metricsBar.Update(active,Bounds.Width,IsPluginEnabled(PluginCatalog.LinuxMonitoring)?preferences:preferences with { ShowMetrics=false }); encoding.IsVisible=!metricsBar.IsVisible && ActivePane is not (NetworkToolPane or HelpPane);
        transferStatus.IsVisible=preferences.ShowTransferCount;
        if(workspaceStatus!=null) { workspaceStatus.ColumnDefinitions[0].Width=metricsBar.IsVisible?new GridLength(Math.Clamp(Bounds.Width*.2,150,260)):GridLength.Star; workspaceStatus.ColumnDefinitions[1].Width=metricsBar.IsVisible?GridLength.Star:GridLength.Auto; }
        var detail=active?.ConnectionStatus.TrimStart('●','○',' '); connectionStatus.Text=active==null?vm.Status:$"{active.Profile.Protocol} · {(detail!.Contains(active.Profile.Host)?"":active.Profile.Host+":"+active.Profile.Port+" · ")}{detail.Replace(active.Profile.Protocol+" · ","")}"; ToolTip.SetTip(connectionStatus,connectionStatus.Text);
        if(ActivePane is NetworkToolPane tool) { connectionStatus.Text=tool.DisplayName+" · "+tool.StatusText; ToolTip.SetTip(connectionStatus,connectionStatus.Text); }
        if(ActivePane is HelpPane) { connectionStatus.Text=L.T("Pomoc")+" · "+L.T("Dokumentacja offline"); ToolTip.SetTip(connectionStatus,connectionStatus.Text); }
        var live=string.Join(",",sessions.Where(s=>s.IsConnected || s.IsRdpLoggedIn).Select(s=>s.Profile.Id)); if(live!=connectedKey) { connectedKey=live; QueueTree(); }
        transferStatus.Text=L.F($"Transfery: {sessions.Sum(s=>s.ActiveTransfers)}"); if(section=="transfers")UpdateTransferOverview();
    }
    private void HandleKeys(object? sender,KeyEventArgs e)
    {
        if(e.Key==Key.F1 && e.KeyModifiers==KeyModifiers.None)
        {
            for(var origin=e.Source as Visual;origin!=null;origin=origin.GetVisualParent())if(origin is TerminalControl or VncSurface or NativeControlHost)return;
            e.Handled=true; _=Safe(HelpAsync); return;
        }
        var ctrl=e.KeyModifiers.HasFlag(KeyModifiers.Control); var shift=e.KeyModifiers.HasFlag(KeyModifiers.Shift); if(!ctrl)return;
        { var origin=e.Source as Visual; while(origin!=null) { if(origin is VncSurface desktop && (!shift || desktop.IsComposingAltGr))return; origin=origin.GetVisualParent(); } }
        if(e.Key==Key.Tab) { CycleSession(shift); e.Handled=true; }
        else if(shift && e.Key==Key.N) { e.Handled=true; _ = Safe(()=>EditAsync(null)); }
        else if(shift && e.Key==Key.P) { e.Handled=true; _ = Safe(CommandsAsync); }
        else if(shift && e.Key==Key.K) { e.Handled=true; _ = Safe(OpenCommandsPanelAsync); }
        else if(shift && e.Key==Key.L) { e.Handled=true; _ = Safe(()=>SwitchSessionAsync(sender as Window)); }
        else if(shift && e.Key==Key.O) { e.Handled=true; _ = Safe(FindConnectionAsync); }
        else if(e.Key is Key.B or Key.W)
        {
            // Ctrl+B and Ctrl+W belong to shells/editors whenever the terminal owns keyboard focus.
            var source=e.Source as Visual; while(source!=null) { if(source is TerminalControl or VncSurface && !shift)return; source=source.GetVisualParent(); }
            e.Handled=true; if(e.Key==Key.B)ToggleSidebar(); else if(ActivePane is {} active)_ = Safe(()=>CloseWorkspacePaneAsync(active));
        }
    }
    private async Task HistoryAsync(Guid? profileId=null) { var profile=await Dialogs.History(this,repository,profileId); if(profile!=null)await ConnectAsync(profile); }
    private Task AboutAsync()=>Dialogs.About(this);
    private async Task CommandsAsync()
    {
        var commands=new Dictionary<string,Func<Task>>(); var icons=new Dictionary<string,string?>();
        void AddAction(string source,Func<Task> run,string? glyph=null) { var label=L.T(source); commands[label]=run; icons[label]=glyph??DeskIcons.ForAction(source); }
        AddAction("Narzędzia sieciowe",()=>SelectSectionAsync("network")); AddAction("Nowe połączenie",()=>EditAsync(null)); AddAction("Szybkie połączenie",QuickAsync); AddAction("Znajdź połączenie · Ctrl+Shift+O",FindConnectionAsync);
        AddAction("Otwarte sesje · Ctrl+Shift+L",()=>SwitchSessionAsync()); AddAction("Nowy folder",FolderAsync); AddAction("Import konfiguracji",ImportAsync); AddAction("Eksport konfiguracji",ExportAsync);
        AddAction("Ustawienia",SettingsAsync); AddAction("Historia połączeń",()=>HistoryAsync()); AddAction("Pokaż / ukryj panel połączeń",()=> { ToggleSidebar(); return Task.CompletedTask; });
        AddAction("Pomoc",HelpAsync);
        if(IsPluginEnabled(PluginCatalog.Commands))AddAction("Komendy · Ctrl+Shift+K",OpenCommandsPanelAsync);
        foreach(var tool in Enum.GetValues<NetworkToolKind>())AddAction(L.T("Narzędzia sieciowe")+" · "+NetworkToolCatalog.Name(tool),()=> { OpenNetworkTool(tool); return Task.CompletedTask; },NetworkToolCatalog.Icon(tool));
        foreach(var (label,mode) in new[]{("Jeden panel",WorkspaceMode.Single),("Panele obok siebie",WorkspaceMode.Columns),("Panele jeden nad drugim",WorkspaceMode.Rows),("Trzy panele (1 + 2)",WorkspaceMode.Three),("Cztery panele (2 × 2)",WorkspaceMode.Grid)})AddAction(label,()=> { SetWorkspaceMode(mode); return Task.CompletedTask; });
        AddAction("Wyrównaj wielkość paneli",()=> { EqualizeWorkspace(); return Task.CompletedTask; });
        var chosen=await Dialogs.Commands(this,commands.Keys,label=>icons.GetValueOrDefault(label)); if(chosen!=null)await commands[chosen]();
    }
}
