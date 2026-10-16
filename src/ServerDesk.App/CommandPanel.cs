using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal sealed partial class CommandPanel : Border,IDisposable
{
    private readonly MainWindow owner;
    private readonly CommandStore store;
    private readonly CancellationTokenSource lifetime=new();
    private CommandUserLibrary library=new();
    private CommandUserState state=new();
    private CommandEnvironment environment=new();
    private SessionPane? target;
    private (Guid,Guid,bool) targetKey;
    private long contextRevision;
    private bool updating,ready,libraryLocked,working;
    private Task preferenceSave=Task.CompletedTask;
    internal Task Shutdown=>preferenceSave;
    private readonly TextBlock context=Dialogs.Text("",12),notice=Dialogs.Text("",12),count=DesignSystem.Label("",true,12);
    private readonly ComboBox mode=new() { Name="CommandMode",ItemsSource=new[]{L.T("Automatyczny"),L.T("Ręczny")},SelectedIndex=0,MinWidth=130 };
    private readonly CheckBox pin=new() { Name="CommandPin",Content=L.T("Przypnij profil") };
    private readonly ComboBox system=new() { Name="CommandSystem",ItemsSource=CommandStore.Systems,SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch };
    private readonly ComboBox shell=new() { Name="CommandShell",ItemsSource=Enum.GetValues<CommandShell>(),SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch };
    private readonly TextBox version=new() { Name="CommandVersion",PlaceholderText=L.T("Wersja (opcjonalna)"),MaxLength=40 };
    private readonly Grid manual=new() { ColumnDefinitions=new("*,*"),RowDefinitions=new("Auto,Auto"),ColumnSpacing=6,RowSpacing=5 };
    private readonly Expander contextOptions=new() { Name="CommandContextOptions",Header=L.T("Profil systemu"),IsExpanded=false,HorizontalAlignment=HorizontalAlignment.Stretch };
    private readonly TextBox search=new() { Name="CommandSearch",PlaceholderText=L.T("Szukaj komendy, narzędzia lub problemu…"),MaxLength=200 };
    private readonly ComboBox section=new() { Name="CommandSection",ItemsSource=new[]{"Smart Cheat Sheet",L.T("Moje komendy"),L.T("Ulubione"),L.T("Ostatnie"),L.T("Wyszukiwanie")},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch };
    private readonly ComboBox category=new() { Name="CommandCategory",HorizontalAlignment=HorizontalAlignment.Stretch };
    private readonly ComboBox sort=new() { Name="CommandSort",ItemsSource=new[]{L.T("Trafność"),L.T("Nazwa (A–Z)"),L.T("Nazwa (Z–A)"),L.T("Poziom ryzyka")},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch };
    private readonly ListBox list=new() { Name="CommandList",MinHeight=70 };
    private readonly StackPanel detail=new() { Spacing=8,Margin=new Thickness(0,0,8,0) };
    private readonly TextBox preview=new() { Name="CommandPreview",IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=65,MaxHeight=160,FontFamily=new FontFamily("Cascadia Mono,Consolas,monospace") };
    private readonly TextBlock validation=Dialogs.Text("",12);
    private readonly CheckBox codeApproval=new() { Content=L.T("Zatwierdzam surowy fragment Code"),Name="CommandCodeApproval" };
    private readonly Dictionary<string,Func<string>> values=[];
    private CommandDefinition? selected;
    private CommandDispatchTicket? ticket;
    private readonly Button insert,execute,copy,favorite,edit,duplicate;
    private readonly DispatcherDebounce queryDebounce;
    internal CommandUserLibrary UserLibrary=>library;
    internal CommandEnvironment Environment=>environment;
    internal CommandPanel(MainWindow owner,CommandStore store)
    {
        this.owner=owner; this.store=store; Name="CommandsPanel"; Padding=new Thickness(12,10); BorderThickness=new Thickness(1,0,0,0);
        contextOptions.Classes.Add("network-options"); DesignSystem.Resource(validation,TextBlock.ForegroundProperty,"Danger");
        validation.IsVisible=false; validation.PropertyChanged+=(_,e)=> { if(e.Property==TextBlock.TextProperty)validation.IsVisible=!string.IsNullOrEmpty(validation.Text); };
        DesignSystem.Resource(this,BackgroundProperty,"BackgroundSecondary"); DesignSystem.Resource(this,BorderBrushProperty,"BorderDefault");
        var grid=new Grid { RowDefinitions=new("Auto,Auto,*,1.15*,Auto"),RowSpacing=9,ClipToBounds=true }; Child=grid;
        var header=new Grid { ColumnDefinitions=new("*,Auto,Auto,Auto,Auto,Auto"),ColumnSpacing=3 };
        var heading=DesignSystem.Label(L.T("Komendy"),false,16); heading.FontWeight=FontWeight.SemiBold;
        header.Children.Add(new StackPanel { Orientation=Orientation.Horizontal,Spacing=8,Children={DeskIcons.Create("square-terminal",18,"AccentPrimary"),heading} });
        var options=DeskIcons.Button("more",L.T("Opcje komend"),()=> { }); Grid.SetColumn(options,4); header.Children.Add(options);
        options.ContextMenu=new() { ItemsSource=new[]{Menu("Nowa komenda",()=>EditAsync(null)),Menu("Kategorie komend",CategoriesAsync),Menu("Import komend JSON",ImportAsync),Menu("Eksport komend JSON",ExportAsync),Menu("Ponów odczyt biblioteki",ReloadLibraryAsync),Menu("Historia komend: włącz / wyłącz",ToggleHistoryAsync),Menu("Wyczyść historię komend",ClearHistoryAsync)} }; options.Click+=(_,_)=>options.ContextMenu.Open(options);
        var close=DeskIcons.Button("close",L.T("Zamknij panel komend"),owner.CloseCommandsPanel); Grid.SetColumn(close,5); header.Children.Add(close); grid.Children.Add(header);
        var controls=new StackPanel { Spacing=7 };
        var modeRow=new WrapPanel { Orientation=Orientation.Horizontal }; modeRow.Children.Add(mode); pin.Margin=new Thickness(8,0); modeRow.Children.Add(pin);
        modeRow.Children.Add(DeskIcons.Button("refresh",L.T("Odśwież rozpoznanie systemu"),()=>Run(()=>ResolveContextAsync(true))));
        manual.Children.Add(system); Grid.SetColumn(shell,1); manual.Children.Add(shell); Grid.SetRow(version,1); Grid.SetColumnSpan(version,2); manual.Children.Add(version);
        contextOptions.Content=new StackPanel { Spacing=5,Children={modeRow,manual} }; controls.Children.Add(contextOptions); controls.Children.Add(context);
        search.InnerLeftContent=new Border { Child=DeskIcons.Create("search",15,"ForegroundSecondary"),Margin=new Thickness(8,0,0,0) };
        context.TextWrapping=TextWrapping.NoWrap; context.TextTrimming=TextTrimming.CharacterEllipsis;
        DesignSystem.Resource(context,TextBlock.ForegroundProperty,"ForegroundSecondary");
        system.ItemTemplate=new FuncDataTemplate<string>((id,_)=>id==null?null:DesignSystem.Label(SystemLabel(id)));
        category.ItemTemplate=new FuncDataTemplate<string>((id,_)=>id==null?null:DesignSystem.Label(CategoryLabel(id)));
        var filter=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=6 }; filter.Children.Add(section); var add=DeskIcons.Button("plus",L.T("Nowa komenda"),()=>Run(()=>EditAsync(null))); Grid.SetColumn(add,1); filter.Children.Add(add);
        var filter2=new Grid { ColumnDefinitions=new("*,*"),ColumnSpacing=6 }; filter2.Children.Add(category); Grid.SetColumn(sort,1); filter2.Children.Add(sort);
        var filterBody=new StackPanel { Spacing=9,Children={controls,search,filter,filter2} };
        var filterScroll=new ScrollViewer { Name="CommandFilters",Content=filterBody,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,MaxHeight=300 };
        SizeChanged+=(_,_)=>filterScroll.MaxHeight=Math.Clamp(Bounds.Height*.38,140,330); Grid.SetRow(filterScroll,1); grid.Children.Add(filterScroll);
        var results=new Grid { RowDefinitions=new("Auto,*"),RowSpacing=7,MinHeight=96,ClipToBounds=true }; results.Children.Add(count); Grid.SetRow(list,1); results.Children.Add(list); Grid.SetRow(results,2); grid.Children.Add(results);
        list.Classes.Add("command-list"); list.ItemTemplate=new FuncDataTemplate<CommandMatch>((match,_)=>match==null?null:Row(match)); list.SelectionChanged+=(_,_)=>Select(list.SelectedItem as CommandMatch);
        list.SizeChanged+=(_,_)=>Avalonia.Threading.Dispatcher.UIThread.Post(()=> { if(!lifetime.IsCancellationRequested && list.SelectedItem is {} item)list.ScrollIntoView(item); });
        var detailScroll=new ScrollViewer { Name="CommandDetails",Content=detail,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,MinHeight=85,ClipToBounds=true };
        preview.Classes.Add("code-preview");
        var detailCard=DesignSystem.Card(detailScroll,new Thickness(12)); detailCard.Name="CommandDetailCard"; Grid.SetRow(detailCard,3); grid.Children.Add(detailCard);
        var footer=new StackPanel { Spacing=6 }; footer.Children.Add(notice);
        notice.PropertyChanged+=(_,e)=> { if(e.Property==TextBlock.TextProperty)notice.IsVisible=!string.IsNullOrEmpty(notice.Text); }; notice.IsVisible=false;
        var actions=new Grid { ColumnDefinitions=new("*,*,*"),ColumnSpacing=6 };
        copy=Action("Kopiuj",()=>DispatchAsync("copy")); insert=Action("Wstaw",()=>DispatchAsync("insert")); insert.Classes.Add("primary"); execute=Action("Wykonaj",()=>DispatchAsync("execute"));
        favorite=Action("Ulubione",FavoriteAsync); edit=Action("Edytuj",()=>EditAsync(selected)); duplicate=Action("Duplikuj",DuplicateAsync);
        var buttons=new[]{copy,insert,execute,favorite,edit,duplicate};
        for(var i=0;i<3;i++) { var b=buttons[i]; b.Padding=new Thickness(6,5); b.HorizontalAlignment=HorizontalAlignment.Stretch; b.HorizontalContentAlignment=HorizontalAlignment.Center; Grid.SetColumn(b,i); actions.Children.Add(b); }
        var management=new[]{(favorite,"star","Ulubione"),(edit,"edit","Edytuj"),(duplicate,"copy","Duplikuj")};
        for(var i=0;i<management.Length;i++) { var (b,glyph,label)=management[i]; b.Content=DeskIcons.Create(glyph,16); b.Classes.Add("chrome"); b.Classes.Add("icon"); b.Width=30; b.Height=30; ToolTip.SetTip(b,L.T(label)); Avalonia.Automation.AutomationProperties.SetName(b,L.T(label)); Grid.SetColumn(b,i+1); header.Children.Add(b); }
        footer.Children.Add(actions); var footerSurface=new Border { Child=footer,Padding=new Thickness(0,8,0,0),BorderThickness=new Thickness(0,1,0,0) }; DesignSystem.Resource(footerSurface,BorderBrushProperty,"BorderDefault"); Grid.SetRow(footerSurface,4); grid.Children.Add(footerSurface);
        queryDebounce=new(()=>RefreshList()); search.TextChanged+=(_,_)=> { if(ready)queryDebounce.Request(); };
        section.SelectionChanged+=(_,_)=> { if(ready&&!updating)RefreshList(); }; sort.SelectionChanged+=(_,_)=> { if(ready&&!updating)RefreshList(); };
        category.SelectionChanged+=(_,_)=> { if(ready&&!updating)RefreshList(); };
        mode.SelectionChanged+=(_,_)=>PreferenceChanged(); pin.IsCheckedChanged+=(_,_)=>PreferenceChanged(); system.SelectionChanged+=(_,_)=>PreferenceChanged(); shell.SelectionChanged+=(_,_)=>PreferenceChanged(); version.TextChanged+=(_,_)=>PreferenceChanged();
        codeApproval.IsCheckedChanged+=(_,_)=>UpdatePreview(); SetButtons(false);
    }
    private Button Action(string label,Func<Task> action)=>Dialogs.Button(label,()=>Run(action));
    private MenuItem Menu(string label,Func<Task> action) { var m=new MenuItem { Header=L.T(label) }; m.Click+=(_,_)=>Run(action); return m; }
    private void Run(Func<Task> action)=>_ = RunAsync(action);
    private async Task RunAsync(Func<Task> action)
    {
        if(working||lifetime.IsCancellationRequested)return; working=true; SetButtons(false);
        try { await action(); }
        catch(OperationCanceledException) { }
        catch(Exception ex) { notice.Text=ex is ArgumentException or InvalidDataException or InvalidOperationException?ex.Message:L.T("Operacja komend nie powiodła się. Sprawdź magazyn systemowy i dostęp do pliku."); }
        finally { working=false; UpdatePreview(); }
    }
    internal async Task InitializeAsync()
    {
        var invalidState=false;
        try { state=await store.ReadStateAsync(lifetime.Token); }
        catch(InvalidDataException) { invalidState=true; }
        await ReloadLibraryAsync(); updating=true;
        if(invalidState)notice.Text=L.T("Niepoprawny stan panelu komend. Biblioteka pozostaje zachowana.");
        ApplyPreferences(state.Preferences); updating=false; ready=true; RefreshTarget(true); RefreshList();
    }
    private async Task ReloadLibraryAsync()
    {
        try { library=await store.ReadLibraryAsync(lifetime.Token); libraryLocked=false; notice.Text=""; }
        catch(Exception ex) when(ex is not OperationCanceledException) { libraryLocked=true; notice.Text=L.T("Własna biblioteka jest zablokowana lub uszkodzona. Wbudowana ściągawka pozostaje dostępna."); }
        if(ready)RefreshList();
    }
    private void ApplyPreferences(CommandPanelPreferences p)
    { mode.SelectedIndex=p.Automatic?0:1; pin.IsChecked=p.Pinned; system.SelectedItem=p.System; shell.SelectedItem=p.Shell; version.Text=p.Version; }
    private CommandPanelPreferences ReadPreferences()=>state.Preferences with { Automatic=mode.SelectedIndex==0,Pinned=pin.IsChecked==true,System=system.SelectedItem as string??"general",Version=version.Text??"",Shell=shell.SelectedItem is CommandShell s?s:CommandShell.Any };
    private void PreferenceChanged()
    {
        if(updating||!ready)return; contextRevision++; selected=null; ticket=null; RefreshList();
        if(pin.IsChecked==true && !state.Preferences.Pinned && environment.Detected)
        {
            updating=true;
            system.SelectedItem=CommandStore.Systems.Contains(environment.Distribution)?environment.Distribution:environment.Platform==CommandPlatform.Windows?"windows":"linux";
            version.Text=environment.Version; shell.SelectedItem=environment.Shell; updating=false;
        }
        var p=ReadPreferences(); state=state with { Preferences=p,Hosts=new(state.Hosts) };
        if(target!=null)state.Hosts[target.Profile.Id]=p;
        _=SavePreferences(); _=ResolveContextAsync();
        async Task SavePreferences()
        {
            try { await PersistStateAsync(); }
            catch(OperationCanceledException) { }
            catch { if(!lifetime.IsCancellationRequested)notice.Text=L.T("Nie udało się zapisać ustawień panelu komend."); }
        }
    }
    private Task PersistStateAsync()
    {
        var snapshot=state; var previous=preferenceSave; return preferenceSave=Save();
        async Task Save()
        {
            try { await previous; } catch { }
            await store.SaveStateAsync(snapshot,lifetime.Token);
        }
    }
    internal void RefreshTarget(bool force=false)
    {
        if(!ready||lifetime.IsCancellationRequested)return; var next=owner.CommandTarget; var key=(next?.WorkspaceId??Guid.Empty,next?.CommandConnectionId??Guid.Empty,next?.CanAcceptCommands==true);
        if(!force&&key==targetKey)return; targetKey=key; target=next; contextRevision++; ClearSelection();
        if(pin.IsChecked!=true && next!=null && state.Hosts.TryGetValue(next.Profile.Id,out var host)) { updating=true; ApplyPreferences(host); updating=false; }
        _=ResolveContextAsync();
    }
    internal async Task ResolveContextAsync(bool refresh=false)
    {
        var revision=++contextRevision; var pane=target; var p=ReadPreferences();
        manual.IsVisible=!p.Automatic||p.Pinned||pane?.CanAcceptCommands!=true;
        if(p.Automatic&&!p.Pinned&&pane?.CanAcceptCommands==true)
        {
            context.Text=L.T("Rozpoznawanie systemu przez osobny kanał SSH…");
            try
            {
                var found=await pane.DiscoverCommandsAsync(refresh);
                if(revision!=contextRevision||!ReferenceEquals(target,pane)||lifetime.IsCancellationRequested)return; environment=found;
            }
            catch(OperationCanceledException) { return; }
            catch { if(revision!=contextRevision)return; environment=CommandDetection.Unknown(); }
        }
        else
        {
            environment=CommandEnvironment.Manual(p.System,p.Version,p.Shell);
            if(p.Automatic&&!p.Pinned&&pane?.Profile.Protocol==Protocol.RDP)environment=CommandEnvironment.Manual("windows",shell:CommandShell.PowerShell);
        }
        if(revision!=contextRevision||lifetime.IsCancellationRequested)return;
        var systemName=CommandStore.Systems.Contains(environment.PrettyName)?SystemLabel(environment.PrettyName):environment.PrettyName;
        context.Text=(pane==null?L.T("Tryb offline"):pane.Profile.Name+" · "+pane.Profile.Protocol)+" · "+(systemName.Length>0?systemName:L.T("System nieznany"))+" "+environment.Version+" · "+environment.Shell;
        var contextDetail=context.Text;
        if(environment.Diagnostic.Length>0)contextDetail+="\n"+environment.Diagnostic;
        if(pane?.CanAcceptCommands!=true)contextDetail+="\n"+L.T("Kopiowanie dostępne. Wstawianie i wykonanie wymagają aktywnego terminala SSH.");
        ToolTip.SetTip(context,contextDetail); DesignSystem.Resource(context,TextBlock.ForegroundProperty,environment.Diagnostic.Length>0?"Warning":"ForegroundSecondary");
        RefreshList();
    }
    internal void FocusSearch()=>search.Focus();
    internal void ClearSelection() { list.SelectedItem=null; Select(null); }
    private Control Row(CommandMatch match)
    {
        var c=match.Command; var title=DesignSystem.Label((state.Favorites.Contains(c.Id)?"★ ":"")+c.Title); title.TextWrapping=TextWrapping.Wrap; title.FontWeight=FontWeight.SemiBold;
        var subtitle=DesignSystem.Label(c.Description,true,12); subtitle.TextWrapping=TextWrapping.Wrap;
        var meta=DesignSystem.Label(c.Shell+" · "+Risk(c.Risk)+(!match.Compatible?" · "+L.T("Inny profil / system"):match.MissingTools.Length>0?" · "+L.T("Brak narzędzia"):""),true,11);
        if(c.Risk==CommandRisk.Dangerous)DesignSystem.Resource(meta,TextBlock.ForegroundProperty,"Danger");
        var row=new StackPanel { Spacing=3,Margin=new Thickness(2,5),Children={title} }; if(c.Description!=c.Title)row.Children.Add(subtitle); row.Children.Add(meta); return row;
    }
    internal void RefreshList()
    {
        if(!ready)return; var keep=selected?.Id; var oldCategory=category.SelectedItem as string; updating=true;
        try
        {
            var management=section.SelectedIndex is 1 or 2 or 3;
            var matches=CommandCatalog.Default.Find(environment,library.Commands,search.Text??"",target?.Profile.Id,target?.Profile.Folder??"",allVariants:management,includeIncompatible:management);
            matches=matches.Where(m=>section.SelectedIndex switch { 0=>m.Command.Origin==CommandOrigin.BuiltIn,1=>m.Command.Origin==CommandOrigin.User,2=>state.Favorites.Contains(m.Command.Id),3=>state.Recent.Any(r=>r.CommandId==m.Command.Id),_=>true }).ToArray();
            var all=L.T("Wszystkie kategorie"); var categories=new[]{all}.Concat(library.Categories.Concat(matches.Select(m=>m.Command.Category)).Distinct().Order(StringComparer.CurrentCultureIgnoreCase)).ToArray();
            category.ItemsSource=categories; category.SelectedItem=categories.Contains(oldCategory??"")?oldCategory:all;
            if(category.SelectedItem is string path&&path!=all)matches=matches.Where(m=>m.Command.Category==path||m.Command.Category.StartsWith(path+"/",StringComparison.Ordinal)).ToArray();
            matches=sort.SelectedIndex switch { 1=>matches.OrderBy(m=>m.Command.Title,StringComparer.CurrentCultureIgnoreCase).ToArray(),2=>matches.OrderByDescending(m=>m.Command.Title,StringComparer.CurrentCultureIgnoreCase).ToArray(),3=>matches.OrderBy(m=>m.Command.Risk).ThenBy(m=>m.Command.Title).ToArray(),_=>section.SelectedIndex==3?matches.OrderByDescending(m=>state.Recent.First(r=>r.CommandId==m.Command.Id).UsedAt).ToArray():matches };
            list.ItemsSource=matches; count.Text=L.F($"Komendy: {matches.Count}"); list.SelectedItem=matches.FirstOrDefault(m=>m.Command.Id==keep); Select(list.SelectedItem as CommandMatch);
            if(matches.Count==0)count.Text=L.T("Brak pasujących komend. Zmień system, kategorię lub wyszukiwanie.");
        }
        finally { updating=false; }
    }
    private static string Risk(CommandRisk risk)=>L.T(risk switch { CommandRisk.ReadOnly=>"Tylko odczyt",CommandRisk.Modify=>"Zmienia stan",_=>"Niebezpieczna" });
    private static string CategoryLabel(string value)=>L.T(value switch { "system"=>"Informacje systemowe","network"=>"Sieć","services"=>"Usługi","packages"=>"Pakiety","files"=>"Pliki i uprawnienia","users"=>"Użytkownicy i grupy","storage"=>"Dyski i pamięć masowa","logs"=>"Logi i diagnostyka","security"=>"Bezpieczeństwo","devops"=>"DevOps",_=>value });
    private static string SystemLabel(string value)=>value switch { "general"=>L.T("Ogólne"),"linux"=>"Linux","ubuntu"=>"Ubuntu","debian"=>"Debian","rhel"=>"Red Hat Enterprise Linux","rocky"=>"Rocky Linux","almalinux"=>"AlmaLinux","centos"=>"CentOS Stream","fedora"=>"Fedora","arch"=>"Arch Linux","alpine"=>"Alpine Linux","windows10"=>"Windows 10","windows11"=>"Windows 11","windows-server"=>"Windows Server",_=>"Windows" };
    public void Dispose() { if(lifetime.IsCancellationRequested)return; lifetime.Cancel(); queryDebounce.Dispose(); values.Clear(); preview.Text=""; }
    private sealed class DispatcherDebounce(Action action) : IDisposable
    {
        private readonly Avalonia.Threading.DispatcherTimer timer=Create(action);
        private static Avalonia.Threading.DispatcherTimer Create(Action action) { var t=new Avalonia.Threading.DispatcherTimer { Interval=TimeSpan.FromMilliseconds(160) }; t.Tick+=(_,_)=> { t.Stop(); action(); }; return t; }
        public void Request() { timer.Stop(); timer.Start(); }
        public void Dispose()=>timer.Stop();
    }
}
