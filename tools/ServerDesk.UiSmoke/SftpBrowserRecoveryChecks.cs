using System.Diagnostics;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SftpBrowserRecoveryChecks
{
    private static T Field<T>(SessionPane pane,string name)=>(T)typeof(SessionPane).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pane)!;
    private static Task Call(SessionPane pane,string name,params object?[] args)=>(Task)typeof(SessionPane).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pane,args)!;
    private static void Until(Func<bool> done,string message,int seconds=25)
    { var watch=Stopwatch.StartNew(); while(!done()) { if(watch.Elapsed>TimeSpan.FromSeconds(seconds))throw new TimeoutException(message); RedesignChecks.Frames(1); } }
    private static void Wait(Task work) { Until(()=>work.IsCompleted,"Browser recovery task did not finish",40); work.GetAwaiter().GetResult(); }
    private static T Result<T>(Task<T> work) { Wait(work); return work.Result; }

    internal static void Run(MainWindow main,ProfileRepository repo,string root,string host,string key)
    {
        main.PreviewPreferences(main.Preferences with { ShowMetrics=false });
        using var proxy=new SftpFaultProxy(host,22222);
        var profile=new ServerProfile { Name="Owned SFTP browser recovery",Host="127.0.0.1",Port=proxy.Port,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
        using var inspection=new SftpSession(Result(factory.SftpAsync(profile with { Host=host,Port=22222 },"serverdesk-test-passphrase",default)));
        using var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo);
        var window=new Window { Width=1100,Height=760,Content=pane }; window.Show();
        var remote="/tmp/serverdesk-browser-recovery-"+Guid.NewGuid().ToString("N");
        var local=Path.GetFullPath(Path.Combine(root,"files")); Directory.CreateDirectory(local);
        SftpFaultProxy.Connection? browser=null; Task? closingRecovery=null;
        try
        {
            Wait(inspection.CreateDirectoryAsync(remote,default)); Wait(inspection.CreateDirectoryAsync(remote+"/nested",default));
            var payload=Path.Combine(local,"payload.txt"); File.WriteAllText(payload,"Owned recovery fixture — żółć");
            Wait(inspection.UploadAsync(payload,remote+"/nested/payload.txt",false,new Progress<TransferProgress>(),default));
            Wait(pane.ConnectAsync("serverdesk-test-passphrase",default));
            var browserReady=proxy.ArmNext(); var showing=pane.ShowAsync(1); browser=Result(browserReady); Wait(showing);
            var ssh=Field<SshTerminalSession>(pane,"session"); var connectionId=Field<Guid>(pane,"commandConnectionId");
            Field<TextBox>(pane,"localPath").Text=local;
            Wait(Call(pane,"NavigateFilesAsync",true,remote,null)); Wait(Call(pane,"NavigateFilesAsync",true,remote+"/nested",null));
            Field<TextBox>(pane,"localFilter").Text="payload"; Field<TextBox>(pane,"remoteFilter").Text="payload"; RedesignChecks.Frames();
            var remoteList=Field<ListBox>(pane,"remoteList"); var localList=Field<ListBox>(pane,"localList");
            remoteList.SelectedItem=remoteList.Items.OfType<FileEntry>().Single(e=>e.Name=="payload.txt");
            localList.SelectedItem=localList.Items.OfType<FileEntry>().Single(e=>e.Name=="payload.txt");
            var remoteHistory=Field<DirectoryNavigationHistory>(pane,"remoteNavigation"); var localHistory=Field<DirectoryNavigationHistory>(pane,"localNavigation");
            var remoteCount=remoteHistory.Count; var localCount=localHistory.Count;
            void CheckContext()
            {
                if(!pane.IsConnected || !ReferenceEquals(ssh,Field<SshTerminalSession>(pane,"session")) || connectionId!=Field<Guid>(pane,"commandConnectionId"))throw new Exception("SFTP recovery reset the SSH terminal");
                if(Field<TextBox>(pane,"localPath").Text!=local || Field<TextBox>(pane,"remotePath").Text!=remote+"/nested" || Field<TextBox>(pane,"localFilter").Text!="payload" || Field<TextBox>(pane,"remoteFilter").Text!="payload")throw new Exception("Recovery changed directories or filters");
                if(remoteHistory.Count!=remoteCount || localHistory.Count!=localCount)throw new Exception("Recovery reset or duplicated navigation history");
            }
            var original=Field<SftpSession>(pane,"sftp"); browser.Dispose(); Until(()=>!original.IsConnected,"Browser did not notice transport loss");
            var next=proxy.ArmNext();
            pane.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Odśwież").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            browser=Result(next);
            Until(()=>main.OwnedWindows.Count>0 || Field<SftpSession?>(pane,"sftp") is { IsConnected:true } replacement && !ReferenceEquals(replacement,original) && Field<int>(pane,"fileRefreshes")==0,"Refresh did not recover SFTP");
            if(main.OwnedWindows.Count>0)throw new Exception("Explicit refresh showed an error instead of recovering SFTP");
            CheckContext();
            if(localList.SelectedItem is not FileEntry { Name:"payload.txt" } || remoteList.SelectedItem is not FileEntry { Name:"payload.txt" })throw new Exception("Recovery lost selected files");
            var marker=Guid.NewGuid().ToString("N")[..10];
            Wait(ssh.WriterStream.WriteAsync(Encoding.UTF8.GetBytes("printf '\\nRECOVERY_%s\\n' '"+marker+"'\r")).AsTask()); Wait(ssh.WriterStream.FlushAsync());
            Until(()=>SshAcceptanceChecks.Text(Field<TerminalControl>(pane,"terminal")).Replace("\n","").Contains("RECOVERY_"+marker),"Original SSH terminal stopped responding");
            Console.WriteLine("PASS manual Refresh reconnects only SFTP and preserves paths, filters, selection, history and live SSH terminal");

            // A fresh SFTP login must still validate the host key. Never loosen
            // trust or send mutations merely because the old SSH terminal lives.
            var trust=Result(repo.TrustedHostKeysAsync()).Single(e=>e.Host==profile.Host && e.Port==profile.Port);
            Wait(repo.ForgetHostKeyAsync(trust));
            var wrong="SHA256:"+Convert.ToBase64String(Enumerable.Repeat((byte)7,32).ToArray()).TrimEnd('=');
            Wait(repo.TrustHostAsync(profile.Host,profile.Port,wrong));
            browser.Dispose(); original=Field<SftpSession>(pane,"sftp"); Until(()=>!original.IsConnected,"Browser did not close before trust test");
            var failed=Call(pane,"RefreshFilesAsync");
            try { Wait(failed); throw new Exception("Changed host key was accepted during recovery"); }
            catch when(failed.IsFaulted) { }
            if(Field<SftpSession?>(pane,"sftp")!=null)throw new Exception("Rejected recovery published a browser");
            var wrongTrust=Result(repo.TrustedHostKeysAsync()).Single(e=>e.Host==profile.Host && e.Port==profile.Port);
            Wait(repo.ForgetHostKeyAsync(wrongTrust)); Wait(repo.TrustHostAsync(trust.Host,trust.Port,trust.Fingerprint)); CheckContext();
            var count=proxy.ConnectionCount; next=proxy.ArmNext();
            Field<TextBox>(pane,"remotePath").Focus(); SshAcceptanceChecks.Press(window,Key.F5);
            var extra=Call(pane,"RefreshFilesAsync"); browser=Result(next); Wait(extra);
            Until(()=>Field<int>(pane,"fileRefreshes")==0 && !pane.IsBusy,"Concurrent refresh did not settle");
            if(proxy.ConnectionCount!=count+1)throw new Exception("Concurrent refresh opened duplicate SFTP clients");
            CheckContext();
            window.CaptureRenderedFrame()!.Save(Path.Combine(root,"sftp-browser-recovered.png"));
            Console.WriteLine("PASS host-key rejection leaves SSH/context intact; F5 and rapid refresh share one recovered browser");

            browser.Dispose(); original=Field<SftpSession>(pane,"sftp"); Until(()=>!original.IsConnected,"Browser did not close before cancellation test");
            trust=Result(repo.TrustedHostKeysAsync()).Single(e=>e.Host==profile.Host && e.Port==profile.Port); Wait(repo.ForgetHostKeyAsync(trust));
            var prompt=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            factory.ConfirmHostKey=(_,ct)=> { prompt.TrySetResult(); return new TaskCompletionSource<bool>().Task.WaitAsync(ct); };
            closingRecovery=Call(pane,"RefreshFilesAsync"); Wait(prompt.Task); Wait(pane.CloseAsync());
            try { Wait(closingRecovery); throw new Exception("Recovery survived a closed session"); } catch(OperationCanceledException) { }
            if(Field<SftpSession?>(pane,"sftp")!=null || pane.IsConnected)throw new Exception("Cancelled recovery leaked into the closed pane");
            Console.WriteLine("PASS closing the pane cancels an in-flight recovery/host-key prompt and drains the new client");
        }
        finally
        {
            browser?.Dispose(); Wait(pane.CloseAsync());
            if(closingRecovery!=null) { try { Wait(closingRecovery); } catch(OperationCanceledException) { } }
            foreach(var entry in Result(inspection.ListAsync(remote+"/nested",default)))
            { if(entry.IsDirectory || entry.IsSymbolicLink || entry.FullPath!=remote+"/nested/payload.txt")throw new Exception("Unsafe recovery fixture cleanup entry"); Wait(inspection.DeleteAsync(entry,default)); }
            Wait(inspection.DeleteAsync(new("nested",remote+"/nested",true,false,0,DateTime.Now,""),default));
            Wait(inspection.DeleteAsync(new("fixture",remote,true,false,0,DateTime.Now,""),default)); window.Close();
        }
    }
}
