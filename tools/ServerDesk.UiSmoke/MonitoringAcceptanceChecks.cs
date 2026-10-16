using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class MonitoringAcceptanceChecks
{
    private static void Wait(Task work)
    {
        var end=DateTime.UtcNow.AddSeconds(40); while(!work.IsCompleted && DateTime.UtcNow<end)RedesignChecks.Frames(1);
        if(!work.IsCompleted)throw new TimeoutException("Monitoring acceptance operation did not finish"); work.GetAwaiter().GetResult();
    }
    private static T Result<T>(Task<T> work) { Wait(work); return work.Result; }
    private static T Field<T>(SessionPane pane,string name)=>(T)typeof(SessionPane).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(pane)!;
    private static void Render(SessionPane pane,ServerSnapshot snapshot)=>typeof(SessionPane).GetMethod("RenderMonitoringSnapshot",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(pane,[snapshot]);
    internal static void Run(string root,string host,string key)
    {
        var repo=new ProfileRepository(new AppPaths(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N")))); Wait(repo.InitializeAsync());
        var factory=new SshConnectionFactory(repo); var main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(new AppPaths(Path.Combine(root,"credentials"))),factory,new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
        factory.ConfirmHostKey=(_,_)=>Task.FromResult(true); main.PreviewPreferences(main.Preferences with { ShowMetrics=false });
        var profile=new ServerProfile { Name="Monitoring fixture",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        using var shell=Result(factory.SshAsync(profile,"serverdesk-test-passphrase",default));
        using var files=new SftpSession(Result(factory.SftpAsync(profile,"serverdesk-test-passphrase",default)));
        var remote="/tmp/serverdesk-monitoring-"+Guid.NewGuid().ToString("N"); Wait(files.CreateDirectoryAsync(remote,default));
        string Execute(string commandText)
        { using var command=shell.CreateCommand(commandText); Wait(command.ExecuteAsync(default)); if(command.ExitStatus!=0)throw new Exception("Owned monitoring fixture command failed"); return command.Result; }
        try
        {
            Execute("set -eu; mkdir '"+remote+"/bin'; for app in cat head hostname uname id getconf sed date; do tool=$(command -v \"$app\"); ln -s \"$tool\" '"+remote+"/bin/'\"$app\"; done; printf denied > '"+remote+"/restricted'; chmod 000 '"+remote+"/restricted'");
            using var normal=new LinuxMonitoring(Result(factory.SshAsync(profile,"serverdesk-test-passphrase",default)));
            var complete=Result(normal.SampleAsync(default)); RedesignChecks.Frames(5); complete=Result(normal.SampleAsync(default));
            if(!complete.CpuAvailable || complete.CpuPercent==null || complete.Memory.Total<=0 || complete.UnavailableSections.Count>0)throw new Exception("Normal fixture metrics are incomplete");
            var minimalText=Execute("PATH='"+remote+"/bin'; "+LinuxMonitoring.Command);
            var minimal=new MonitoringSampleParser().Parse(minimalText,1);
            if(!minimal.CpuAvailable || minimal.Memory.Total!=complete.Memory.Total || minimal.DiskDetails.Count!=0 || minimal.Topology!=null || minimal.NetworkDetails.Interfaces.Count!=0)throw new Exception("Missing command fixture did not retain CPU/RAM or report missing details");
            if(!minimal.UnavailableSections.Contains(MonitoringSection.Disks) || !minimal.UnavailableSections.Contains(MonitoringSection.Addresses))throw new Exception("Missing commands were not reported");
            var deniedText=Execute(LinuxMonitoring.Command.Replace("/proc/stat",remote+"/restricted"));
            var denied=new MonitoringSampleParser().Parse(deniedText,1);
            if(denied.CpuAvailable || denied.Memory.Total!=complete.Memory.Total || denied.DiskDetails.Count==0)throw new Exception("Denied CPU file discarded valid memory/disks");
            var noMemory=new MonitoringSampleParser().Parse(Execute(LinuxMonitoring.Command.Replace("/proc/meminfo",remote+"/restricted")),1);
            if(!noMemory.CpuAvailable || noMemory.Memory.Total!=0 || !noMemory.UnavailableSections.Contains(MonitoringSection.Memory))throw new Exception("Denied memory file looks like valid zero RAM");
            Console.WriteLine("PASS real OpenSSH monitoring with minimal PATH (no df/ip/lscpu) and separate permission-denied CPU/memory files; other sections survive");
            foreach(var language in new[]{"pl","en"})
            {
                AppText.SetLanguage(language); using var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo);
                var bar=new ServerMetricsBar(); var grid=new Grid { RowDefinitions=new("*,Auto") }; grid.Children.Add(pane); Grid.SetRow(bar,1); grid.Children.Add(bar);
                var window=new Window { Width=900,Height=760,Content=grid }; window.Show();
                Wait(pane.ConnectAsync("serverdesk-test-passphrase",default)); Wait(pane.ShowAsync(2));
                foreach(var theme in new[]{"dark","light"})foreach(var width in new[]{650,900})
                {
                    DesignSystem.Apply(theme); window.Width=width; Render(pane,denied); bar.Render(denied,DateTimeOffset.UtcNow,"",true,width); RedesignChecks.Frames(4);
                    var notice=Field<TextBlock>(pane,"metricsNotice");
                    if(!notice.IsVisible || !notice.Text!.Contains(language=="pl"?"Dane niedostępne":"Unavailable") || Field<ProgressBar>(pane,"cpuBar").IsVisible || Field<Viewbox>(pane,"cpuChartHost").IsVisible || !Field<ProgressBar>(pane,"ramBar").IsVisible)throw new Exception("Partial metrics warning or availability UI is wrong");
                    var point=notice.TranslatePoint(default,window)!.Value; if(point.X<0 || point.X+notice.Bounds.Width>window.ClientSize.Width+1)throw new Exception("Monitoring notice overflows narrow window");
                    window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"monitoring-{language}-{theme}-{width}.png"));
                }
                Render(pane,noMemory); bar.Render(noMemory,DateTimeOffset.UtcNow,"",true,900); RedesignChecks.Frames(2);
                if(bar.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="Metric_ram").Text!="RAM —" || Field<ProgressBar>(pane,"ramBar").IsVisible)throw new Exception("Unavailable RAM rendered fake numbers");
                Render(pane,complete); if(Field<TextBlock>(pane,"metricsNotice").IsVisible || !Field<ProgressBar>(pane,"cpuBar").IsVisible)throw new Exception("Recovered metrics did not restore normal UI");
                Wait(pane.CloseAsync()); if(pane.LatestSnapshot!=null || Field<ProgressBar>(pane,"ramBar").IsVisible || Field<StackPanel>(pane,"diskRows").Children.Count!=0)throw new Exception("Close retained old visible metrics"); window.Close(); RedesignChecks.Frames(2);
            }
            Console.WriteLine("PASS partial/recovered/cleared metrics UI and bottom bar in PL/EN, dark/light and 650/900px; missing RAM is unavailable rather than zero");
            Recovery(main,repo,factory,profile);
        }
        finally
        {
            AppText.SetLanguage("pl"); DesignSystem.Apply("dark"); main.Close(); RedesignChecks.Frames();
            foreach(var entry in Result(files.ListAsync(remote+"/bin",default)))
            { if(!entry.IsSymbolicLink || !entry.FullPath.StartsWith(remote+"/bin/",StringComparison.Ordinal))throw new Exception("Unexpected owned fixture entry"); Wait(files.DeleteAsync(entry,default)); }
            Wait(files.DeleteAsync(new("bin",remote+"/bin",true,false,0,DateTime.Now,""),default));
            foreach(var entry in Result(files.ListAsync(remote,default)))
            { if(entry.IsDirectory || !entry.FullPath.StartsWith(remote+"/",StringComparison.Ordinal))throw new Exception("Unexpected fixture entry"); Wait(files.DeleteAsync(entry,default)); }
            Wait(files.DeleteAsync(new("fixture",remote,true,false,0,DateTime.Now,""),default));
        }
    }
    private static void Recovery(MainWindow main,ProfileRepository repo,SshConnectionFactory factory,ServerProfile direct)
    {
        AppText.SetLanguage("pl"); using var proxy=new SftpFaultProxy(direct.Host,direct.Port);
        using var pane=new SessionPane(main,direct with { Host="127.0.0.1",Port=proxy.Port },factory,new ExternalRdpClient(),repo);
        var window=new Window { Width=900,Height=650,Content=pane }; window.Show();
        void Until(Func<bool> ready,string message)
        { var end=DateTime.UtcNow.AddSeconds(45); while(!ready() && DateTime.UtcNow<end)RedesignChecks.Frames(1); if(!ready())throw new TimeoutException(message); }
        try
        {
            Wait(pane.ConnectAsync("serverdesk-test-passphrase",default)); pane.IsActive=true;
            var next=proxy.ArmNext(); Wait(pane.ShowAsync(2)); var relay=Result(next);
            Until(()=>pane.LatestSnapshot!=null,"Initial recovery fixture sample missing");
            relay.Dispose(); Until(()=>pane.MetricsError.Length>0,"Lost monitoring connection was not reported");
            if(!pane.IsConnected || pane.LatestSnapshot!=null || Field<ProgressBar>(pane,"ramBar").IsVisible)throw new Exception("Monitoring loss poisoned terminal or retained old values");
            Until(()=>pane.LatestSnapshot is { CpuAvailable:true,Memory.Total:>0 } && pane.MetricsError.Length==0,"Monitoring did not recover its own connection after backoff");
            if(!pane.IsConnected)throw new Exception("Recovering monitoring disconnected the terminal");
            Console.WriteLine("PASS dropped monitoring-only SSH connection: old values cleared, terminal stays connected, new monitoring client recovers automatically after backoff");
        }
        finally { Wait(pane.CloseAsync()); window.Close(); RedesignChecks.Frames(3); }
    }
}
