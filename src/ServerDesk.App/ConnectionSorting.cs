using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using ServerDesk.Core;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private Button sortDirection=null!;
    private bool restoringProfileOrder;
    private Task profileSortSave=Task.CompletedTask;
    private int profileSortRevision;
    private Control BuildConnectionSorting()
    {
        sortSelector.Name="ConnectionSort"; sortSelector.Margin=new Thickness(0);
        sortSelector.ItemsSource=new[]{L.T("Nazwa"),L.T("Adres"),L.T("Protokół"),L.T("Ostatnie połączenie")};
        ToolTip.SetTip(sortSelector,L.T("Sortowanie w bieżącym widoku. Foldery pozostają na początku."));
        AutomationProperties.SetName(sortSelector,L.T("Sortowanie połączeń"));
        sortDirection=DeskIcons.Button("arrow-up-down","Kierunek sortowania połączeń",()=>vm.SortDescending=!vm.SortDescending,28); sortDirection.Name="ConnectionSortDirection"; sortDirection.Classes.Add("chrome");
        sortSelector.SelectionChanged+=(_,_)=> { if(sortSelector.SelectedIndex>=0)vm.SortMode=(ProfileSort)sortSelector.SelectedIndex; };
        vm.PropertyChanged+=(_,e)=>
        {
            if(e.PropertyName is not (nameof(MainViewModel.SortMode) or nameof(MainViewModel.SortDescending)))return;
            UpdateProfileSortControls(); QueueTree();
            if(!workspaceReady || restoringProfileOrder)return;
            var previous=profileSortSave; var revision=++profileSortRevision; var mode=vm.SortMode; var descending=vm.SortDescending;
            profileSortSave=PersistAsync();
            async Task PersistAsync() { await previous; if(revision==profileSortRevision)await Safe(()=>Task.Run(()=>repository.SaveProfileOrderingAsync(mode,descending,lifetime.Token))); }
        };
        var row=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=4,Margin=new Thickness(10,0,10,8),Children={sortSelector,sortDirection} }; Grid.SetColumn(sortDirection,1); UpdateProfileSortControls(); return row;
    }
    private void UpdateProfileSortControls()
    {
        sortSelector.SelectedIndex=(int)vm.SortMode; sortDirection.Content=DesignSystem.Label(vm.SortDescending?"↓":"↑");
        sortDirection.Classes.Set("active",vm.SortDescending);
        ToolTip.SetTip(sortDirection,L.T(vm.SortDescending?"Malejąco · kliknij, aby sortować rosnąco":"Rosnąco · kliknij, aby sortować malejąco"));
    }
    private async Task RestoreProfileOrderingAsync()
    {
        var order=await repository.ProfileOrderingAsync(lifetime.Token); restoringProfileOrder=true;
        try { vm.SortMode=order.Mode; vm.SortDescending=order.Descending; UpdateProfileSortControls(); }
        finally { restoringProfileOrder=false; }
    }
    private static string SortAddress(ServerProfile profile)=>profile.Protocol!=Protocol.Serial && System.Net.IPAddress.TryParse(profile.Host.Trim('[',']'),out var address)?new System.Net.IPEndPoint(address,profile.Port).ToString():profile.Endpoint;
    private static IEnumerable<TreeViewItem> OrderFolders(IEnumerable<TreeViewItem> nodes)
    {
        var items=nodes.ToArray();
        foreach(var folder in items.Where(node=>node.Tag is string).OrderBy(node=>(string)node.Tag!,NaturalStringComparer.CurrentCultureIgnoreCase).ThenBy(node=>(string)node.Tag!,StringComparer.Ordinal))
        {
            var children=OrderFolders(folder.Items.OfType<TreeViewItem>()).ToArray(); folder.Items.Clear(); foreach(var child in children)folder.Items.Add(child);
            yield return folder;
        }
        foreach(var profile in items.Where(node=>node.Tag is not string))yield return profile;
    }
}
