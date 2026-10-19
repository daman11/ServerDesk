using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SftpNavigationChecks
{
    private static T Field<T>(SessionPane pane,string name)=>(T)typeof(SessionPane).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pane)!;
    private static Task Call(SessionPane pane,string name,params object?[] args)
    {
        try { return (Task)typeof(SessionPane).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pane,args)!; }
        catch(TargetInvocationException e) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(e.InnerException!).Throw(); throw; }
    }
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    private static Task Go(SessionPane pane,bool remote,string path)=>Call(pane,"NavigateFilesAsync",remote,path,null);
    private static Button NavigationButton(SessionPane pane,string name)=>pane.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="Sftp"+name);
    private static void Click(SessionPane pane,string name)
    {
        var button=NavigationButton(pane,name); if(!button.IsEnabled)throw new Exception("Navigation action unexpectedly disabled: "+name);
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var deadline=DateTime.UtcNow.AddSeconds(30); while(Field<int>(pane,"fileRefreshes")>0 && DateTime.UtcNow<deadline)RedesignChecks.Frames(1);
        if(Field<int>(pane,"fileRefreshes")>0)throw new TimeoutException("Navigation button did not settle");
    }
    private static void Fail(Task task)
    {
        try { Wait(task); } catch when(task.IsFaulted) { return; }
        throw new Exception("Invalid directory unexpectedly succeeded");
    }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language)); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
            var main=new MainWindow(new(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames();
            main.PreviewPreferences(main.Preferences with { InterfaceFontSize=16 }); DesignSystem.Apply(language=="pl"?"graphite":"light");
            using var pane=new SessionPane(main,new() { Name="Navigation fixture · long server name",Host="fixture.invalid",Username="fixture" },new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
            pane.Children.OfType<Iciclecreek.Terminal.TerminalControl>().Single().IsVisible=false; Field<Grid>(pane,"files").IsVisible=true;
            var window=new Window { Width=1000,Height=760,Content=pane }; window.Show();
            foreach(var width in new[]{280,520,1000})foreach(var remote in new[]{false,true})
            {
                window.Width=width; Field<ComboBox>(pane,"fileTarget").SelectedIndex=remote?1:0; RedesignChecks.Frames();
                var prefix=remote?"Remote":"Local"; var controls=new[]{"Back","Forward","Up","Home"}.Select(n=>NavigationButton(pane,prefix+n)).ToArray();
                for(var i=0;i<controls.Length;i++)
                {
                    var point=controls[i].TranslatePoint(default,pane)??throw new Exception("Missing navigation geometry");
                    if(point.X<0 || point.X+controls[i].Bounds.Width>pane.Bounds.Width+.5 || controls[i].IsEnabled)throw new Exception("Disconnected navigation clipped or enabled");
                    if(i>0 && controls[i-1].TranslatePoint(default,pane)!.Value.X+controls[i-1].Bounds.Width>point.X+.5)throw new Exception("Overlapping navigation buttons");
                }
                var path=Field<TextBox>(pane,remote?"remotePath":"localPath"); path.Focus();
                SshAcceptanceChecks.Press(window,Key.L,RawInputModifiers.Control); if(!path.IsKeyboardFocusWithin)throw new Exception("Ctrl+L lost active panel");
                window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"navigation-{language}-{prefix}-{width}.png"));
            }
            Wait(pane.CloseAsync()); window.Close(); main.Close(); RedesignChecks.Frames();
        }
        AppText.SetLanguage("pl"); Console.WriteLine("PASS SFTP navigation layout: separate local/remote controls, disabled without connection, Ctrl+L, PL/EN Graphite/Light font16 at 280/520/1000px");
    }
    internal static void Network(string root,string host,string key)
    {
        Console.WriteLine("INFO navigation fixture initialize");
        var paths=new AppPaths(Path.Combine(root,"data")); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
        var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
        var profile=new ServerProfile { Name="Navigation fixture",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var main=new MainWindow(new(repo),repo,new CredentialStore(paths),factory,new ExternalRdpClient()); main.Show();
        factory.ConfirmHostKey=(_,_)=>Task.FromResult(true); // Only this disposable loopback fixture.
        Console.WriteLine("INFO navigation owner ready");
        using var inspection=new SftpSession(Result(factory.SftpAsync(profile,"serverdesk-test-passphrase",default)));
        Console.WriteLine("INFO navigation inspection ready");
        var remote="/tmp/serverdesk-navigation-"+Guid.NewGuid().ToString("N"); var local=Path.GetFullPath(Path.Combine(root,"local"));
        Wait(inspection.CreateDirectoryAsync(remote,default)); foreach(var part in new[]{"a","b","b/child"}) { Directory.CreateDirectory(Path.Combine(local,part)); Wait(inspection.CreateDirectoryAsync(remote+"/"+part,default)); }
        using var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo); var window=new Window { Width=1100,Height=800,Content=pane }; window.Show();
        Console.WriteLine("INFO navigation pane ready");
        try
        {
            Wait(pane.ConnectAsync("serverdesk-test-passphrase",default)); Wait(pane.ShowAsync(1));
            Console.WriteLine("INFO navigation connected");
            var localHistory=Field<DirectoryNavigationHistory>(pane,"localNavigation"); var remoteHistory=Field<DirectoryNavigationHistory>(pane,"remoteNavigation");
            Wait(Go(pane,false,local)); Wait(Go(pane,true,remote)); var remoteCount=remoteHistory.Count;
            Console.WriteLine("INFO navigation roots loaded");
            Wait(Go(pane,false,Path.Combine(local,"a"))); Wait(Go(pane,false,Path.Combine(local,"b")));
            Wait(Call(pane,"NavigateFileHistoryAsync",false,false)); if(localHistory.Current!=Path.Combine(local,"a"))throw new Exception("Local back failed");
            var count=localHistory.Count; Wait(Call(pane,"RefreshAsync")); if(localHistory.Count!=count || localHistory.Forward==null)throw new Exception("Refresh destroyed forward branch");
            Field<ListBox>(pane,"localList").Focus(); SshAcceptanceChecks.Press(window,Key.Right,RawInputModifiers.Alt); RedesignChecks.Frames();
            var deadline=DateTime.UtcNow.AddSeconds(30); while(Field<int>(pane,"fileRefreshes")>0 && DateTime.UtcNow<deadline)RedesignChecks.Frames(1);
            if(Field<int>(pane,"fileRefreshes")>0)throw new TimeoutException("Keyboard navigation did not settle");
            if(localHistory.Current!=Path.Combine(local,"b") || remoteHistory.Count!=remoteCount)throw new Exception("Alt+Right or panel independence failed");
            Console.WriteLine("INFO navigation local keyboard ready");
            Wait(Go(pane,true,remote+"/a")); Wait(Go(pane,true,remote+"/b")); Click(pane,"RemoteBack");
            var before=remoteHistory.Current; count=remoteHistory.Count; Fail(Go(pane,true,remote+"/missing"));
            if(remoteHistory.Current!=before || remoteHistory.Count!=count || remoteHistory.Forward==null || Field<TextBox>(pane,"remotePath").Text!=before)throw new Exception("Failed remote path damaged navigation state");
            Fail(Go(pane,false,Path.Combine(local,"missing"))); if(Field<TextBox>(pane,"localPath").Text!=localHistory.Current)throw new Exception("Failed local path not restored");
            Console.WriteLine("INFO navigation failure rollback ready");
            Click(pane,"RemoteForward"); Wait(Go(pane,true,remote+"/b/child")); Click(pane,"RemoteUp");
            if(remoteHistory.Current!=remote+"/b")throw new Exception("Parent failed");
            var bytes=Encoding.UTF8.GetBytes("żółć – navigation transfer\n"); var source=Path.Combine(local,"b","żółć.txt"); File.WriteAllBytes(source,bytes);
            Wait(Call(pane,"RefreshAsync")); var sourceList=Field<ListBox>(pane,"localList"); sourceList.SelectedItem=sourceList.Items.OfType<FileEntry>().Single(e=>e.Name=="żółć.txt");
            if(!Field<Button>(pane,"uploadSelection").IsEnabled)throw new Exception("Confirmed upload action disabled");
            Field<TextBox>(pane,"remotePath").Text=remote+"/a";
            if(Field<Button>(pane,"uploadSelection").IsEnabled)throw new Exception("Upload action enabled with draft destination");
            var entry=new FileEntry("żółć.txt",source,false,false,bytes.Length,DateTime.Now,""); var rejected=false;
            try { Wait(Call(pane,"UploadFilesAsync",(object)new FileEntry[]{entry})); } catch(InvalidOperationException) { rejected=true; }
            var unexpected=Result(inspection.ListAsync(remote+"/a",default)).Any(e=>e.Name==entry.Name);
            if(!rejected || Field<ListBox>(pane,"remoteList").IsEnabled || unexpected)throw new Exception($"Draft protection failed: rejected={rejected}, listEnabled={Field<ListBox>(pane,"remoteList").IsEnabled}, unexpectedFile={unexpected}");
            Field<TextBox>(pane,"remotePath").Focus(); SshAcceptanceChecks.Press(window,Key.Escape,RawInputModifiers.None);
            if(Field<TextBox>(pane,"remotePath").Text!=remoteHistory.Current || !Field<ListBox>(pane,"remoteList").IsKeyboardFocusWithin)throw new Exception("Escape did not restore the confirmed path/focus");
            Wait(Go(pane,true,remote+"/a")); Wait(Call(pane,"UploadFilesAsync",(object)new FileEntry[]{entry}));
            var downloaded=Path.Combine(local,"roundtrip.txt"); Wait(inspection.DownloadAsync(remote+"/a/żółć.txt",downloaded,false,new Progress<TransferProgress>(),default));
            if(!File.ReadAllBytes(downloaded).SequenceEqual(bytes))throw new Exception("Confirmed destination roundtrip differs");
            Console.WriteLine("INFO navigation roundtrip ready");
            var gate=Field<SemaphoreSlim>(pane,"refreshGate"); gate.Wait(); var prior=localHistory.Count; Task stale,latest;
            try {
                stale=Go(pane,false,Path.Combine(local,"a")); latest=Go(pane,false,local);
                if(NavigationButton(pane,"LocalBack").IsEnabled || Field<ListBox>(pane,"localList").IsEnabled)throw new Exception("Navigation remained actionable while loading");
            } finally { gate.Release(); }
            Wait(Task.WhenAll(stale,latest)); if(localHistory.Current!=local || localHistory.Count!=prior+1)throw new Exception("Obsolete queued navigation committed");
            Click(pane,"RemoteHome"); if(remoteHistory.Current!=Field<string>(pane,"remoteHome"))throw new Exception("Remote home differs from login directory");
            Wait(Call(pane,"NavigateFileHomeAsync",false)); if(localHistory.Current!=Path.TrimEndingDirectorySeparator(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))throw new Exception("Local home failed");
            var savedLocalCount=localHistory.Count; var savedRemoteCount=remoteHistory.Count;
            var savedLocalPath=localHistory.Current; var savedRemotePath=remoteHistory.Current;
            Wait(pane.ConnectAsync("serverdesk-test-passphrase",default));
            if(localHistory.Count!=savedLocalCount || remoteHistory.Count!=savedRemoteCount || localHistory.Current!=savedLocalPath || remoteHistory.Current!=savedRemotePath)throw new Exception("Reconnect discarded confirmed navigation context");
            if(NavigationButton(pane,"RemoteBack").IsEnabled || NavigationButton(pane,"LocalBack").IsEnabled)throw new Exception("Cached navigation enabled before a fresh SFTP connection");
            Console.WriteLine("INFO navigation reconnected");
            Wait(pane.ShowAsync(1));
            if(localHistory.Count!=savedLocalCount || remoteHistory.Count!=savedRemoteCount || localHistory.Current!=savedLocalPath || remoteHistory.Current!=savedRemotePath)throw new Exception("Fresh SFTP refresh changed confirmed navigation context");
            window.CaptureRenderedFrame()!.Save(Path.Combine(root,"navigation-network.png"));
            Console.WriteLine("PASS real OpenSSH/SFTP navigation: back/forward and Alt+Right, refresh/branch preservation, local/remote independence, failed-path rollback, parent/home, rejected unconfirmed upload, UTF8 roundtrip, queued stale suppression and confirmed context retained across reconnect with actions gated until SFTP opens");
        }
        finally
        {
            Console.WriteLine("INFO navigation cleanup start");
            Wait(pane.CloseAsync()); window.Close(); main.Close();
            Console.WriteLine("INFO navigation UI closed");
            async Task Cleanup(string path)
            {
                if(path!=remote && !path.StartsWith(remote+"/",StringComparison.Ordinal))throw new Exception("Unsafe fixture cleanup");
                foreach(var file in await inspection.ListAsync(path,default)) { if(file.IsDirectory && !file.IsSymbolicLink)await Cleanup(file.FullPath); await inspection.DeleteAsync(file,default); }
            }
            Wait(Task.Run(()=>Cleanup(remote))); Wait(inspection.DeleteAsync(new("fixture",remote,true,false,0,DateTime.Now,""),default));
        }
    }
}
