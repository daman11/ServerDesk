using CommunityToolkit.Mvvm.ComponentModel;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.Collections.ObjectModel;

namespace ServerDesk.App;
public sealed partial class MainViewModel(ProfileRepository repository) : ObservableObject
{
    public BatchObservableCollection<ServerProfile> Servers { get; } = [];
    public ObservableCollection<string> Folders { get; } = [];
    private List<ServerProfile> all = [];
    internal IReadOnlyList<ServerProfile> AllServers => all;
    internal IReadOnlyDictionary<string,string> FolderIcons { get; private set; }=new Dictionary<string,string>();
    [ObservableProperty] private ProfileSort sortMode=ProfileSort.Name;
    [ObservableProperty] private bool sortDescending;
    [ObservableProperty] private Protocol? protocolFilter;
    partial void OnProtocolFilterChanged(Protocol? value)=>Filter();
    partial void OnSortModeChanged(ProfileSort value) { SortDescending=value==ProfileSort.LastConnected; Filter(); }
    partial void OnSortDescendingChanged(bool value)=>Filter();
    [ObservableProperty] private string search = "";
    [ObservableProperty] private string status = L.T("Gotowy");
    [ObservableProperty] private ServerProfile? selectedServer;
    [ObservableProperty] private string selectedFolder = "Wszystkie";
    partial void OnSearchChanged(string value) => Filter();
    partial void OnSelectedFolderChanged(string value) => Filter();
    public async Task LoadAsync()
    {
        var folderSelection=SelectedFolder??"Wszystkie";
        await repository.InitializeAsync(); all=await repository.ListAsync(); FolderIcons=await repository.FolderIconsAsync(); var folders=await repository.FoldersAsync(); Folders.Clear(); Folders.Add("Wszystkie"); Folders.Add("★ Ulubione"); Folders.Add("◷ Ostatnie"); foreach(var folder in folders) Folders.Add(folder);
        SelectedFolder=""; SelectedFolder=Folders.Contains(folderSelection)?folderSelection:"Wszystkie";
        if(SelectedServer is {} selected)SelectedServer=all.FirstOrDefault(p=>p.Id==selected.Id);
        Filter();
    }
    private void Filter()
    {
        var matches=ProfileSearch.Compile(Search);
        var query=all.Where(x=>matches(x) && (ProtocolFilter==null || x.Protocol==ProtocolFilter));
        if (SelectedFolder == "★ Ulubione") query=query.Where(x=>x.Favorite);
        else if (SelectedFolder == "◷ Ostatnie") query=query.Where(x=>x.LastConnected!=null);
        else if (!string.IsNullOrEmpty(SelectedFolder) && SelectedFolder != "Wszystkie") query=query.Where(x=>x.Folder==SelectedFolder || x.Folder.StartsWith(SelectedFolder+"/",StringComparison.Ordinal));
        query=ProfileOrdering.Sort(query,SortMode,SortDescending);
        Servers.ReplaceWith(query);
    }
}


