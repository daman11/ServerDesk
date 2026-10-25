using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class ConnectionPresenceChecks
{
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(owner)!;
    private static void Invoke(MainWindow main,string name,params object[] args)=>typeof(MainWindow).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(main,args);
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
        var measurements=new List<object>();
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"presence-"+language+"-"+Guid.NewGuid().ToString("N")));
            var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var settings=repo.ApplicationSettingsAsync().Result;
            RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(settings with { Preferences=settings.Preferences with { Language=language,CheckUpdatesOnStartup=false,ShowConnectionAddresses=true } }));
            const int count=5000;
            var profiles=Enumerable.Range(0,count).Select(i=>new ServerProfile { Name="node"+i,Host="fixture.invalid",Username="fixture",Folder="DC"+(i%100)+"/Linux",Favorite=i%4==0,Color="#4C9CE5" }).ToList();
            using var json=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new ProfileRepository.ConfigurationExport(2,profiles,[])));
            RedesignChecks.Wait(repo.ImportAsync(json));
            RedesignChecks.Wait(repo.SetSettingAsync("collapsedFolders",JsonSerializer.Serialize(Enumerable.Range(0,100).Select(i=>"DC"+i))));
            var vm=new MainViewModel(repo); var main=new MainWindow(vm,repo,new NoCredentials(),new(repo),new ExternalRdpClient());
            main.Show(); SshAcceptanceChecks.Until(()=>Field<Task?>(main,"initialization")!=null,"Browser initialization did not start");
            RedesignChecks.Wait(Field<Task>(main,"initialization")); RedesignChecks.Frames(3); Field<DispatcherTimer>(main,"statusTimer").Stop();
            var imported=vm.AllServers.ToDictionary(p=>p.Name); profiles=profiles.Select(p=>imported[p.Name]).ToList();
            var tree=Field<TreeView>(main,"tree");
            TreeViewItem Folder(string name)=>Nodes(tree.Items).Single(n=>n.Tag as string==name);
            TreeViewItem Profile(Guid id)=>Nodes(tree.Items).Single(n=>n.Tag is ServerProfile p && p.Id==id);
            void Live(params Guid[] ids)=>Invoke(main,"ApplyBrowserConnectionStates",ids.ToHashSet());
            DeskIcon Marker(TreeViewItem node)=>node.GetVisualDescendants().OfType<DeskIcon>().Single(i=>i.Name=="ConnectionPresenceMarker");
            var clock=Stopwatch.StartNew(); var allocated=GC.GetAllocatedBytesForCurrentThread(); Invoke(main,"BuildTree"); clock.Stop();
            var buildMs=clock.Elapsed.TotalMilliseconds; var buildBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            RedesignChecks.Frames(3); Folder("DC0").IsExpanded=true; RedesignChecks.Frames(3);
            var target=profiles[2000]; var selected=Profile(target.Id); tree.SelectedItem=selected; selected.BringIntoView(); selected.Focus(); RedesignChecks.Frames(4);
            var marker=Marker(selected); if(marker.IsVisible)throw new Exception("Disconnected row has a connected marker");
            var row=(Grid)marker.Parent!; var badge=row.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Text=="SSH");
            var protocolX=badge.TranslatePoint(default,tree)!.Value.X;
            var scroll=tree.GetVisualDescendants().OfType<ScrollViewer>().First(); var offset=scroll.Offset;
            if(offset.Y<=0)throw new Exception("Fixture did not exercise a scrolled connection tree");
            var source=tree.ItemsSource; var roots=tree.Items.ToArray(); var menu=selected.ContextMenu!;
            menu.Open(selected); RedesignChecks.Frames(2); var focus=main.FocusManager?.GetFocusedElement();
            var menuItems=menu.Items.ToArray();
            allocated=GC.GetAllocatedBytesForCurrentThread(); clock.Restart();
            for(var i=0;i<1000;i++)Live(i%2==0?new[]{target.Id,profiles[1].Id}:Array.Empty<Guid>());
            clock.Stop(); var updateMs=clock.Elapsed.TotalMilliseconds; var updateBytes=GC.GetAllocatedBytesForCurrentThread()-allocated;
            Live(target.Id,profiles[1].Id); RedesignChecks.Frames(4);
            if(!ReferenceEquals(source,tree.ItemsSource) || !tree.Items.SequenceEqual(roots) || !ReferenceEquals(tree.SelectedItem,selected))throw new Exception("Presence update rebuilt the tree or lost selection");
            if(scroll.Offset!=offset || !menu.IsOpen || !menu.Items.SequenceEqual(menuItems))throw new Exception("Presence update moved scrolling or recreated an open menu");
            if(focus!=null && !ReferenceEquals(main.FocusManager?.GetFocusedElement(),focus))throw new Exception("Presence update stole keyboard focus");
            if(!marker.IsVisible || Math.Abs(protocolX-badge.TranslatePoint(default,tree)!.Value.X)>.1)throw new Exception("Presence update lost the marker or moved the protocol");
            menu.Close(); RedesignChecks.Frames(2);
            Folder("DC1").IsExpanded=true; RedesignChecks.Frames(4); var lazy=Profile(profiles[1].Id); lazy.BringIntoView(); RedesignChecks.Frames(3);
            if(!Marker(lazy).IsVisible)throw new Exception("A row opened after a status change used a stale snapshot");
            Live(target.Id,target.Id); RedesignChecks.Frames(2);
            if(!marker.IsVisible || Marker(lazy).IsVisible)throw new Exception("Duplicate profile IDs or independent disconnect changed the wrong marker");
            Live(); RedesignChecks.Frames(2); if(marker.IsVisible)throw new Exception("Last disconnect retained a green marker");
            vm.Search="node2000"; RedesignChecks.Frames(5); Live(target.Id); RedesignChecks.Frames(2);
            if(Field<IDictionary>(main,"browserConnectionStates").Count!=1 || !Marker(Profile(target.Id)).IsVisible)throw new Exception("Filtering retained old row state or lost a connected marker");
            vm.Search=""; RedesignChecks.Frames(5); Live();
            var panes=profiles.Take(2).Select(p=>new SessionPane(main,p,new(repo),new ExternalRdpClient(),repo)).ToArray();
            foreach(var pane in panes) { pane.PrepareRestored(0); main.RegisterPane(pane); }
            RedesignChecks.Frames(4); source=tree.ItemsSource; selected=Profile(target.Id); tree.SelectedItem=selected;
            for(var i=0;i<10;i++) { main.FocusSession(panes[i%2]); RedesignChecks.Frames(1); }
            main.DockSession(panes[0],SessionDock.Secondary); RedesignChecks.Frames(3);
            if(!ReferenceEquals(source,tree.ItemsSource) || !ReferenceEquals(tree.SelectedItem,selected) || panes.Any(p=>p.IsConnected))throw new Exception("Tab focus/docking rebuilt the tree or connected a restored session");
            main.CaptureRenderedFrame()!.Save(Path.Combine(root,"connection-presence-"+language+".png"));
            foreach(var pane in panes) { main.RemovePane(pane); RedesignChecks.Wait(pane.CloseAsync()); }
            main.Close(); RedesignChecks.Frames(8);
            measurements.Add(new { language,profiles=count,folders=200,updates=1000,treeBuildMs=buildMs,treeBuildAllocatedBytes=buildBytes,statusUpdatesMs=updateMs,statusUpdatesAllocatedBytes=updateBytes,treeReplaced=false,simulatedPresence=true,realServerUsed=false });
            Console.WriteLine("PASS connection presence "+language+": 5000 profiles, 1000 simulated updates, no tree replacement, stable selection/scroll/focus/menu/protocol, lazy rows, duplicate IDs, filtering, tab switching/docking and no credential access");
        }
        File.WriteAllText(Path.Combine(root,"measurements.json"),JsonSerializer.Serialize(measurements,new JsonSerializerOptions { WriteIndented=true }));
    }
}
