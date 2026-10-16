using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;

namespace ServerDesk.App;
internal sealed partial class SessionPane
{
    private readonly TextBox localFilter=new() { PlaceholderText=L.T("Filtruj pliki…") },remoteFilter=new() { PlaceholderText=L.T("Filtruj pliki…") };
    private readonly ComboBox localSort=new() { ItemsSource=new[]{L.T("Nazwa"),L.T("Rozmiar"),L.T("Modyfikacja"),L.T("Typ")},SelectedIndex=0,MinWidth=112 },remoteSort=new() { ItemsSource=new[]{L.T("Nazwa"),L.T("Rozmiar"),L.T("Modyfikacja"),L.T("Typ")},SelectedIndex=0,MinWidth=112 };
    private bool localDescending,remoteDescending;
    private readonly Dictionary<(bool Remote,FileSort Sort),(Button Button,TextBlock Arrow)> fileSortHeaders=[];
    private readonly Dictionary<bool,Button> fileSortDirections=[];
    private string loadedLocalPath="",loadedRemotePath="";
    private double localShare=.5;
    private GridSplitter fileSplitter=null!;

    private void BuildFileFilters()
    {
        foreach(var remote in new[]{false,true})
        {
            var filter=remote?remoteFilter:localFilter; var sort=remote?remoteSort:localSort; var list=remote?remoteList:localList; var path=remote?remotePath:localPath;
            var prefix=remote?L.T("Zdalny"):L.T("Lokalny");
            AutomationProperties.SetName(filter,prefix+L.T(" filtr plików")); AutomationProperties.SetName(sort,prefix+L.T(" porządek plików")); AutomationProperties.SetName(path,prefix+L.T(" katalog")); AutomationProperties.SetName(list,prefix+L.T(" lista plików"));
            filter.TextChanged+=(_,_)=>FilterFiles(); sort.SelectionChanged+=(_,_)=> { if(remote)remoteDescending=false; else localDescending=false; FilterFiles(); };
            var direction=DeskIcons.Button("arrow-up-down",prefix+L.T(" kierunek sortowania"),()=>
            {
                fileTarget.SelectedIndex=remote?1:0;
                if(remote)remoteDescending=!remoteDescending; else localDescending=!localDescending;
                FilterFiles();
            });
            fileSortDirections[remote]=direction; UpdateFileSortControls();
            var clear=DeskIcons.Button("x",prefix+L.T(" wyczyść filtr"),()=> { fileTarget.SelectedIndex=remote?1:0; filter.Text=""; }); clear.IsEnabled=false;
            filter.TextChanged+=(_,_)=>clear.IsEnabled=!string.IsNullOrEmpty(filter.Text);
            var controls=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto,Auto,Auto"),ColumnSpacing=4,Margin=new Thickness(4,0,4,4),ClipToBounds=true };
            controls.Children.Add(filter); Grid.SetColumn(clear,1); controls.Children.Add(clear); Grid.SetColumn(sort,2); controls.Children.Add(sort); Grid.SetColumn(direction,3); controls.Children.Add(direction);
            Grid.SetRow(controls,2); Grid.SetColumn(controls,remote?2:0); files.Children.Add(controls);
            foreach(var control in new Control[]{list,path,filter,sort,direction,clear})control.GotFocus+=(_,_)=>fileTarget.SelectedIndex=remote?1:0;
            list.AddHandler(PointerPressedEvent,(_,e)=>
            {
                fileTarget.SelectedIndex=remote?1:0;
                if(e.GetCurrentPoint(list).Properties.IsRightButtonPressed && e.Source is Control { DataContext:FileEntry entry })
                { if(list.SelectedItems?.Contains(entry)!=true)list.SelectedItem=entry; e.Handled=true; }
            },RoutingStrategies.Tunnel);
            var menuItems=new List<MenuItem> { OperationMenu(remote?"Pobierz":"Wyślij",remote?DownloadAsync:UploadAsync),LinkFileMenu(remote),OperationMenu("Nowy katalog",MkdirAsync),SingleFileMenu(remote,"Zmień nazwę",RenameAsync),SingleFileMenu(remote,"Przenieś do katalogu",MoveFileAsync),SingleFileMenu(remote,"Szczegóły",FileDetailsAsync),DeleteSelectionMenu(remote) };
            if(remote)menuItems.Insert(2,SingleFileMenu(true,"Utwórz zdalne dowiązanie…",CreateRemoteLinkAsync));
            list.ContextMenu=new ContextMenu { ItemsSource=menuItems };
        }
        files.AddHandler(KeyDownEvent,(_,e)=>
        {
            if(!files.IsVisible)return;
            if(fileRefreshes>0 && e.KeyModifiers==KeyModifiers.Alt && e.Key is Key.Left or Key.Right or Key.Up or Key.Home) { e.Handled=true; return; }
            if(e.Key==Key.F5) { e.Handled=true; _=owner.Safe(RefreshFilesAsync); }
            else if(e.Key==Key.Delete && e.KeyModifiers==KeyModifiers.None && (localList.IsKeyboardFocusWithin || remoteList.IsKeyboardFocusWithin))
            { e.Handled=true; _=owner.Safe(DeleteFileAsync); }
            else if(e.Key==Key.A && e.KeyModifiers==KeyModifiers.Control && (localList.IsKeyboardFocusWithin || remoteList.IsKeyboardFocusWithin))
            {
                var list=remoteList.IsKeyboardFocusWithin?remoteList:localList; list.SelectAll();
                foreach(var parent in list.SelectedItems!.OfType<FileEntry>().Where(f=>f.Name=="..").ToArray())list.SelectedItems!.Remove(parent);
                UpdateFileSelection(); e.Handled=true;
            }
            else if(e.Key==Key.Escape && (localList.IsKeyboardFocusWithin || remoteList.IsKeyboardFocusWithin))
            { (remoteList.IsKeyboardFocusWithin?remoteList:localList).SelectedItems!.Clear(); e.Handled=true; }
            else if(e.Key==Key.L && e.KeyModifiers==KeyModifiers.Control) { var path=RemoteSelected?remotePath:localPath; path.Focus(); path.SelectAll(); e.Handled=true; }
            else if(e.Key==Key.Up && e.KeyModifiers==KeyModifiers.Alt) { e.Handled=true; _=owner.Safe(()=>NavigateParentAsync(RemoteSelected)); }
            else if(e.KeyModifiers==KeyModifiers.Alt && e.Key is Key.Left or Key.Right) { e.Handled=true; _=owner.Safe(()=>NavigateFileHistoryAsync(RemoteSelected,e.Key==Key.Right)); }
            else if(e.Key==Key.Home && e.KeyModifiers==KeyModifiers.Alt) { e.Handled=true; _=owner.Safe(()=>NavigateFileHomeAsync(RemoteSelected)); }
            else if(e.Key==Key.Enter && (localList.IsKeyboardFocusWithin || remoteList.IsKeyboardFocusWithin)) { e.Handled=true; _=owner.Safe(()=>OpenDirectoryAsync(remoteList.IsKeyboardFocusWithin)); }
        },RoutingStrategies.Tunnel);
    }
    private Button FileSortHeader(bool remote,FileSort mode,string caption,bool right=false)
    {
        var text=DesignSystem.Label(caption,true,DesignTokens.Micro); text.TextTrimming=TextTrimming.CharacterEllipsis;
        var arrow=DesignSystem.Label("",true,DesignTokens.Micro); arrow.Width=10;
        var content=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=2,Children={text,arrow} }; Grid.SetColumn(arrow,1);
        var button=new Button { Name=(remote?"Remote":"Local")+"Sort_"+mode,Content=content,Classes={"chrome"},Padding=new Thickness(2,0),MinHeight=0,MinWidth=0,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch };
        if(right)text.TextAlignment=TextAlignment.Right;
        AutomationProperties.SetName(button,(remote?L.T("Zdalny"):L.T("Lokalny"))+" · "+caption);
        button.GotFocus+=(_,_)=>fileTarget.SelectedIndex=remote?1:0;
        button.Click+=(_,_)=>
        {
            fileTarget.SelectedIndex=remote?1:0; var sort=remote?remoteSort:localSort;
            if(sort.SelectedIndex==(int)mode) { if(remote)remoteDescending=!remoteDescending; else localDescending=!localDescending; FilterFiles(); }
            else sort.SelectedIndex=(int)mode;
        };
        fileSortHeaders[(remote,mode)]=(button,arrow); UpdateFileSortControls(); return button;
    }
    private void UpdateFileSortControls()
    {
        foreach(var (remote,button) in fileSortDirections)
        {
            var descending=remote?remoteDescending:localDescending; button.Classes.Set("active",descending);
            ToolTip.SetTip(button,L.T(descending?"Malejąco · kliknij, aby sortować rosnąco":"Rosnąco · kliknij, aby sortować malejąco"));
        }
        foreach(var (key,header) in fileSortHeaders)
        {
            var active=(key.Remote?remoteSort:localSort).SelectedIndex==(int)key.Sort; var descending=key.Remote?remoteDescending:localDescending;
            header.Arrow.Text=active?descending?"↓":"↑":""; header.Button.Classes.Set("active",active);
            ToolTip.SetTip(header.Button,L.T(active && !descending?"Sortuj malejąco":"Sortuj rosnąco"));
        }
    }
    private async Task OpenDirectoryAsync(bool remote)
    {
        var list=remote?remoteList:localList;
        if(list.SelectedItems?.Count!=1 || list.SelectedItem is not FileEntry entry)return;
        if(entry.IsSymbolicLink)
        {
            using var busy=BeginBusy(L.T("Sprawdzanie celu dowiązania…"));
            entry=await ResolveLinkAsync(remote,entry,connectionLifetime.Token);
        }
        if(!entry.IsDirectory)return;
        await NavigateFilesAsync(remote,entry.FullPath);
    }
    private Task NavigateParentAsync(bool remote)
    {
        var directory=FileDirectory(remote);
        var path=remote?RemotePath.Parent(directory):Directory.GetParent(directory)?.FullName;
        if(path==null)return Task.CompletedTask;
        return NavigateFilesAsync(remote,path);
    }
    private void FilterFiles()=>_ = owner.Safe(FilterFilesAsync);
    private void PublishFileViews(IReadOnlyList<FileEntry> local,IReadOnlyList<FileEntry> remote)
    {
        void Apply(ListBox list,IReadOnlyList<FileEntry> result,TextBox filter,TextBlock empty)
        {
            var selected=list.SelectedItems?.OfType<FileEntry>().ToArray()??[];
            var retained=FileSelection.Retain(result,selected,list==localList);
            list.ItemsSource=result; list.SelectedItems!.Clear(); foreach(var entry in retained)list.SelectedItems.Add(entry);
            empty.Text=string.IsNullOrWhiteSpace(filter.Text)?L.T("Brak plików. Włącz „Ukryte”, aby sprawdzić wszystkie wpisy."):L.T("Brak wyników. Zmień lub wyczyść filtr.");
            ToolTip.SetTip(filter,L.F($"Widoczne wpisy: {result.Count(e=>e.Name!="..")}. Filtr dotyczy nazw w bieżącym katalogu."));
        }
        changingFileSelection=true;
        try { Apply(localList,local,localFilter,localEmpty); Apply(remoteList,remote,remoteFilter,remoteEmpty); }
        finally { changingFileSelection=false; UpdateFileSelection(); }
        UpdateFileSortControls(); AdaptFiles();
    }
}
