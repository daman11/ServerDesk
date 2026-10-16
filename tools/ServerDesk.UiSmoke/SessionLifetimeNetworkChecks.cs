using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SessionLifetimeNetworkChecks
{
    private static T Field<T>(object owner,string name)=>(T)(owner.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)??owner.GetType().GetField("<"+name+">P",BindingFlags.NonPublic|BindingFlags.Instance))!.GetValue(owner)!;
    private static void Until(Func<bool> done,string message,int seconds=40)
    { var end=DateTime.UtcNow.AddSeconds(seconds); while(!done() && DateTime.UtcNow<end)RedesignChecks.Frames(1); if(!done())throw new TimeoutException(message); }
    private static void Wait(Task work) { Until(()=>work.IsCompleted,"Session operation did not finish"); work.GetAwaiter().GetResult(); }
    private static T Result<T>(Task<T> work) { Wait(work); return work.Result; }
    private static bool Connected(Renci.SshNet.BaseClient client) { try { return client.IsConnected; } catch(ObjectDisposedException) { return false; } }
    private static Task Call(SessionPane pane,string method,params object[] args)=>(Task)typeof(SessionPane).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(pane,args)!;
    private static (MainWindow Main,ProfileRepository Repo,SshConnectionFactory Factory,ServerProfile Profile) Setup(string root,string host,string key)
    {
        var paths=new AppPaths(Path.Combine(root,"network-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
        var factory=new SshConnectionFactory(repo); var main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),factory,new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
        factory.ConfirmHostKey=(_,_)=>Task.FromResult(true); main.PreviewPreferences(main.Preferences with { ShowMetrics=false });
        return (main,repo,factory,new ServerProfile { Name="Owned lifecycle fixture",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) });
    }
    private static SessionPane Pane(MainWindow main,ProfileRepository repo,SshConnectionFactory factory,ServerProfile profile)
    { var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo); main.RegisterPane(pane); return pane; }
    private static void Echo(SessionPane pane)
    {
        RedesignChecks.Frames(2);
        var id=Guid.NewGuid().ToString("N")[..12]; var terminal=Field<TerminalControl>(pane,"terminal");
        var session=Field<SshTerminalSession>(pane,"session");
        Wait(session.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("printf '\\nLIFECYCLE_%s\\n' '"+id+"'\r")).AsTask());
        Wait(session.WriterStream.FlushAsync());
        Until(()=>SshAcceptanceChecks.Text(terminal).Replace("\n","").Contains("LIFECYCLE_"+id),"Terminal did not render its own marker");
    }
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static WeakReference[] ReconnectCycle(MainWindow main,SessionPane[] panes,int cycle)
    {
        foreach(var pane in panes) { main.DockSession(pane,cycle%2==0?SessionDock.Secondary:SessionDock.Primary); Echo(pane); }
        var lifetimes=panes.Select(p=>new WeakReference(Field<CancellationTokenSource>(p,"connectionLifetime"))).ToArray();
        var previous=panes.Select(p=>Field<SshTerminalSession>(p,"session")).ToArray();
        Wait(Task.WhenAll(panes.Select(p=>p.ReconnectAsync())));
        if(previous.Any(p=>!p.Completion.IsCompleted))throw new Exception("Reconnect returned before the previous transport stopped");
        return lifetimes;
    }
    internal static void Run(string root,string host,string key)
    {
        var (main,repo,factory,profile)=Setup(root,host,key);
        var panes=Enumerable.Range(0,4).Select(i=>Pane(main,repo,factory,profile with { Id=Guid.NewGuid(),Name="Lifecycle "+i })).ToArray();
        using var parent=new CancellationTokenSource(); var lifetimes=new List<WeakReference>();
        Wait(Task.WhenAll(panes.Select(p=>p.ConnectAsync("serverdesk-test-passphrase",parent.Token))));
        for(var cycle=0;cycle<4;cycle++)
        {
            lifetimes.AddRange(ReconnectCycle(main,panes,cycle));
            Console.WriteLine($"INFO reconnect cycle {cycle+1}/4 complete");
        }
        main.DockSession(panes[0],SessionDock.Floating); Echo(panes[0]); main.DockSession(panes[0],SessionDock.Primary);
        main.FocusSession(panes[1]); Wait(panes[1].ShowAsync(1)); var endedBrowser=Field<SftpSession>(panes[1],"sftp");
        Wait(panes[1].ShowAsync(2)); Until(()=>panes[1].LatestSnapshot!=null,"EOF fixture did not sample monitoring");
        var endedMonitor=Field<LinuxMonitoring>(panes[1],"monitor"); var ending=Field<SshTerminalSession>(panes[1],"session");
        Wait(ending.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("exit\r")).AsTask()); Wait(ending.WriterStream.FlushAsync());
        Until(()=>panes[1].VisualState==SessionVisualState.Closed,"EOF did not close the pane"); Wait(Field<Task>(panes[1],"transportShutdown"));
        if(Connected(Field<Renci.SshNet.SftpClient>(endedBrowser,"client")) || Connected(Field<Renci.SshNet.SshClient>(endedMonitor,"client")) || panes[1].LatestSnapshot!=null)throw new Exception("EOF retained dependent connections or stale metrics");
        Wait(panes[1].ReconnectAsync()); Echo(panes[1]);
        Console.WriteLine("PASS remote shell exit closes dependent SFTP/monitoring, clears stale metrics and allows reconnect");
        main.FocusSession(panes[0]);
        Wait(panes[0].ShowAsync(2)); Until(()=>panes[0].LatestSnapshot!=null,"Monitoring did not sample before close");
        var monitor=Field<LinuxMonitoring>(panes[0],"monitor"); var monitoring=Field<Task>(panes[0],"monitorLoop");
        Wait(panes[0].ShowAsync(1)); var browser=Field<SftpSession>(panes[0],"sftp");
        foreach(var pane in panes) { lifetimes.Add(new(Field<CancellationTokenSource>(pane,"connectionLifetime"))); main.RemovePane(pane); }
        Wait(Task.WhenAll(panes.Select(p=>p.CloseAsync()))); RedesignChecks.Frames(5);
        if(!monitoring.IsCompleted || Connected(Field<Renci.SshNet.SshClient>(monitor,"client")) || Connected(Field<Renci.SshNet.SftpClient>(browser,"client")))throw new Exception("Close retained monitoring/browser transport");
        for(var i=0;i<3;i++) { GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); RedesignChecks.Frames(3); }
        Console.WriteLine("INFO retained previous lifetimes: "+string.Join(",",lifetimes.Take(16).Select((r,i)=>(r,i)).Where(p=>p.r.IsAlive).Select(p=>p.i)));
        if(lifetimes.Take(16).Any(p=>p.IsAlive))throw new Exception("Reconnect retained linked cancellation sources");
        GC.KeepAlive(parent); main.Close(); RedesignChecks.Frames();
        Console.WriteLine("PASS four parallel SSH sessions, 16 reconnects, terminal round trips, split/floating/redock, 16 collected previous lifetimes and drained browser/monitoring");
        Blackhole(root,host,key);
    }
    private static void Blackhole(string root,string host,string key)
    {
        var (main,repo,factory,direct)=Setup(root,host,key); using var proxy=new SftpFaultProxy(host,22222);
        using var inspection=new SftpSession(Result(factory.SftpAsync(direct,"serverdesk-test-passphrase",default)));
        var profile=direct with { Host="127.0.0.1",Port=proxy.Port }; var pane=Pane(main,repo,factory,profile);
        var pending=proxy.ArmNext(); Wait(pane.ConnectAsync("serverdesk-test-passphrase",default)); var ssh=Result(pending);
        pending=proxy.ArmNext(); Wait(pane.ShowAsync(1)); var browserRelay=Result(pending);
        pending=proxy.ArmNext(); Wait(pane.ShowAsync(2)); var monitorRelay=Result(pending);
        Until(()=>pane.LatestSnapshot!=null,"Silent-loss fixture did not sample monitoring"); Wait(pane.ShowAsync(1));
        var id=Guid.NewGuid().ToString("N"); var remote="/tmp/serverdesk-lifetime-"+id; Wait(inspection.CreateDirectoryAsync(remote,default));
        var local=Path.Combine(root,"payload-"+id); Directory.CreateDirectory(local); var source=Path.Combine(local,"payload.bin"); File.WriteAllBytes(source,new byte[8<<20]);
        Field<TextBox>(pane,"localPath").Text=Path.GetFullPath(local); Field<TextBox>(pane,"remotePath").Text=remote; Wait(Call(pane,"RefreshAsync"));
        var entry=Field<ListBox>(pane,"localList").Items.OfType<FileEntry>().Single(e=>e.Name=="payload.bin");
        SftpFaultProxy.Connection? uploadRelay=null;
        try
        {
            pending=proxy.ArmNext(); var upload=Call(pane,"UploadEntryAsync",entry); uploadRelay=Result(pending); Until(()=>pane.TransferSnapshots.Any(p=>p.Progress>0),"Upload did not start"); uploadRelay.Paused=true;
            // Reserve the other slot so the next job is definitely queued.
            if(!MainWindow.TransferSlots.Wait(0))throw new Exception("Fixture could not reserve its second slot");
            Task queued;
            try { queued=Call(pane,"UploadEntryAsync",entry); RedesignChecks.Frames(2); if(pane.TransferSnapshots.Count(p=>p.IsActive)!=2)throw new Exception("Queued transfer missing"); }
            finally { MainWindow.TransferSlots.Release(); }
            ssh.Paused=true; browserRelay.Paused=true; monitorRelay.Paused=true;
            using var watchdog=new CancellationTokenSource(); var activeRelay=uploadRelay;
            _=Task.Run(async()=> { try { await Task.Delay(8000,watchdog.Token); activeRelay.Dispose(); ssh.Dispose(); browserRelay.Dispose(); monitorRelay.Dispose(); } catch(OperationCanceledException) { } });
            var watch=Stopwatch.StartNew(); main.RemovePane(pane); var immediate=watch.Elapsed; var close=pane.CloseAsync();
            var applicationClosed=false; var earlyExit=false;
            main.Closed+=(_,_)=> { applicationClosed=true; earlyExit=!close.IsCompleted; }; main.Close();
            Wait(close); watch.Stop(); watchdog.Cancel();
            Until(()=>applicationClosed,"Application did not wait for its removed session");
            if(earlyExit)throw new Exception("Application exited before its removed session finished closing");
            Wait(Task.WhenAll(upload,queued));
            if(!ReferenceEquals(close,pane.CloseAsync()))throw new Exception("Close is not idempotent");
            if(MainWindow.TransferSlots.CurrentCount!=2 || pane.ActiveTransfers!=0 || Field<HashSet<Task>>(pane,"transferTasks").Count!=0)throw new Exception("Close returned with active transfer work or retained slots");
            Console.WriteLine($"INFO silent-network close: UI removal {immediate.TotalMilliseconds:F0}ms, full drain {watch.ElapsedMilliseconds}ms");
            if(immediate>TimeSpan.FromMilliseconds(500) || watch.Elapsed>TimeSpan.FromSeconds(5))throw new Exception("Silent-network close blocked UI or did not drain promptly");
            Console.WriteLine("PASS close during silent SSH/upload loss drains active and queued jobs and returns both global slots");
        }
        finally
        {
            proxy.Dispose(); Wait(pane.CloseAsync()); if(main.OpenSessions.Contains(pane))main.RemovePane(pane); main.Close(); RedesignChecks.Frames();
            // Delete only entries in this run's exact GUID directory, without following links.
            foreach(var item in Result(inspection.ListAsync(remote,default)))
            {
                if(item.IsDirectory || !item.FullPath.StartsWith(remote+"/",StringComparison.Ordinal))throw new Exception("Unexpected fixture entry");
                Wait(inspection.DeleteAsync(item,default));
            }
            Wait(inspection.DeleteAsync(new("fixture",remote,true,false,0,DateTime.Now,""),default));
        }
    }
    internal static void Endurance(string root,string host,string key,int minutes)
    {
        if(minutes is <1 or >30)throw new ArgumentOutOfRangeException(nameof(minutes));
        var (main,repo,factory,profile)=Setup(root,host,key); using var parent=new CancellationTokenSource();
        var panes=Enumerable.Range(0,8).Select(i=>Pane(main,repo,factory,profile with { Id=Guid.NewGuid(),Name="Endurance "+i })).ToArray();
        var samples=new List<object>(); var watch=Stopwatch.StartNew(); var cycles=0;
        using var measured=Process.GetCurrentProcess(); var lastCpu=measured.TotalProcessorTime; var lastElapsed=watch.Elapsed;
        try
        {
            Wait(Task.WhenAll(panes.Select(p=>p.ConnectAsync("serverdesk-test-passphrase",parent.Token))));
            main.PreviewPreferences(main.Preferences with { ShowMetrics=true }); Wait(panes[0].ShowAsync(2));
            while(watch.Elapsed<TimeSpan.FromMinutes(minutes))
            {
                foreach(var pane in panes) { main.DockSession(pane,cycles%2==0?SessionDock.Secondary:SessionDock.Primary); Echo(pane); }
                var reconnect=panes[cycles%panes.Length]; Wait(reconnect.ReconnectAsync()); Echo(reconnect);
                if(cycles%2==0) { main.DockSession(reconnect,SessionDock.Floating); RedesignChecks.Frames(2); main.DockSession(reconnect,SessionDock.Primary); }
                if(cycles%4==0) { Wait(reconnect.ShowAsync(1)); Wait(reconnect.ShowAsync(0)); }
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); using var process=Process.GetCurrentProcess(); process.Refresh();
                var elapsed=watch.Elapsed; var cpu=process.TotalProcessorTime;
                var cpuPercent=100*(cpu-lastCpu).TotalSeconds/Math.Max(.001,(elapsed-lastElapsed).TotalSeconds)/Environment.ProcessorCount;
                lastCpu=cpu; lastElapsed=elapsed;
                var sample=new { seconds=(int)elapsed.TotalSeconds,cycles=++cycles,connected=panes.Count(p=>p.IsConnected),managedBytes=GC.GetTotalMemory(false),privateBytes=process.PrivateMemorySize64,workingSetBytes=process.WorkingSet64,cpuPercent,handles=process.HandleCount,threads=process.Threads.Count };
                if(sample.connected!=panes.Length)throw new Exception("An endurance session disconnected unexpectedly");
                samples.Add(sample); File.WriteAllText(Path.Combine(root,"endurance.json"),JsonSerializer.Serialize(samples,new JsonSerializerOptions { WriteIndented=true })); Console.WriteLine(JsonSerializer.Serialize(sample));
                var next=watch.Elapsed+TimeSpan.FromSeconds(15); while(watch.Elapsed<next && watch.Elapsed<TimeSpan.FromMinutes(minutes))RedesignChecks.Frames(1);
            }
        }
        finally
        {
            foreach(var pane in panes)main.RemovePane(pane); Wait(Task.WhenAll(panes.Select(p=>p.CloseAsync()))); main.Close(); RedesignChecks.Frames();
        }
        if(MainWindow.TransferSlots.CurrentCount!=2)throw new Exception("Endurance retained transfer slots");
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); measured.Refresh();
        File.WriteAllText(Path.Combine(root,"endurance-closed.json"),JsonSerializer.Serialize(new { cycles,managedBytes=GC.GetTotalMemory(false),privateBytes=measured.PrivateMemorySize64,workingSetBytes=measured.WorkingSet64,handles=measured.HandleCount,threads=measured.Threads.Count,transferSlots=MainWindow.TransferSlots.CurrentCount },new JsonSerializerOptions { WriteIndented=true }));
        Console.WriteLine($"PASS {minutes}-minute eight-session endurance with {cycles} reconnects, terminal markers, monitoring, SFTP and repeated docking; measurements in endurance.json");
    }
}
