using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using L=ServerDesk.Core.AppText;

internal static class TransferOverviewChecks
{
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static object? Call(object owner,string method,params object[] arguments)=>owner.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,arguments);
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Until(Func<bool> ready)
    { var deadline=DateTime.UtcNow.AddSeconds(45); while(!ready() && DateTime.UtcNow<deadline)RedesignChecks.Frames(1); if(!ready())throw new TimeoutException("Transfer overview timed out"); }
    private static void Section(MainWindow main,string id) { RedesignChecks.Wait((Task)Call(main,"SelectSectionAsync",id)!); RedesignChecks.Frames(); }
    private static Border[] VisibleJobs(MainWindow main)=>Field<StackPanel>(main,"transferRows").Children.OfType<Border>().Where(row=>row.IsVisible).ToArray();
    private static void Close(MainWindow main) { foreach(var pane in main.OpenSessions.ToArray())main.RemovePane(pane); main.Close(); RedesignChecks.Frames(); }

    internal static void Layout(string root)
    {
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            L.SetLanguage(language); var repo=new ProfileRepository(new AppPaths(Path.Combine(root,"layout-"+language))); RedesignChecks.Wait(repo.InitializeAsync());
            var vm=new MainViewModel(repo); var main=new MainWindow(vm,repo,new CredentialStore(new AppPaths(Path.Combine(root,"layout-"+language))),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames();
            vm.Search="saved filter"; vm.ProtocolFilter=Protocol.RDP; vm.SortMode=ProfileSort.Host; Section(main,"transfers");
            if(main.GetVisualDescendants().OfType<Control>().Any(c=>c.IsEffectivelyVisible && (c.Name is "ConnectionBrowserTools" or "ConnectionResultCount" || c is TreeView || c is Button b && (AutomationProperties.GetName(b)?.StartsWith(L.T("Filtr protokołu: "))==true || AutomationProperties.GetName(b)==L.T("Dodaj folder")))))throw new Exception("Transfers still exposes connection-only controls");
            if(!Field<StackPanel>(main,"transferEmpty").IsEffectivelyVisible || VisibleJobs(main).Length!=0 || Field<ComboBox>(main,"transferFilter").ItemCount!=4)throw new Exception("Empty transfer view or status filters missing");
            foreach(var theme in new[]{"graphite","light"})
            {
                DesignSystem.Apply(theme); main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"transfers-empty-{language}-{theme}.png"));
            }
            Field<TextBox>(main,"transferSearch").Text="transfer-only"; Field<ComboBox>(main,"transferFilter").SelectedIndex=1; Section(main,"connections");
            if(vm.Search!="saved filter" || vm.ProtocolFilter!=Protocol.RDP || vm.SortMode!=ProfileSort.Host || !Field<StackPanel>(main,"connectionTools").IsEffectivelyVisible)throw new Exception("Transfer filters overwrote the connection browser state");
            Section(main,"transfers"); if(Field<TextBox>(main,"transferSearch").Text!="transfer-only" || Field<ComboBox>(main,"transferFilter").SelectedIndex!=1)throw new Exception("Transfer filters were lost on navigation");
            foreach(var protocol in new[]{Protocol.RDP,Protocol.SSH,Protocol.VNC,Protocol.Serial})
            {
                using var listener=new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback,0); listener.Start(); var probePort=((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
                using var pane=new SessionPane(main,new() { Protocol=protocol,Name="Operations / bardzo długa nazwa serwera",Host=protocol==Protocol.RDP?"127.0.0.1":"server-for-layout.invalid",Port=protocol==Protocol.RDP?probePort:22,Username="admin" },new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
                pane.PrepareRestored(protocol==Protocol.SSH?1:0);
                var w=new Window { Width=1000,Height=580,Content=pane }; w.Show();
                foreach(var (width,height) in new[]{(1000,580),(440,300)})
                {
                    w.Width=width; w.Height=height; DesignSystem.ApplyPreferences(preferences with { InterfaceFontSize=16 }); RedesignChecks.Frames();
                    var notice=Field<ScrollViewer>(pane,"restoredNotice");
                    if(!notice.IsEffectivelyVisible || pane.HasAttemptedConnection || pane.HasActiveClient || Field<Grid>(pane,"files").IsVisible || Field<ScrollViewer>(pane,"info").IsVisible)throw new Exception("Restored session opens live content");
                    if(protocol==Protocol.RDP && Field<Control>(pane,"rdpNotice").IsEffectivelyVisible || protocol is Protocol.VNC or Protocol.Serial && Field<Border>(pane,"additionalNotice").IsEffectivelyVisible)throw new Exception("Protocol notice overlaps restored card");
                    var card=pane.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="RestoredSessionCard");
                    var labels=card.GetVisualDescendants().OfType<TextBlock>().ToArray();
                    var rects=labels.Select(label=>new Rect(label.TranslatePoint(default,card)!.Value,label.Bounds.Size)).ToArray();
                    for(var i=0;i<rects.Length;i++)for(var j=i+1;j<rects.Length;j++)if(rects[i].Intersects(rects[j]))throw new Exception("Restored card text overlaps");
                    if(rects.Any(r=>r.Left<0 || r.Right>card.Bounds.Width+1))throw new Exception("Restored text escapes horizontally");
                    if(notice.Extent.Width>notice.Viewport.Width+1)throw new Exception("Restored card requires horizontal scrolling");
                    w.CaptureRenderedFrame()!.Save(Path.Combine(root,$"restored-{protocol}-{language}-{width}.png"));
                    if(width==440) { notice.Offset=new Vector(0,notice.Extent.Height); RedesignChecks.Frames(); var bottom=labels.Last().TranslatePoint(default,notice)!.Value.Y+labels.Last().Bounds.Height; if(bottom>notice.Viewport.Height+1)throw new Exception("Restored instructions cannot be reached by scrolling"); }
                }
                if(protocol==Protocol.RDP)
                {
                    var rejectProbe=Task.Run(async()=> { using var client=await listener.AcceptTcpClientAsync(); client.Close(); });
                    var check=(Task)Call(pane,"CheckRdpAsync")!; try { RedesignChecks.Wait(check); throw new Exception("Invalid RDP fixture unexpectedly passed"); } catch(Exception) when(check.IsFaulted) { }
                    RedesignChecks.Wait(rejectProbe); RedesignChecks.Frames();
                    if(Field<ScrollViewer>(pane,"restoredNotice").IsVisible || !Field<Control>(pane,"rdpNotice").IsEffectivelyVisible || pane.HasActiveClient || pane.HasAttemptedConnection)throw new Exception("Manual RDP diagnostic hidden behind restoration or launched a session");
                }
                w.Close();
            }
            Close(main);
        }
        L.SetLanguage("pl"); DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark");
        Console.WriteLine("PASS restored SSH/RDP/VNC/Serial cards: no stacked protocol notice, pairwise text geometry, wrapping/scroll reachability at 1000x580 and 440x300, font16 PL/EN; separate transfer search/status/empty state, no connection controls, preserved browser filters");
    }

    internal static void Network(MainWindow main,ProfileRepository repo,string root,string host,string key)
    {
        var profile=new ServerProfile { Name="SSH transfer overview",Host=host,Port=22222,Username="damian",Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
        using var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo); main.RegisterPane(pane); RedesignChecks.Wait(pane.ConnectAsync("serverdesk-test-passphrase",CancellationToken.None)); RedesignChecks.Wait(pane.ShowAsync(1));
        var inspectionConnection=factory.SftpAsync(profile,"serverdesk-test-passphrase",CancellationToken.None); RedesignChecks.Wait(inspectionConnection);
        using var inspection=new SftpSession(inspectionConnection.GetAwaiter().GetResult());
        var remote="/tmp/serverdesk-overview-"+Guid.NewGuid().ToString("N"); RedesignChecks.Wait(inspection.CreateDirectoryAsync(remote,CancellationToken.None));
        var local=Path.Combine(root,"overview-source-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(local);
        Task Upload(string name)
        { var path=Path.Combine(local,name); return (Task)Call(pane,"UploadEntryAsync",new FileEntry(name,path,false,false,System.IO.File.Exists(path)?new FileInfo(path).Length:0,DateTime.Now,""))!; }
        Border Row(string name)=>VisibleJobs(main).Single(row=>row.GetVisualDescendants().OfType<TextBlock>().Any(text=>text.Text?.Contains(name)==true));
        Button Button(Border row,string prefix)=>row.GetVisualDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)?.StartsWith(prefix)==true);
        try
        {
            Field<TextBox>(pane,"localPath").Text=local; Field<TextBox>(pane,"remotePath").Text=remote; RedesignChecks.Wait((Task)Call(pane,"RefreshAsync")!); Section(main,"transfers");
            if(VisibleJobs(main).Length!=0)throw new Exception("SSH session appeared as a transfer without jobs");
            System.IO.File.WriteAllText(Path.Combine(local,"queued.txt"),"Transfer overview żółć");
            RedesignChecks.Wait(MainWindow.TransferSlots.WaitAsync()); RedesignChecks.Wait(MainWindow.TransferSlots.WaitAsync());
            Task queued;
            try
            {
                queued=Upload("queued.txt"); RedesignChecks.Frames(); var row=Row("queued.txt");
                if(!row.GetVisualDescendants().OfType<ProgressBar>().Single().IsIndeterminate)throw new Exception("Queued overview has no pending progress");
                Field<ComboBox>(main,"transferFilter").SelectedIndex=1; var search=Field<TextBox>(main,"transferSearch"); search.Text="queued SSH"; RedesignChecks.Frames();
                if(VisibleJobs(main).Length!=1)throw new Exception("Multiword transfer search or active filter failed");
                search.Focus(); Call(main,"UpdateStatus"); RedesignChecks.Frames(); if(!search.IsKeyboardFocusWithin || !ReferenceEquals(Row("queued.txt"),row))throw new Exception("Progress refresh rebuilt the transfer controls or lost focus");
                Click(Button(row,"Anuluj transfer:")); RedesignChecks.Wait(queued); if(VisibleJobs(main).Length!=0)throw new Exception("Cancelled transfer remains in active filter");
            }
            finally { MainWindow.TransferSlots.Release(2); }
            Field<TextBox>(main,"transferSearch").Text=""; Field<ComboBox>(main,"transferFilter").SelectedIndex=3; RedesignChecks.Frames();
            var cancelled=pane.TransferSnapshots.Single(); if(cancelled.State!=TransferState.Cancelled)throw new Exception("Sidebar cancel did not cancel actual queued work");
            Click(Button(Row("queued.txt"),"Ponów transfer:")); Until(()=>pane.ActiveTransfers==0); RedesignChecks.Frames();
            if(pane.TransferSnapshots.Single().State!=TransferState.Completed || VisibleJobs(main).Length!=0)throw new Exception("Sidebar retry did not complete or leave attention filter");
            RedesignChecks.Wait(Upload("missing.txt")); RedesignChecks.Frames(); if(VisibleJobs(main).Length!=1 || pane.TransferSnapshots.First().State!=TransferState.Failed)throw new Exception("Failed job not visible in attention filter");
            Field<ComboBox>(main,"transferFilter").SelectedIndex=2; RedesignChecks.Frames(); if(VisibleJobs(main).Length!=2)throw new Exception("Finished filter lost a completed or failed job");
            var preferences=DesignSystem.Preferences;
            foreach(var theme in new[]{"graphite","light"})
            {
                DesignSystem.Apply(theme); DesignSystem.ApplyPreferences(preferences with { SidebarWidth=220,InterfaceFontSize=16 }); Field<Grid>(main,"layout").ColumnDefinitions[1].Width=new GridLength(220); RedesignChecks.Frames();
                var scroll=main.GetVisualDescendants().OfType<ScrollViewer>().Single(s=>s.Name=="TransferOverviewScroll"); if(scroll.Extent.Width>scroll.Viewport.Width+1)throw new Exception("Transfer row overflows narrow sidebar");
                if(Field<Grid>(main,"transferOverview").Bounds.Width>221)throw new Exception("Narrow sidebar test did not constrain the panel");
                foreach(var row in VisibleJobs(main))foreach(var label in row.GetVisualDescendants().OfType<TextBlock>()) { var point=label.TranslatePoint(default,row)!.Value; if(point.X<0 || point.X+label.Bounds.Width>row.Bounds.Width+1)throw new Exception("Transfer text escapes the card"); }
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"transfers-results-{theme}.png"));
            }
            DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark"); Field<Grid>(main,"layout").ColumnDefinitions[1].Width=new GridLength(preferences.SidebarWidth);
            Click(Button(Row("queued.txt"),"Pokaż kolejkę:")); if(main.Workspace.Focused!=pane.WorkspaceId || !Field<ScrollViewer>(pane,"queueScroll").IsVisible)throw new Exception("Show queue did not focus source session");
            var result=inspection.ListAsync(remote,CancellationToken.None); RedesignChecks.Wait(result); var file=result.Result.Single(); var download=Path.Combine(local,"check.txt"); RedesignChecks.Wait(inspection.DownloadAsync(file.FullPath,download,false,new Progress<TransferProgress>(),CancellationToken.None));
            if(System.IO.File.ReadAllText(download)!="Transfer overview żółć")throw new Exception("Retry uploaded incorrect payload");
            Click(Field<Button>(main,"clearTransferHistory")); if(pane.TransferSnapshots.Count!=0 || VisibleJobs(main).Length!=0)throw new Exception("Sidebar clear did not update session queue");
            RedesignChecks.Wait(Upload("closed-tab.txt")); RedesignChecks.Frames(); Section(main,"connections"); main.RemovePane(pane);
            if(Field<System.Collections.IDictionary>(main,"transferViews").Count!=0 || Field<StackPanel>(main,"transferRows").Children.Count!=0)throw new Exception("Closed session retained transfer controls and their pane references");
            Console.WriteLine("PASS actual SSH/SFTP transfer overview: session without tasks excluded; queued progress; multiword search/status filters; stable controls/focus; sidebar cancellation and original-task retry; failed/completed tasks; narrow Graphite/Light font16; focus source queue; uploaded UTF-8 payload verified; shared clear history; closed-pane controls released even while overview hidden");
        }
        finally
        {
            var listing=inspection.ListAsync(remote,CancellationToken.None); RedesignChecks.Wait(listing);
            foreach(var entry in listing.Result) { if(entry.IsDirectory || entry.IsSymbolicLink || entry.FullPath!=remote+"/queued.txt")throw new Exception("Unexpected cleanup target"); RedesignChecks.Wait(inspection.DeleteAsync(entry,CancellationToken.None)); }
            RedesignChecks.Wait(inspection.DeleteAsync(new("fixture",remote,true,false,0,DateTime.Now,""),CancellationToken.None)); if(main.OpenSessions.Contains(pane))main.RemovePane(pane);
        }
    }
}
