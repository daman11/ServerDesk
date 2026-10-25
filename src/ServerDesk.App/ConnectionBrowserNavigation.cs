using Avalonia.Controls;
using Avalonia.Threading;
using ServerDesk.Core;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private IEnumerable<TreeViewItem> BrowserFolders()
    {
        var pending=new Stack<TreeViewItem>(tree.Items.OfType<TreeViewItem>());
        while(pending.TryPop(out var node))
        {
            if(node.Tag is not string)continue;
            yield return node;
            foreach(var child in node.Items.OfType<TreeViewItem>())pending.Push(child);
        }
    }
    private void SetBrowserFoldersExpanded(bool expanded)
    {
        if(section!="connections")return;
        var selectedPath=tree.SelectedItem is TreeViewItem { Tag:ServerProfile p }?p.Folder:
            tree.SelectedItem is TreeViewItem { Tag:string folder }?folder:"";
        foreach(var node in BrowserFolders())node.IsExpanded=expanded;
        // Do not leave keyboard actions targeting an invisible child after collapse.
        if(!expanded && selectedPath.Length>0 && tree.Items.OfType<TreeViewItem>().FirstOrDefault(n=>n.Tag as string==selectedPath.Split('/')[0]) is {} parent)
        { tree.SelectedItem=parent; parent.BringIntoView(); }
        // Existing expansion handlers persist only the ordinary, unfiltered tree.
        // Search expansion/collapse must remain temporary.
    }
    private bool CanRevealConnection(WorkspacePane pane)=>pane is SessionPane session && vm.AllServers.Any(p=>p.Id==session.Profile.Id);
    private async Task RevealConnectionAsync(Guid id)
    {
        var profile=vm.AllServers.FirstOrDefault(p=>p.Id==id); if(profile==null)return;
        if(section!="connections")await SelectSectionAsync("connections");
        if(!sidebar.IsVisible)ToggleSidebar();
        ClearBrowserFilters();
        if(profile.Folder.Length>0)foreach(var ancestor in ProfileFolder.Ancestors(profile.Folder))collapsed.Remove(ancestor);
        vm.SelectedServer=profile; browserReveal=id; QueueTree();
        Dispatcher.UIThread.Post(()=>
        {
            if(!closing && tree.IsEffectivelyVisible && tree.SelectedItem is TreeViewItem { Tag:ServerProfile selected } node && selected.Id==id)
            { node.BringIntoView(); node.Focus(); }
        },DispatcherPriority.Background);
    }
}
