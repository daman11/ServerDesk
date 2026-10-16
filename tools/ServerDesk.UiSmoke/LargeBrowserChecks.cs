using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class LargeBrowserChecks
{
    private static IEnumerable<TreeViewItem> Nodes(IEnumerable<object?> items)
    { foreach(var node in items.OfType<TreeViewItem>()) { yield return node; foreach(var child in Nodes(node.Items))yield return child; } }
    internal static void Run(string root,bool measureOnly=false)
    {
        const int count=5000;
        var repo=new ProfileRepository(new AppPaths(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N")))); RedesignChecks.Wait(repo.InitializeAsync());
        var profiles=Enumerable.Range(0,count).Select(i=>new ServerProfile { Name="web"+i,Host="10.12."+(i/250)+"."+(i%250+1),Username="deploy",Folder="DC"+(i%100)+"/Linux",Favorite=i%11==0,Protocol=i%2==0?Protocol.SSH:Protocol.RDP,Port=i%2==0?22:3389 }).ToList();
        using var json=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new ProfileRepository.ConfigurationExport(2,profiles,[]))); RedesignChecks.Wait(repo.ImportAsync(json));
        RedesignChecks.Wait(repo.SetSettingAsync("collapsedFolders",JsonSerializer.Serialize(Enumerable.Range(0,100).Select(i=>"DC"+i))));
        var vm=new MainViewModel(repo); var main=new MainWindow(vm,repo,new CredentialStore(new AppPaths(Path.Combine(root,"credentials"))),new SshConnectionFactory(repo),new ExternalRdpClient());
        var clock=Stopwatch.StartNew(); main.Show(); RedesignChecks.Frames(25); if(vm.AllServers.Count!=count)throw new Exception("Large fixture did not load");
        Console.WriteLine($"INFO {count}-profile window ready {clock.ElapsedMilliseconds}ms");
        var build=typeof(MainWindow).GetMethod("BuildTree",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var before=GC.GetAllocatedBytesForCurrentThread(); clock.Restart(); build.Invoke(main,null); clock.Stop(); var bytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Console.WriteLine($"INFO {count}-profile tree build {clock.ElapsedMilliseconds}ms, {bytes/1048576d:F2}MiB UI-thread allocations");
        var metrics=new { Profiles=count,TreeBuildMilliseconds=clock.Elapsed.TotalMilliseconds,UiThreadAllocatedBytes=bytes };
        File.WriteAllText(Path.Combine(root,"browser-measurement.json"),JsonSerializer.Serialize(metrics,new JsonSerializerOptions { WriteIndented=true }));
        if(measureOnly) { main.Close(); RedesignChecks.Frames(25); return; }
        var tree=(TreeView)typeof(MainWindow).GetField("tree",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(main)!;
        if(Nodes(tree.Items).Count(n=>n.Tag is ServerProfile)!=count || tree.Items.Count!=100)throw new Exception("Large tree lost folders or profiles");
        var notifications=0; vm.Servers.CollectionChanged+=(_,_)=>notifications++;
        clock.Restart(); vm.Search="web4999 deploy"; var elapsed=clock.Elapsed.TotalMilliseconds;
        if(vm.Servers.Count!=1 || vm.Servers[0].Name!="web4999")throw new Exception("Large multi-term search incorrect");
        RedesignChecks.Frames(); Console.WriteLine($"INFO large search {elapsed:F1}ms, {notifications} collection notifications");
        if(Nodes(tree.Items).Count(n=>n.Tag is ServerProfile)!=1)throw new Exception("Tree did not apply multi-term search");
        vm.Search="absent"; RedesignChecks.Frames(); if(vm.Servers.Count!=0)throw new Exception("Empty large search incorrect");
        notifications=0; vm.Search=""; Console.WriteLine($"INFO clear search to {count} rows: {notifications} collection notifications"); vm.SortMode=ProfileSort.Host; vm.SortDescending=true; RedesignChecks.Frames(); if(vm.Servers.Count!=count)throw new Exception("Clearing search lost profiles");
        vm.ProtocolFilter=Protocol.RDP; RedesignChecks.Frames(); if(Nodes(tree.Items).Count(n=>n.Tag is ServerProfile)!=count/2)throw new Exception("Large protocol filter is incorrect"); vm.ProtocolFilter=null; RedesignChecks.Frames();
        var selected=vm.AllServers.Single(p=>p.Name=="web1234");
        foreach(var folder in Nodes(tree.Items).Where(n=>n.Tag is string f && (f=="DC34" || f==selected.Folder)))folder.IsExpanded=true;
        RedesignChecks.Frames(); tree.SelectedItem=Nodes(tree.Items).Single(n=>n.Tag is ServerProfile p && p.Id==selected.Id); RedesignChecks.Frames();
        if(!tree.GetVisualDescendants().OfType<ProfileVisual>().Any())throw new Exception("Expanded server rows have no profile icons");
        vm.Search="web1234"; RedesignChecks.Frames(); if(vm.SelectedServer?.Id!=selected.Id)throw new Exception("Matching search lost selected identity");
        vm.Search="web4999"; tree.RaiseEvent(new KeyEventArgs { RoutedEvent=InputElement.KeyDownEvent,Key=Key.Enter }); RedesignChecks.Frames();
        if(vm.SelectedServer!=null || main.OpenSessions.Any() || main.OwnedWindows.Any())throw new Exception("Hidden large-tree selection was retained or connected");
        vm.Search=""; RedesignChecks.Frames(); if(vm.SelectedServer!=null)throw new Exception("Clearing search restored a hidden profile");
        var ordered=Nodes(tree.Items).Where(n=>n.Tag as string==selected.Folder).Single().Items.OfType<TreeViewItem>().Select(n=>((ServerProfile)n.Tag!).Id);
        var expected=ProfileOrdering.Sort(vm.AllServers.Where(p=>p.Folder==selected.Folder),ProfileSort.Host,true).Select(p=>p.Id);
        if(!ordered.SequenceEqual(expected))throw new Exception("Large folder host ordering is incorrect");
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"large-connections.png")); main.Close(); RedesignChecks.Frames();
        Console.WriteLine($"PASS {count} imported profiles, 200 folders, multi-term/empty/clear/protocol search, numeric IP ordering, expanded row icons, matching selection and immediate Enter guard for hidden profiles");
    }
}
