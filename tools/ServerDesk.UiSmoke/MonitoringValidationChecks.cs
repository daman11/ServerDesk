using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class MonitoringValidationChecks
{
    private const string Sample="""
        __SYSTEM__
        fixture
        NAME=Fixture Linux
        Linux 1 x86_64 GNU/Linux
        fixture
        86400.0 0
        1
        CPU: Fixture CPU
        __CPU__
        cpu 10 0 10 80
        __MEM__
        MemTotal: 1000 kB
        MemAvailable: 600 kB
        SwapTotal: 0 kB
        SwapFree: 0 kB
        __DISKS__
        disk ext4 1000 400 500 NaN% /
        __IPJSON__
        [{"ifname":"eth0","operstate":"UP","addr_info":[{"family":"inet","local":"not-an-ip","prefixlen":24}]}]
        __ROUTES__
        [{"gateway":"broken","dev":"eth0"}]
        __ROUTETEXT__
        default via 192.0.2.1 dev eth0
        __DNS__
        nameserver 192.0.2.53
        __LOAD__
        0.1 0.2 0.3 1/100 1
        __TOPOLOGY__
        0,0,0
        __TRAFFIC__
        eth0: 100 0 0 0 0 0 0 0 20 0 0 0 0 0 0 0
        __CLOCK__
        12:34
        """;
    private sealed class NoVault:ICredentialStore
    {
        internal int Calls;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected fixture vault read"); }
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected fixture vault write"); }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected fixture vault delete"); }
    }
    private static T Field<T>(SessionPane pane,string name)=>(T)typeof(SessionPane).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pane)!;
    private static void Render(SessionPane pane,ServerSnapshot snapshot)=>typeof(SessionPane).GetMethod("RenderMonitoringSnapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pane,[snapshot]);
    private static void Verify(bool condition,string message) { if(!condition)throw new Exception(message); }
    internal static void Run(string root)
    {
        var language=AppText.Language;
        try
        {
            foreach(var chosen in new[]{"pl","en"})Check(root,chosen);
        }
        finally { AppText.SetLanguage(language); DesignSystem.Apply("dark"); }
        Console.WriteLine("PASS monitoring validation PL/EN dark/light: malformed disk is unavailable, CPU/RAM retained, invalid IPs absent, valid gateway fallback, finite progress values and recovery in panel/bottom bar; injected snapshots, no SSH or OS vault");
        File.WriteAllText(Path.Combine(root,"result.txt"),"PASS PL/EN dark/light partial and recovered monitoring presentation with injected snapshots. No SSH or OS credential access.\n");
    }
    private static void Check(string root,string language)
    {
        AppText.SetLanguage(language); var repo=new ProfileRepository(new AppPaths(Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N")))); RedesignChecks.Wait(repo.InitializeAsync());
        RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults with { Preferences=new() { Language=language } }));
        var vault=new NoVault(); var owner=new MainWindow(new(repo),repo,vault,new(repo),new ExternalRdpClient());
        var pane=new SessionPane(owner,new() { Name="Disposable monitoring fixture",Host="fixture.invalid",Username="fixture" },new(repo),new ExternalRdpClient(),repo);
        // Exercise only the real presentation controls. Do not fake a connected
        // transport or start ShowAsync/monitoring/background SSH work.
        Field<Control>(pane,"terminal").IsVisible=false; Field<Control>(pane,"files").IsVisible=false; Field<Control>(pane,"info").IsVisible=true;
        var bar=new ServerMetricsBar(); var layout=new Grid { RowDefinitions=new("*,Auto"),Children={pane,bar} }; Grid.SetRow(bar,1);
        var window=new Window { Width=900,Height=760,Content=layout }; window.Show(); RedesignChecks.Frames(4);
        try
        {
            foreach(var theme in new[]{"dark","light"})
            {
                DesignSystem.Apply(theme); var parser=new MonitoringSampleParser(); parser.Parse(Sample,0);
                var partial=parser.Parse(Sample.Replace("cpu 10 0 10 80","cpu 20 0 20 160"),1);
                Render(pane,partial); bar.Render(partial,DateTimeOffset.UtcNow,"",true,900); RedesignChecks.Frames(4);
                Verify(Field<TextBlock>(pane,"metricsNotice").IsVisible && Field<StackPanel>(pane,"diskRows").Children.Count==0,"Invalid disk data lost its partial-data warning");
                Verify(Field<TextBlock>(pane,"disks").Text==AppText.T("Dane dysków niedostępne"),"Raw malformed df output is presented as a usable disk metric");
                Verify(Field<ProgressBar>(pane,"cpuBar").IsVisible && Field<ProgressBar>(pane,"ramBar").IsVisible,"Invalid disk data discarded CPU/RAM");
                Verify(!Field<StackPanel>(pane,"networkRows").GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("not-an-ip")==true),"Invalid address reached the monitoring UI");
                Verify(Field<TextBlock>(pane,"networkSummary").Text!.Contains("192.0.2.1 · eth0"),"Valid text gateway did not reach monitoring");
                Verify(bar.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="Metric_disk").Text=="/: —","Unavailable root disk looks like zero usage");
                window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"partial-{language}-{theme}.png"));
                var recovered=parser.Parse(Sample.Replace("NaN%","45%").Replace("not-an-ip","192.0.2.5").Replace("cpu 10 0 10 80","cpu 30 0 30 240"),1);
                Render(pane,recovered); bar.Render(recovered,DateTimeOffset.UtcNow,"",true,900); RedesignChecks.Frames(4);
                Verify(!Field<TextBlock>(pane,"metricsNotice").IsVisible && Field<StackPanel>(pane,"diskRows").Children.Count==1,"Recovery did not restore the disk card or clear warning");
                Verify(bar.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="Metric_disk").Text=="/: 45%","Recovered root-disk value missing");
                Verify(pane.GetVisualDescendants().OfType<ProgressBar>().All(p=>double.IsFinite(p.Value)),"Nonfinite progress value reached the controls");
                window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"recovered-{language}-{theme}.png"));
            }
            Verify(!pane.IsConnected && vault.Calls==0,"Presentation fixture accessed a real transport or credential store");
        }
        finally { RedesignChecks.Wait(pane.CloseAsync()); window.Close(); owner.Close(); RedesignChecks.Frames(4); }
    }
}
