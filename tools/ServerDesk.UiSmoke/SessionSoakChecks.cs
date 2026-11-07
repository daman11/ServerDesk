using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using ServerDesk.Verification;

internal static class SessionSoakChecks
{
    private sealed record Retired(WeakReference Pane,WeakReference ReconnectedLifetime,WeakReference ClosedLifetime);
    private const string FixturePassphrase="serverdesk-test-passphrase";
    private sealed class DeniedCredentials:ICredentialStore
    {
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>throw new Exception("Soak must not read saved credentials.");
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default)=>throw new Exception("Soak must not save credentials.");
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>throw new Exception("Soak must not delete credentials.");
    }
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(value)!;
    private static void Wait(Task work)=>RedesignChecks.Wait(work);
    private static T Result<T>(Task<T> work) { Wait(work); return work.Result; }
    private static Task Call(SessionPane pane,string method,params object[] args)=>(Task)pane.GetType().GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(pane,args)!;
    private static TerminalControl Terminal(SessionPane pane)=>Field<TerminalControl>(pane,"terminal");
    private static void Until(Func<bool> ready,string message)=>SshAcceptanceChecks.Until(ready,message);
    private static void WriteJson(string path,object value)
    { File.WriteAllText(path+".tmp",JsonSerializer.Serialize(value,new JsonSerializerOptions { WriteIndented=true })); File.Move(path+".tmp",path,true); }
    private static void RejectReparseAncestors(string path)
    {
        for(var ancestor=new DirectoryInfo(path);ancestor!=null;ancestor=ancestor.Parent)
            if(ancestor.Exists && ancestor.Attributes.HasFlag(FileAttributes.ReparsePoint))throw new ArgumentException("Soak scratch must not traverse a symbolic link or junction.");
    }
    internal static string BootstrapDataRoot()
    {
        var root=Path.GetFullPath("artifacts/soak-data"); RejectReparseAncestors(root);
        return Path.Combine(root,"bootstrap-"+Guid.NewGuid().ToString("N"));
    }
    private static void Collect()
    { RedesignChecks.Frames(12); GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect(); RedesignChecks.Frames(4); }
    private static SoakResources Resources()
    { using var p=Process.GetCurrentProcess(); p.Refresh(); return new(GC.GetTotalMemory(false),p.PrivateMemorySize64,p.WorkingSet64,p.HandleCount,p.Threads.Count); }
    private static string Tail(SessionPane pane)
    {
        var buffer=Terminal(pane).Terminal.Buffer;
        return string.Join("",Enumerable.Range(Math.Max(0,buffer.Lines.Length-80),Math.Min(80,buffer.Lines.Length)).Select(i=>buffer.Lines[i]?.TranslateToString(true)??""));
    }
    private static void Echo(MainWindow main,SessionPane pane,bool prime=false)
    {
        main.FocusSession(pane); RedesignChecks.Frames(2);
        var id=Guid.NewGuid().ToString("N");
        var text=(prime?"printf 'warm buffer line\\n%.0s' {1..1100}; ":"")+"printf '\\nSD_SOAK_%s\\n' '"+id+"'\r";
        Wait(Terminal(pane).SendInputAsync(text));
        Until(()=>Tail(pane).Contains("SD_SOAK_"+id,StringComparison.Ordinal),"Owned SSH terminal did not render its own marker.");
    }
    private static SessionPane Open(MainWindow main,ProfileRepository repo,SshConnectionFactory factory,ServerProfile profile,int slot)
    {
        var pane=new SessionPane(main,profile with { Id=Guid.NewGuid(),Name="Soak fixture "+slot },factory,new ExternalRdpClient(),repo); main.RegisterPane(pane);
        main.DockSession(pane,new[]{SessionDock.Primary,SessionDock.Secondary,SessionDock.Tertiary,SessionDock.Quaternary}[slot%4]);
        try { Wait(pane.ConnectAsync(FixturePassphrase,default)); Echo(main,pane,true); return pane; }
        catch { main.RemovePane(pane); Wait(pane.CloseAsync()); throw; }
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Retired Replace(MainWindow main,ProfileRepository repo,SshConnectionFactory factory,ServerProfile profile,List<SessionPane> panes,int cycle)
    {
        var index=cycle%panes.Count; var old=panes[index];
        var reconnected=new WeakReference(Field<CancellationTokenSource>(old,"connectionLifetime"));
        main.FocusSession(old); Wait(old.ReconnectAsync()); Echo(main,old);
        var result=new Retired(new(old),reconnected,new(Field<CancellationTokenSource>(old,"connectionLifetime")));
        main.RemovePane(old); Wait(old.CloseAsync()); panes[index]=Open(main,repo,factory,profile,index); return result;
    }
    private static void Close(MainWindow main,IEnumerable<SessionPane> panes)
    {
        var items=panes.ToArray(); foreach(var pane in items)main.RemovePane(pane);
        Wait(Task.WhenAll(items.Select(p=>p.CloseAsync()))); main.Focus(); RedesignChecks.Frames(10);
    }
    private static void Transfer(MainWindow main,SessionPane pane,string local,string remote,int cycle)
    {
        main.FocusSession(pane); RedesignChecks.Frames(2);
        var source=Path.Combine(local,"payload-"+cycle.ToString("D4")+".bin"); var destination=Path.Combine(local,"download");
        Directory.CreateDirectory(destination);
        var bytes=Enumerable.Range(0,65536).Select(i=>(byte)(i+cycle)).ToArray(); File.WriteAllBytes(source,bytes);
        Wait(pane.ShowAsync(1));
        Field<TextBox>(pane,"localPath").Text=local; Field<TextBox>(pane,"remotePath").Text=remote; Wait(Call(pane,"RefreshAsync"));
        var upload=Field<ListBox>(pane,"localList").Items.OfType<FileEntry>().Single(e=>e.FullPath==source);
        Wait(Call(pane,"UploadEntryAsync",upload));
        var download=Field<ListBox>(pane,"remoteList").Items.OfType<FileEntry>().Single(e=>e.Name==Path.GetFileName(source));
        Field<TextBox>(pane,"localPath").Text=destination; Wait(Call(pane,"RefreshAsync")); Wait(Call(pane,"DownloadEntryAsync",download));
        var received=File.ReadAllBytes(Path.Combine(destination,Path.GetFileName(source)));
        if(!SHA256.HashData(received).SequenceEqual(SHA256.HashData(bytes)))throw new Exception("Owned SFTP round-trip SHA-256 mismatch.");
        if(pane.ActiveTransfers!=0 || MainWindow.TransferSlots.CurrentCount!=2)throw new Exception("SFTP retained work or a transfer slot.");
        Wait(pane.ShowAsync(0));
    }
    private static void Monitoring(MainWindow main,SessionPane pane)
    { main.FocusSession(pane); RedesignChecks.Frames(2); Wait(pane.ShowAsync(2)); Until(()=>pane.LatestSnapshot!=null,"Owned Linux monitoring did not produce a sample."); Wait(pane.ShowAsync(0)); }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void Warmup(MainWindow main,ProfileRepository repo,SshConnectionFactory factory,ServerProfile profile,string local,string remote)
    {
        var panes=new List<SessionPane>();
        try
        {
            for(var i=0;i<SessionSoakPlan.Sessions;i++)panes.Add(Open(main,repo,factory,profile,i));
            Monitoring(main,panes[0]); Transfer(main,panes[1],local,remote,-1);
            main.DockSession(panes[0],SessionDock.Floating); RedesignChecks.Frames(3); main.DockSession(panes[0],SessionDock.Primary);
        }
        finally { Close(main,panes); }
    }
    internal static void Run(string root,string key,string fingerprint,SessionSoakPlan plan)
    {
        plan.Validate();
        if(!fingerprint.StartsWith("SHA256:",StringComparison.Ordinal) || fingerprint.Length is <40 or >60)throw new ArgumentException("An explicit disposable fixture fingerprint is required.");
        root=Path.GetFullPath(root);
        var artifacts=Path.GetFullPath("artifacts")+Path.DirectorySeparatorChar;
        if(!root.StartsWith(artifacts,OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))throw new ArgumentException("Soak output must be inside workspace artifacts.");
        RejectReparseAncestors(root); RejectReparseAncestors(Path.Combine(artifacts,"soak-data"));
        var nonce=Guid.NewGuid().ToString("N"); var scratch=Path.Combine(artifacts,"soak-data",nonce); Directory.CreateDirectory(scratch);
        var local=Path.Combine(scratch,"files"); Directory.CreateDirectory(local);
        var paths=new AppPaths(Path.Combine(scratch,"profile")); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
        var preferences=new ApplicationPreferences { CheckUpdatesOnStartup=false,TerminalScrollbackLines=1000,SftpLocalDirectory=local,ShowMetrics=false,DisabledPlugins=new([PluginCatalog.Commands,PluginCatalog.Bitwarden]),Bitwarden=new(UnlockOnStartup:false) };
        Wait(repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults with { RememberWorkspace=false,Preferences=preferences }));
        var factory=new SshConnectionFactory(repo); var main=new MainWindow(new(repo),repo,new DeniedCredentials(),factory,new ExternalRdpClient()); main.Show();
        Until(()=>Field<Task?>(main,"initialization")!=null,"Soak workspace did not initialize."); Wait(Field<Task>(main,"initialization"));
        factory.ConfirmHostKey=(challenge,_)=>Task.FromResult(challenge.Host=="127.0.0.1" && challenge.Port==22222 && challenge.Fingerprint==fingerprint);
        main.SetWorkspaceMode(WorkspaceMode.Grid);
        var profile=new ServerProfile { Name="Owned soak fixture",Host="127.0.0.1",Port=22222,Username="serverdesk_fixture",Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var remote="/tmp/serverdesk-soak-"+nonce;
        using var inspection=new SftpSession(Result(factory.SftpAsync(profile,FixturePassphrase,default)));
        Wait(inspection.CreateDirectoryAsync(remote,default));
        var panes=new List<SessionPane>(); var retired=new List<Retired>();
        var started=DateTimeOffset.UtcNow; var watch=new Stopwatch(); var cycle=0; var transfers=0; var samples=0; var maximumGap=0d; var maximumUiGap=0d; var lastUiBeat=0d; var nextSample=0d; var nextEcho=60d;
        var heartbeat=new Avalonia.Threading.DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };
        heartbeat.Tick+=(_,_)=> { var now=watch.Elapsed.TotalSeconds; maximumUiGap=Math.Max(maximumUiGap,now-lastUiBeat); lastUiBeat=now; };
        using var process=Process.GetCurrentProcess(); var lastCpu=process.TotalProcessorTime; var lastCpuSeconds=0d; var lastSample=0d;
        SoakResources? baseline=null,final=null; string? failure=null; string[] retainedFailures=[],completionFailures=[]; var state="WARMUP";
        var metadata=new { plan,sessionCount=SessionSoakPlan.Sessions,nonce,startedUtc=started,source=typeof(App).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            os=System.Runtime.InteropServices.RuntimeInformation.OSDescription,runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,processors=Environment.ProcessorCount,pid=Environment.ProcessId,
            renderer="Avalonia headless + Skia; real OpenSSH loopback; not native desktop acceptance",scratch,remote,bootstrap=Environment.GetEnvironmentVariable("SERVERDESK_DATA_HOME"),
            budgets=new { SessionSoakPlan.ManagedGrowthBudget,SessionSoakPlan.PrivateGrowthBudget,SessionSoakPlan.HandleGrowthBudget,SessionSoakPlan.ThreadGrowthBudget,SessionSoakPlan.MaximumSampleGapSeconds,SessionSoakPlan.MaximumUiGapSeconds,allowedRetiredPanes=1,allowedRetiredLifetimes=0 } };
        WriteJson(Path.Combine(root,"plan.json"),metadata);
        using var jsonl=new StreamWriter(Path.Combine(root,"samples.jsonl"),false,new UTF8Encoding(false)) { AutoFlush=true };
        using var csv=new StreamWriter(Path.Combine(root,"samples.csv"),false,new UTF8Encoding(false)) { AutoFlush=true };
        csv.WriteLine("utc,phase,seconds,cycles,connected,managedBytes,privateBytes,workingSetBytes,handles,threads,cpuPercent,gapSeconds,forcedGc");
        void Status()=>WriteJson(Path.Combine(root,"status.json"),new { state,seconds=watch.Elapsed.TotalSeconds,requestedSeconds=plan.Seconds,cycle,requestedCycles=plan.Cycles,connected=panes.Count(p=>p.IsConnected),samples,transfers,startedUtc=started,steadyStartedUtc=DateTimeOffset.UtcNow-watch.Elapsed,updatedUtc=DateTimeOffset.UtcNow,maximumGap,maximumUiGap,failure });
        void Sample(string phase,bool forcedGc)
        {
            var seconds=watch.Elapsed.TotalSeconds; var gap=seconds-lastSample; if(state=="RUNNING" && samples>0)maximumGap=Math.Max(maximumGap,gap);
            process.Refresh(); var cpu=process.TotalProcessorTime; var wall=seconds-lastCpuSeconds;
            var cpuPercent=wall>0?SessionSoakPlan.CpuPercent(Math.Max(0,(cpu-lastCpu).TotalSeconds),wall,Environment.ProcessorCount):0;
            var resource=Resources(); var utc=DateTimeOffset.UtcNow;
            var sample=new { utc,phase,seconds,cycle,connected=panes.Count(p=>p.IsConnected),resources=resource,cpuPercent,gapSeconds=gap,maximumUiGap,forcedGc };
            jsonl.WriteLine(JsonSerializer.Serialize(sample));
            csv.WriteLine(FormattableString.Invariant($"{utc:O},{phase},{seconds:F3},{cycle},{sample.connected},{resource.ManagedBytes},{resource.PrivateBytes},{resource.WorkingSetBytes},{resource.Handles},{resource.Threads},{cpuPercent:F3},{gap:F3},{forcedGc}"));
            lastCpu=cpu; lastCpuSeconds=seconds; lastSample=seconds; samples++; Status();
        }
        Status();
        try
        {
            Warmup(main,repo,factory,profile,local,remote); Collect(); baseline=Resources(); Sample("warm-empty-baseline",true);
            for(var i=0;i<SessionSoakPlan.Sessions;i++)panes.Add(Open(main,repo,factory,profile,i));
            Monitoring(main,panes[0]); Collect();
            state="RUNNING"; watch.Start(); heartbeat.Start(); lastCpu=process.TotalProcessorTime; lastCpuSeconds=0; lastSample=0; Sample("steady-start",true);
            while(watch.Elapsed.TotalSeconds<plan.Seconds)
            {
                if(File.Exists(Path.Combine(root,"cancel.request")))throw new OperationCanceledException("Soak cancellation requested.");
                if(panes.Any(p=>!p.IsConnected))throw new Exception("An owned soak session disconnected unexpectedly.");
                if(cycle<plan.Cycles && watch.Elapsed.TotalSeconds>=plan.CycleDueSeconds(cycle))
                {
                    retired.Add(Replace(main,repo,factory,profile,panes,cycle)); cycle++;
                    if(cycle%5==0 || plan.Cycles<5) { Transfer(main,panes[cycle%panes.Count],local,remote,cycle); transfers++; Monitoring(main,panes[0]); }
                    if(cycle%10==0) { var pane=panes[cycle%panes.Count]; main.DockSession(pane,SessionDock.Floating); RedesignChecks.Frames(2); main.DockSession(pane,SessionDock.Primary); }
                }
                if(watch.Elapsed.TotalSeconds>=nextEcho) { foreach(var pane in panes)Echo(main,pane); nextEcho=watch.Elapsed.TotalSeconds+60; }
                if(watch.Elapsed.TotalSeconds>=nextSample) { Sample("periodic",false); nextSample=watch.Elapsed.TotalSeconds+plan.SampleSeconds; }
                Avalonia.Threading.Dispatcher.UIThread.RunJobs(); Thread.Sleep(100);
            }
            maximumUiGap=Math.Max(maximumUiGap,watch.Elapsed.TotalSeconds-lastUiBeat);
            Sample("steady-end",false);
            completionFailures=plan.CompletionFailures(watch.Elapsed.TotalSeconds,cycle,maximumGap,maximumUiGap);
        }
        catch(Exception ex)
        {
            failure=ex.GetType().Name; state=ex is OperationCanceledException?"CANCELLED":"FAILED";
            File.WriteAllText(Path.Combine(root,"failure.txt"),ex.ToString());
        }
        finally
        {
            heartbeat.Stop();
            state=state=="RUNNING"?"CLEANUP":state;
            try
            {
                Close(main,panes); panes.Clear(); Collect(); final=Resources(); Sample("closed",true);
                if(MainWindow.TransferSlots.CurrentCount!=2)throw new Exception("Soak retained transfer slots.");
                retainedFailures=baseline==null?["missing-baseline"]:SessionSoakPlan.RetentionFailures(baseline,final);
                if(retired.Any(p=>p.ReconnectedLifetime.IsAlive || p.ClosedLifetime.IsAlive))retainedFailures=retainedFailures.Append("retired-connection-lifetimes").ToArray();
                if(retired.Count(p=>p.Pane.IsAlive)>1)retainedFailures=retainedFailures.Append("retired-session-controls").ToArray();
                foreach(var entry in Result(inspection.ListAsync(remote,default)))
                {
                    if(entry.IsDirectory || entry.IsSymbolicLink || !entry.FullPath.StartsWith(remote+"/",StringComparison.Ordinal))throw new Exception("Unexpected entry in owned soak scratch.");
                    Wait(inspection.DeleteAsync(entry,default));
                }
                Wait(inspection.DeleteAsync(new("owned-soak",remote,true,false,0,DateTime.Now,""),default));
            }
            catch(Exception ex) { failure??="Cleanup:"+ex.GetType().Name; File.WriteAllText(Path.Combine(root,"cleanup-failure.txt"),ex.ToString()); }
            main.Close(); RedesignChecks.Frames(8);
        }
        var passed=failure==null && completionFailures.Length==0 && retainedFailures.Length==0;
        state=passed?"PASSED":failure==null?"INVESTIGATE":state=="CANCELLED"?"CANCELLED":"FAILED";
        WriteJson(Path.Combine(root,"result.json"),new { passed,state,plan.IsFourHourPlan,elapsedSeconds=watch.Elapsed.TotalSeconds,cycles=cycle,samples,transfers,maximumGap,maximumUiGap,baseline,final,completionFailures,retainedFailures,
            retiredPanesAlive=retired.Count(p=>p.Pane.IsAlive),retiredLifetimesAlive=retired.Count(p=>p.ReconnectedLifetime.IsAlive)+retired.Count(p=>p.ClosedLifetime.IsAlive),failure,finishedUtc=DateTimeOffset.UtcNow,scratch });
        Status(); Console.WriteLine(JsonSerializer.Serialize(new { state,cycles=cycle,elapsedSeconds=watch.Elapsed.TotalSeconds,samples,transfers,completionFailures,retainedFailures }));
        if(state=="CANCELLED")Environment.ExitCode=2;
        else if(!passed)throw new Exception("Owned session soak did not pass; inspect result.json and failure records.");
    }
}
