using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ServerDesk.Core;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private Border rootProfileDrop=null!;
    private object? browserReveal;
    private async Task FindConnectionAsync()
    {
        if(section is not ("connections" or "favorite" or "recent"))await SelectSectionAsync("connections");
        else if(!sidebar.IsVisible)ToggleSidebar();
        Activate(); connectionSearch?.Focus(); connectionSearch?.SelectAll();
    }
    private static IEnumerable<TreeViewItem> VisibleBrowserNodes(IEnumerable<object?> items)
    {
        foreach(var node in items.OfType<TreeViewItem>())
        {
            yield return node;
            if(node.IsExpanded)foreach(var child in VisibleBrowserNodes(node.Items))yield return child;
        }
    }
    private void FocusBrowserResults()
    {
        // Text binding and the tree refresh may still be queued when Enter arrives.
        vm.Search=connectionSearch?.Text??""; QueueTree();
        Dispatcher.UIThread.Post(()=>
        {
            if(!tree.IsEffectivelyVisible || connectionSearch?.IsKeyboardFocusWithin!=true)return;
            var visible=VisibleBrowserNodes(tree.Items).ToArray();
            var target=visible.FirstOrDefault(n=>n.Tag is ServerProfile p && p.Id==vm.SelectedServer?.Id && IsBrowserProfileVisible(p))
                ??visible.FirstOrDefault(n=>n.Tag is ServerProfile p && IsBrowserProfileVisible(p))??visible.FirstOrDefault();
            if(target==null)return;
            tree.SelectedItem=target; target.BringIntoView(); target.Focus();
        },DispatcherPriority.Background);
    }
    private static bool InFolder(string candidate,string folder)=>candidate==folder || candidate.StartsWith(folder+"/",StringComparison.Ordinal);
    private string BrowserCreationFolder()
    {
        if(section is not ("connections" or "favorite" or "recent"))return "";
        var folder=tree.SelectedItem is TreeViewItem { Tag:ServerProfile p } && IsBrowserProfileVisible(p)?p.Folder:
            tree.SelectedItem is TreeViewItem { Tag:string path }?path:"";
        return vm.Folders.Contains(folder)?folder:"";
    }
    private async Task RenameFolderAsync()
    {
        if(!RealFolder())return;
        var path=vm.SelectedFolder;
        var target=await Dialogs.Prompt(this,L.T("Nowa ścieżka folderu"),path); if(target==null)return;
        await repository.RenameFolderAsync(path,target,lifetime.Token);
        // Change view state only after the complete repository operation succeeds.
        var renamed=collapsed.Where(folder=>InFolder(folder,path)).ToArray();
        foreach(var folder in renamed)collapsed.Remove(folder);
        foreach(var folder in renamed)collapsed.Add(target+folder[path.Length..]);
        if(InFolder(vm.SelectedFolder,path))vm.SelectedFolder=target+vm.SelectedFolder[path.Length..];
        await vm.LoadAsync();
        browserReveal=vm.SelectedServer?.Id??(object)vm.SelectedFolder; QueueTree();
    }
    private async Task DeleteFolderAsync()
    {
        if(!RealFolder())return;
        var path=vm.SelectedFolder;
        if(!await Dialogs.Confirm(this,L.T("Usunąć pusty folder i podfoldery?"),path))return;
        await repository.DeleteFolderAsync(path,lifetime.Token);
        collapsed.RemoveWhere(folder=>InFolder(folder,path));
        if(InFolder(vm.SelectedFolder,path))vm.SelectedFolder="Wszystkie";
        await vm.LoadAsync();
    }
    private void HandleBrowserKeys(object? sender,KeyEventArgs e)
    {
        if(tree.IsEffectivelyVisible && (section is "connections" or "favorite" or "recent") && e.KeyModifiers==KeyModifiers.None &&
            (e.Key==Key.Escape || e.Key==Key.Up && ReferenceEquals(tree.SelectedItem,VisibleBrowserNodes(tree.Items).FirstOrDefault())))
        { connectionSearch?.Focus(); e.Handled=true; return; }
        if(!tree.IsEffectivelyVisible || section is not ("connections" or "favorite" or "recent") || tree.SelectedItem is not TreeViewItem node)return;
        if(node.Tag is ServerProfile p && IsBrowserProfileVisible(p))
        {
            Func<Task>? action=(e.Key,e.KeyModifiers) switch
            {
                (Key.Enter,KeyModifiers.None)=>()=>ConnectAsync(p),
                (Key.F2,KeyModifiers.None)=>()=>EditAsync(p),
                (Key.Delete,KeyModifiers.None)=>DeleteAsync,
                (Key.D,KeyModifiers.Control)=>DuplicateAsync,
                _=>null
            };
            if(action==null)return;
            vm.SelectedServer=p; e.Handled=true; _=Safe(action);
        }
        else if(node.Tag is string folder && vm.Folders.Contains(folder) && e.KeyModifiers==KeyModifiers.None)
        {
            if(e.Key==Key.Enter) { node.IsExpanded=!node.IsExpanded; e.Handled=true; }
            else if(e.Key is Key.F2 or Key.Delete)
            { vm.SelectedServer=null; vm.SelectedFolder=folder; e.Handled=true; _=Safe(e.Key==Key.F2?RenameFolderAsync:DeleteFolderAsync); }
        }
    }
    private void BuildProfileDropTargets()
    {
        var label=DesignSystem.Label(L.T("Katalog główny"),true,12); label.TextWrapping=TextWrapping.Wrap;
        rootProfileDrop=new Border { Name="ConnectionRootDrop",Padding=new Thickness(8,6),Margin=new Thickness(10,4,10,10),CornerRadius=new CornerRadius(6),BorderThickness=new Thickness(1),
            Child=new StackPanel { Orientation=Orientation.Horizontal,Spacing=8,Children={DeskIcons.Create("folder",16,"ForegroundSecondary"),label} } };
        DesignSystem.Resource(rootProfileDrop,Border.BackgroundProperty,"BackgroundSecondary"); DesignSystem.Resource(rootProfileDrop,Border.BorderBrushProperty,"BorderDefault");
        var hint=L.T("Upuść serwer tutaj, aby przenieść go poza foldery.");
        ToolTip.SetTip(rootProfileDrop,hint); AutomationProperties.SetName(rootProfileDrop,hint);
        ConfigureProfileDrop(rootProfileDrop,""); Grid.SetRow(rootProfileDrop,1); connectionBrowser.RowDefinitions=new("*,Auto"); connectionBrowser.Children.Add(rootProfileDrop);
        ToolTip.SetTip(tree,L.T("Enter: połącz lub rozwiń folder · F2: edytuj · Delete: usuń po potwierdzeniu · Ctrl+D: duplikuj serwer"));
    }
    private bool CanDropProfile(ServerProfile profile,string folder)=>section=="connections" &&
        (folder.Length==0 || vm.Folders.Contains(folder)) && vm.AllServers.Any(p=>p.Id==profile.Id && p.Folder!=folder && IsBrowserProfileVisible(p));
    private void ConfigureProfileDrop(Control target,string folder)
    {
        DragDrop.SetAllowDrop(target,true);
        DragDrop.AddDragOverHandler(target,(_,e)=>
        { e.DragEffects=e.DataTransfer.TryGetValue(ServerDrag) is {} p && CanDropProfile(p,folder)?DragDropEffects.Move:DragDropEffects.None; e.Handled=true; });
        DragDrop.AddDropHandler(target,(_,e)=>
        {
            e.DragEffects=DragDropEffects.None;
            if(e.DataTransfer.TryGetValue(ServerDrag) is {} p && CanDropProfile(p,folder))
            { e.DragEffects=DragDropEffects.Move; _=Safe(()=>MoveBrowserProfileAsync(p,folder)); }
            e.Handled=true;
        });
    }
    private async Task MoveBrowserProfileAsync(ServerProfile profile,string folder)
    {
        if(!CanDropProfile(profile,folder))return;
        await repository.MoveToFolderAsync(profile.Id,folder,lifetime.Token);
        if(folder.Length>0)foreach(var ancestor in ProfileFolder.Ancestors(folder))collapsed.Remove(ancestor);
        await vm.LoadAsync();
        if(vm.AllServers.FirstOrDefault(p=>p.Id==profile.Id) is {} moved && IsBrowserProfileVisible(moved))vm.SelectedServer=moved;
        browserReveal=profile.Id; QueueTree();
    }
}
