using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class LargeFileViewChecks
{
    private static T Field<T>(object target,string name)=>(T)typeof(SessionPane).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static Task Call(SessionPane pane,string name)=>(Task)typeof(SessionPane).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pane,null)!;
    private static void Until(Func<bool> done)
    {
        var timeout=Stopwatch.StartNew();
        while(!done()) { if(timeout.Elapsed>TimeSpan.FromSeconds(30))throw new TimeoutException("Large file view did not settle"); Dispatcher.UIThread.RunJobs(); Thread.Sleep(5); }
    }
    private static void Settle(SessionPane pane)
    {
        RedesignChecks.Frames(2); Until(()=>Field<HashSet<Task>>(pane,"fileViewTasks").Count==0);
        Field<Task>(pane,"fileViewUpdate").GetAwaiter().GetResult();
    }

    internal static void Run(string root)
    {
        var repo=new ProfileRepository(new AppPaths(Path.Combine(root,"fixture-"+Guid.NewGuid().ToString("N")))); RedesignChecks.Wait(repo.InitializeAsync());
        var main=new MainWindow(new(repo),repo,new CredentialStore(new AppPaths(Path.Combine(root,"credentials"))),new SshConnectionFactory(repo),new ExternalRdpClient());
        main.Show(); RedesignChecks.Frames(); var results=new List<object>();
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); DesignSystem.Apply(language=="pl"?"graphite":"light");
            using var pane=new SessionPane(main,new() { Name="Large file fixture",Host="fixture.invalid",Username="fixture" },new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
            pane.Children.OfType<Iciclecreek.Terminal.TerminalControl>().Single().IsVisible=false; Field<Grid>(pane,"files").IsVisible=true;
            var entries=Enumerable.Range(0,50000).Reverse().Select(i=>new FileEntry("file"+i+".txt","/fixture/file"+i+".txt",false,false,i,new DateTime(2026,1,1).AddSeconds(i),"644")).ToList();
            entries.Add(new("..","/",true,false,0,DateTime.MinValue,"")); entries.Add(new(".hidden","/fixture/.hidden",false,false,0,DateTime.MinValue,"644"));
            Field<List<FileEntry>>(pane,"localEntries").AddRange(entries); Field<List<FileEntry>>(pane,"remoteEntries").AddRange(entries);
            var window=new Window { Width=1320,Height=800,Content=pane }; window.Show(); RedesignChecks.Frames(2);
            var local=Field<ListBox>(pane,"localList"); var remote=Field<ListBox>(pane,"remoteList");
            var clock=Stopwatch.StartNew(); var initial=Call(pane,"FilterFilesAsync"); var returnMs=clock.Elapsed.TotalMilliseconds;
            if(initial.IsCompleted || returnMs>100)throw new Exception($"Large sort blocks dispatcher: {returnMs:F1}ms");
            var heartbeat=false; Dispatcher.UIThread.Post(()=>heartbeat=true); Dispatcher.UIThread.RunJobs();
            if(!heartbeat || !pane.IsBusy)throw new Exception("Dispatcher/loading state missing during sort");
            RedesignChecks.Wait(initial); Settle(pane); var readyMs=clock.Elapsed.TotalMilliseconds;
            if(local.ItemCount!=50001 || remote.ItemCount!=50001 || local.Items.OfType<FileEntry>().Skip(1).First().Name!="file0.txt")throw new Exception("Large view truncated or incorrectly sorted");
            local.SelectedItem=local.Items.OfType<FileEntry>().Single(e=>e.Name=="file43210.txt");
            var gate=Field<SemaphoreSlim>(pane,"fileViewGate"); gate.Wait();
            try
            {
                for(var i=0;i<30;i++) { Field<TextBox>(pane,"localFilter").Text="file"+i; _=Call(pane,"FilterFilesAsync"); }
                Field<TextBox>(pane,"localFilter").Text="file43210"; _=Call(pane,"FilterFilesAsync");
                RedesignChecks.Frames(6);
                if(local.ItemCount!=50001)throw new Exception("A waiting request published an incomplete view");
            }
            finally { gate.Release(); }
            Settle(pane);
            if(local.ItemCount!=2 || local.SelectedItem is not FileEntry { Name:"file43210.txt" } || remote.ItemCount!=50001)throw new Exception("Latest filter/selection/panel independence failed");
            Field<ComboBox>(pane,"remoteSort").SelectedIndex=1; Settle(pane);
            typeof(SessionPane).GetField("remoteDescending",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(pane,true); RedesignChecks.Wait(Call(pane,"FilterFilesAsync")); Settle(pane);
            if(remote.Items.OfType<FileEntry>().Skip(1).First().Name!="file49999.txt" || local.ItemCount!=2)throw new Exception("Background size direction affected other panel");
            Field<TextBox>(pane,"localFilter").Text="no-such-file"; Settle(pane); if(local.ItemCount!=1)throw new Exception("Empty filter must retain parent");
            Field<TextBox>(pane,"localFilter").Text="file43210"; Settle(pane); window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"large-files-{language}.png"));
            // A small replacement directory must win over a queued large result.
            gate.Wait();
            try
            {
                var pending=Call(pane,"FilterFilesAsync"); RedesignChecks.Frames(6);
                foreach(var name in new[]{"localEntries","remoteEntries"}) { var list=Field<List<FileEntry>>(pane,name); list.Clear(); list.Add(new("replacement","/next/replacement",false,false,1,DateTime.UtcNow,"644")); }
                Field<TextBox>(pane,"localFilter").Text=""; RedesignChecks.Wait(Call(pane,"FilterFilesAsync"));
            }
            finally { gate.Release(); }
            Settle(pane); if(local.ItemCount!=1 || local.Items.OfType<FileEntry>().Single().Name!="replacement")throw new Exception("Stale result overwrote replacement directory");
            Field<List<FileEntry>>(pane,"localEntries").AddRange(entries); _=Call(pane,"FilterFilesAsync");
            clock.Restart(); RedesignChecks.Wait(pane.CloseAsync()); var closeMs=clock.Elapsed.TotalMilliseconds;
            if(Field<HashSet<Task>>(pane,"fileViewTasks").Count!=0 || closeMs>2000)throw new Exception("Close did not drain cancelled file views");
            window.Close(); results.Add(new { Language=language,EntriesPerPanel=50000,RequestReturnMs=returnMs,ReadyMs=readyMs,CloseMs=closeMs });
        }
        File.WriteAllText(Path.Combine(root,"large-file-views.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions { WriteIndented=true }));
        main.Close(); RedesignChecks.Frames(); AppText.SetLanguage("pl");
        Console.WriteLine("PASS large SFTP views: 50000 entries per panel, dispatcher heartbeat, natural order, latest of 30 requests, panel independence, selection, empty filter, stale result suppression, small replacement and close/drain; PL/EN Graphite/Light");
    }
}
