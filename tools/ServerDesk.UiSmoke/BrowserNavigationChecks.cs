using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class BrowserNavigationChecks
{
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(owner)!;
    private static object? Call(MainWindow owner,string method,params object[] args)=>typeof(MainWindow).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(owner,args);
    private static IEnumerable<TreeViewItem> Nodes(IEnumerable<object?> items)
    { foreach(var node in items.OfType<TreeViewItem>()) { yield return node; foreach(var child in Nodes(node.Items))yield return child; } }
    private sealed class NoCredentials:ICredentialStore
    {
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>throw new Exception("Unexpected credential access");
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default)=>throw new Exception("Unexpected credential save");
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>throw new Exception("Unexpected credential delete");
    }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N")));
            var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var settings=repo.ApplicationSettingsAsync().Result;
            RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(settings with { Preferences=settings.Preferences with { Language=language,CheckUpdatesOnStartup=false } }));
            var target=new ServerProfile { Name="Navigation target",Host="fixture.invalid",Username="fixture",Folder="Prod/Linux/Apps" };
            var other=target with { Id=Guid.NewGuid(),Name="Other saved",Folder="Other",Protocol=Protocol.RDP,Port=3389,Favorite=true,Tags="other" };
            RedesignChecks.Wait(repo.SaveAsync(target)); RedesignChecks.Wait(repo.SaveAsync(other));
            RedesignChecks.Wait(repo.AddFolderAsync(string.Join('/',Enumerable.Range(0,32).Select(i=>"Level"+i))));
            RedesignChecks.Wait(repo.SetSettingAsync("collapsedFolders",JsonSerializer.Serialize(new[]{"Prod","Level0"})));
            MainViewModel vm=null!; MainWindow Open() { vm=new(repo); return new(vm,repo,new NoCredentials(),new(repo),new ExternalRdpClient()); }
            var main=Open(); main.Show(); RedesignChecks.Frames(20); var tree=Field<TreeView>(main,"tree");
            TreeViewItem Folder(string path)=>Nodes(tree.Items).Single(n=>n.Tag as string==path);
            var saved=new SessionPane(main,target,new(repo),new ExternalRdpClient(),repo); saved.PrepareRestored(0); main.RegisterPane(saved);
            var quick=new SessionPane(main,target with { Id=Guid.NewGuid() },new(repo),new ExternalRdpClient(),repo); quick.PrepareRestored(0); main.RegisterPane(quick);
            MenuItem Reveal(SessionPane pane)=>((MenuItem[])Call(main,"DockMenu",pane)!).Single(m=>m.Tag as string=="Pokaż serwer na liście");
            if(!Reveal(saved).IsEnabled || Reveal(quick).IsEnabled || Reveal(saved).Icon is not DeskIcon || Reveal(saved).Header as string!=AppText.T("Pokaż serwer na liście"))throw new Exception("Saved/quick connection reveal action state/icon/language is incorrect");
            var cachedReveal=Reveal(saved); var cachedMenu=new ContextMenu { ItemsSource=new[]{cachedReveal} };
            cachedMenu.Open(main); RedesignChecks.Frames(2); cachedMenu.Close(); RedesignChecks.Frames(1);
            RedesignChecks.Wait((Task)Call(main,"SelectSectionAsync","favorite")!); vm.Search="no match"; vm.ProtocolFilter=Protocol.RDP; vm.SelectedTags=["other"]; vm.TagMode=TagFilterMode.All;
            Call(main,"ToggleSidebar"); RedesignChecks.Frames(3);
            RedesignChecks.Wait((Task)Call(main,"RevealConnectionAsync",target.Id)!); RedesignChecks.Frames(5);
            if(!Field<DockPanel>(main,"sidebar").IsVisible || tree.SelectedItem is not TreeViewItem { Tag:ServerProfile selected } || selected.Id!=target.Id || vm.Search!="" || vm.ProtocolFilter!=null || vm.SelectedTags.Length!=0 || !Folder("Prod").IsExpanded || !Folder(target.Folder).IsExpanded)throw new Exception("Reveal did not clear filters/open sidebar/expand ancestors/select the correct profile");
            Call(main,"SetBrowserFoldersExpanded",false); RedesignChecks.Frames(3);
            if(Nodes(tree.Items).Any(n=>n.Tag is string && n.IsExpanded) || tree.SelectedItem is not TreeViewItem { Tag:string parent } || parent!="Prod" || vm.SelectedServer!=null)throw new Exception("Collapse all left hidden selection or an expanded folder");
            var collapsed=Field<HashSet<string>>(main,"collapsed").ToHashSet();
            vm.Search="Navigation target"; RedesignChecks.Frames(4); Call(main,"SetBrowserFoldersExpanded",false); RedesignChecks.Frames(2);
            if(!Field<HashSet<string>>(main,"collapsed").SetEquals(collapsed))throw new Exception("Search collapse overwrote normal folder state");
            vm.Search=""; RedesignChecks.Frames(4); Call(main,"SetBrowserFoldersExpanded",true); RedesignChecks.Frames(4);
            if(Nodes(tree.Items).Where(n=>n.Tag is string).Any(n=>!n.IsExpanded) || Field<HashSet<string>>(main,"collapsed").Count!=0)throw new Exception("Expand all missed deep/empty folders");
            RedesignChecks.Wait(repo.DeleteAsync(target.Id)); RedesignChecks.Wait(vm.LoadAsync()); RedesignChecks.Frames(3);
            if(Reveal(saved).IsEnabled)throw new Exception("Deleted saved profile still has an enabled reveal action");
            cachedMenu.Open(main); RedesignChecks.Frames(2);
            if(cachedReveal.IsEnabled)throw new Exception("A previously created tab menu retained deleted-profile availability");
            cachedMenu.Close(); RedesignChecks.Frames(1);
            RedesignChecks.Wait((Task)Call(main,"RevealConnectionAsync",target.Id)!); RedesignChecks.Frames(2);
            if(vm.AllServers.Count!=1 || vm.AllServers[0].Id!=other.Id || SavedCount(repo)!=1)throw new Exception("Reveal recreated a removed profile or changed other data");
            main.CaptureRenderedFrame()!.Save(Path.Combine(root,"browser-navigation-"+language+".png"));
            foreach(var pane in main.OpenSessions.ToArray()) { main.RemovePane(pane); RedesignChecks.Wait(pane.CloseAsync()); }
            main.Close(); RedesignChecks.Frames(10);
            main=Open(); main.Show(); RedesignChecks.Frames(15); tree=Field<TreeView>(main,"tree");
            if(Nodes(tree.Items).Where(n=>n.Tag is string).Any(n=>!n.IsExpanded))throw new Exception("Expand-all state was not restored after restart");
            Call(main,"SetBrowserFoldersExpanded",false); RedesignChecks.Frames(2); main.Close(); RedesignChecks.Frames(10);
            main=Open(); main.Show(); RedesignChecks.Frames(15); tree=Field<TreeView>(main,"tree");
            if(Nodes(tree.Items).Any(n=>n.Tag is string && n.IsExpanded))throw new Exception("Collapse-all state was not restored after restart");
            main.Close(); RedesignChecks.Frames(8);
            Console.WriteLine("PASS browser navigation "+language+": saved-tab reveal, hidden sidebar/filters, quick/deleted profiles, 32 folder levels, safe collapsed selection, temporary search state and expand/collapse restart persistence; no credentials or network");
        }
    }
    private static int SavedCount(ProfileRepository repo) { var task=repo.ListAsync(); RedesignChecks.Wait(task); return task.Result.Count; }
}
