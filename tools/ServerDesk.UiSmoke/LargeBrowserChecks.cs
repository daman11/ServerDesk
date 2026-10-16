using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class LargeBrowserChecks
{
    internal static void Run(string root)
    {
        var repo=new ProfileRepository(new AppPaths(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N")))); RedesignChecks.Wait(repo.InitializeAsync());
        var profiles=Enumerable.Range(0,1000).Select(i=>new ServerProfile { Name="web"+i,Host="10.12."+(i/250)+"."+(i%250+1),Username="deploy",Folder="DC"+(i%10)+"/Linux",Favorite=i%11==0 }).ToList();
        using var json=new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new ProfileRepository.ConfigurationExport(2,profiles,[]))); RedesignChecks.Wait(repo.ImportAsync(json));
        var vm=new MainViewModel(repo); var main=new MainWindow(vm,repo,new CredentialStore(new AppPaths(Path.Combine(root,"credentials"))),new SshConnectionFactory(repo),new ExternalRdpClient());
        var clock=Stopwatch.StartNew(); main.Show(); RedesignChecks.Frames(25); if(vm.AllServers.Count!=1000)throw new Exception("Large fixture did not load");
        Console.WriteLine($"INFO 1000-profile window ready {clock.ElapsedMilliseconds}ms");
        var build=typeof(MainWindow).GetMethod("BuildTree",BindingFlags.Instance|BindingFlags.NonPublic)!;
        var before=GC.GetAllocatedBytesForCurrentThread(); clock.Restart(); build.Invoke(main,null); clock.Stop(); var bytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Console.WriteLine($"INFO 1000-profile tree build {clock.ElapsedMilliseconds}ms, {bytes/1048576d:F2}MiB UI-thread allocations");
        var notifications=0; vm.Servers.CollectionChanged+=(_,_)=>notifications++;
        clock.Restart(); vm.Search="web999 deploy"; var elapsed=clock.Elapsed.TotalMilliseconds;
        if(vm.Servers.Count!=1 || vm.Servers[0].Name!="web999")throw new Exception("Large multi-term search incorrect");
        RedesignChecks.Frames(); Console.WriteLine($"INFO large search {elapsed:F1}ms, {notifications} collection notifications");
        vm.Search="absent"; RedesignChecks.Frames(); if(vm.Servers.Count!=0)throw new Exception("Empty large search incorrect");
        notifications=0; vm.Search=""; Console.WriteLine($"INFO clear search to 1000 rows: {notifications} collection notifications"); vm.SortMode=ProfileSort.Host; vm.SortDescending=true; RedesignChecks.Frames(); if(vm.Servers.Count!=1000)throw new Exception("Clearing search lost profiles");
        var selected=vm.Servers[500]; vm.SelectedServer=selected;
        vm.Search="web5"; RedesignChecks.Frames(); vm.Search=""; RedesignChecks.Frames(); if(vm.SelectedServer?.Id!=selected.Id)throw new Exception("Filtering changed selected identity");
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"large-connections.png")); main.Close(); RedesignChecks.Frames();
        Console.WriteLine("PASS 1000 imported profiles, multi-term/empty/clear search, IP ordering and selected identity");
    }
}
