using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Iciclecreek.Terminal;
using Renci.SshNet.Common;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class NetworkRecoveryChecks
{
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(value)!;
    private static Task Call(object value,string name,params object?[] args)=>(Task)value.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(value,args)!;
    private static void Until(Func<bool> value,string message,int seconds=12)
    { var end=DateTime.UtcNow.AddSeconds(seconds); while(!value() && DateTime.UtcNow<end)RedesignChecks.Frames(1); if(!value())throw new TimeoutException(message); }
    private static void Wait(Task task,[System.Runtime.CompilerServices.CallerLineNumber] int line=0)
    { try { RedesignChecks.Wait(task); } catch(TimeoutException ex) { throw new TimeoutException("Network recovery wait at line "+line,ex); } }
    private static T Read<T>(Task<T> task) { Wait(task); return task.Result; }
    private static void Check(bool value,string message) { if(!value)throw new Exception(message); }
    internal static void Run(string root,string host,string key)
    {
        var paths=new AppPaths(Path.Combine(root,"network-recovery-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
        Wait(repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults with { Preferences=new() { ShowMetrics=false,DisabledPlugins=new([PluginCatalog.Commands,PluginCatalog.LinuxMonitoring]) } }));
        var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
        var direct=new ServerProfile { Name="Disposable recovery fixture",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        using var inspection=new SftpSession(Read(factory.SftpAsync(direct,"serverdesk-test-passphrase",default)));
        using var proxy=new SftpFaultProxy(host,22222); var target=direct with { Host="127.0.0.1",Port=proxy.Port };
        var remote="/tmp/serverdesk-recovery-"+Guid.NewGuid().ToString("N"); Wait(inspection.CreateDirectoryAsync(remote,default));
        var local=Path.Combine(root,"payloads"); Directory.CreateDirectory(local); var source=Path.Combine(local,"ui.bin"); var payload=RandomNumberGenerator.GetBytes(4<<20); File.WriteAllBytes(source,payload);
        var small=Path.Combine(local,"sentinel.txt"); File.WriteAllText(small,"preserved original fixture"); Wait(inspection.UploadAsync(small,remote+"/keep.bin",false,new Progress<TransferProgress>(),default));
        var main=new MainWindow(new(repo),repo,new CredentialStore(paths),factory,new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(15);
        factory.ConfirmHostKey=(_,_)=>Task.FromResult(true); // Owned disposable SSH fixture only.
        var pane=new SessionPane(main,target,factory,new ExternalRdpClient(),repo); main.RegisterPane(pane);
        byte[] Remote(string path)
        { var copy=Path.Combine(local,"verify-"+Guid.NewGuid().ToString("N")); Wait(inspection.DownloadAsync(path,copy,false,new Progress<TransferProgress>(),default)); var bytes=File.ReadAllBytes(copy); File.Delete(copy); return bytes; }
        try
        {
            foreach(var upload in new[]{true,false})
            {
                Console.WriteLine("INFO idle transfer fixture: "+(upload?"upload":"download"));
                if(!upload)Wait(inspection.UploadAsync(source,remote+"/large.bin",false,new Progress<TransferProgress>(),default));
                var ready=proxy.ArmNext(); var client=Read(factory.SftpAsync(target,"serverdesk-test-passphrase",default)); var relay=Read(ready); client.OperationTimeout=TimeSpan.FromSeconds(2);
                using var connection=new SftpSession(client); long progress=0; var report=new InlineProgress<TransferProgress>(p=>Interlocked.Exchange(ref progress,p.Bytes));
                var output=Path.Combine(local,"download.bin"); File.WriteAllText(output,"keep local original");
                var work=upload?connection.UploadAsync(source,remote+"/keep.bin",true,report,default):connection.DownloadAsync(remote+"/large.bin",output,true,report,default);
                Until(()=>Interlocked.Read(ref progress)>0,"Transfer did not start"); relay.Paused=true;
                Console.WriteLine("INFO relay paused after "+Interlocked.Read(ref progress)+" bytes");
                Exception? error=null; var clock=Stopwatch.StartNew(); try { Wait(work); } catch(Exception ex) { error=ex; }
                Check(error is SshOperationTimeoutException,"Silent transfer was not classified as an idle timeout: "+error?.GetType().Name);
                Check(clock.Elapsed<TimeSpan.FromSeconds(8),"Silent transfer did not release promptly");
                if(upload)Check(Encoding.UTF8.GetString(Remote(remote+"/keep.bin"))=="preserved original fixture" && connection.CleanupWarnings.Count>0,"Idle upload replaced its original or hid its remote partial");
                else Check(File.ReadAllText(output)=="keep local original" && Directory.GetFiles(local,"*.part").Length==0,"Idle download replaced its original or left a local partial");
            }
            Console.WriteLine("PASS silent upload/download: bounded idle timeout, original destination preservation, local cleanup and remote partial warning; terminal connection independent");

            var warm=proxy.ArmNext(); var warmClient=Read(factory.SftpAsync(target,"serverdesk-test-passphrase",default)); var warmRelay=Read(warm); warmClient.OperationTimeout=TimeSpan.FromSeconds(5);
            using(var connection=new SftpSession(warmClient))
            {
                long progress=0; var work=connection.UploadAsync(source,remote+"/resumed.bin",false,new InlineProgress<TransferProgress>(p=>Interlocked.Exchange(ref progress,p.Bytes)),default);
                Until(()=>Interlocked.Read(ref progress)>0,"Short-pause upload did not start"); warmRelay.Paused=true; RedesignChecks.Frames(20); Check(!work.IsCompleted,"Paused transfer completed unexpectedly"); warmRelay.Paused=false; Wait(work);
                Check(SHA256.HashData(Remote(remote+"/resumed.bin")).SequenceEqual(SHA256.HashData(payload)),"Short-pause recovery changed bytes");
            }
            Console.WriteLine("PASS temporary silence below the idle limit recovers in the same transfer with identical SHA-256");

            var listingReady=proxy.ArmNext(); var listingClient=Read(factory.SftpAsync(target,"serverdesk-test-passphrase",default)); var listingRelay=Read(listingReady); listingClient.OperationTimeout=TimeSpan.FromSeconds(2);
            using(var listing=new SftpSession(listingClient))
            {
                listingRelay.Paused=true; var work=listing.ListAsync(remote,default); Exception? error=null;
                try { Wait(work); } catch(Exception ex) { error=ex; }
                Check(error is SshOperationTimeoutException,"Silent directory read did not have a bounded timeout");
            }
            Console.WriteLine("PASS silent SFTP directory read has a bounded timeout without requiring application close");

            Wait(pane.ConnectAsync("serverdesk-test-passphrase",default)); var sshRelay=proxy.Connections.Last(); Wait(pane.ShowAsync(1));
            Field<TextBox>(pane,"localPath").Text=Path.GetFullPath(local); Field<TextBox>(pane,"remotePath").Text=remote; Wait(Call(pane,"RefreshAsync"));
            Field<TextBox>(pane,"localFilter").Text="bin"; Field<TextBox>(pane,"remoteFilter").Text="bin"; RedesignChecks.Frames();
            Field<ListBox>(pane,"localList").SelectedItem=Field<ListBox>(pane,"localList").Items.OfType<FileEntry>().Single(e=>e.Name=="ui.bin");
            Field<ListBox>(pane,"remoteList").SelectedItem=Field<ListBox>(pane,"remoteList").Items.OfType<FileEntry>().Single(e=>e.Name=="keep.bin");
            var localHistory=Field<DirectoryNavigationHistory>(pane,"localNavigation").Count; var remoteHistory=Field<DirectoryNavigationHistory>(pane,"remoteNavigation").Count;
            var next=proxy.ArmNext(); var copying=Call(pane,"UploadEntryAsync",LocalTransferSource.Read(source)); var transferRelay=Read(next);
            Until(()=>pane.TransferSnapshots.Any(j=>j.Progress>0),"UI transfer did not start"); transferRelay.Paused=true; sshRelay.Dispose(); Wait(copying);
            Until(()=>pane.VisualState==SessionVisualState.Failed,"SSH loss did not reach the pane");
            var job=pane.TransferSnapshots.Single(); Check(job.State==TransferState.Failed && !job.CanRetry && job.Detail.Contains(AppText.T("Transfer przerwany przez utratę połączenia SSH. Ukończone pliki pozostają na miejscu. Połącz sesję ponownie, a następnie wybierz Ponów. Plik zostanie przesłany od początku; nadpisanie wymaga potwierdzenia.")),"Transport loss was presented as a user cancellation");
            Wait(pane.ReconnectAsync());
            Check(pane.SelectedPanel==1 && Field<TextBox>(pane,"remotePath").Text==remote && Field<TextBox>(pane,"localPath").Text==Path.GetFullPath(local),"Reconnect lost the current SFTP view/directories");
            Check(Field<TextBox>(pane,"localFilter").Text=="bin" && Field<TextBox>(pane,"remoteFilter").Text=="bin" && Field<DirectoryNavigationHistory>(pane,"localNavigation").Count==localHistory && Field<DirectoryNavigationHistory>(pane,"remoteNavigation").Count==remoteHistory,"Reconnect lost filters or duplicated navigation history");
            Check(Field<ListBox>(pane,"localList").SelectedItem is FileEntry { Name:"ui.bin" } && Field<ListBox>(pane,"remoteList").SelectedItem is FileEntry { Name:"keep.bin" },"Reconnect lost selected files");
            Check(pane.TransferSnapshots.Single().CanRetry && pane.TransferSnapshots.Single().State==TransferState.Failed,"Reconnect automatically repeated an interrupted write");
            Wait(pane.RetryTransferAsync(job.Id)); Check(pane.TransferSnapshots.Single().State==TransferState.Completed && SHA256.HashData(Remote(remote+"/ui.bin")).SequenceEqual(SHA256.HashData(payload)),"Explicit retry failed or changed bytes");
            Console.WriteLine("PASS SSH loss marks interrupted work failed; explicit reconnect retains SFTP panel, directories, filters, selected files and navigation; explicit retry verifies SHA-256");

            sshRelay=proxy.Connections[^3]; // reconnect SSH, browser, then the explicit transfer
            var ssh=Field<SshTerminalSession>(pane,"session");
            var busyProbe=ssh.ProbeAsync("sleep 2",default);
            Wait(Call(ssh,"VerifyTransportAsync",TimeSpan.FromMilliseconds(100),CancellationToken.None)); Wait(busyProbe);
            Check(pane.IsConnected,"A busy command probe was mistaken for a broken transport");
            Console.WriteLine("PASS a busy Commands probe does not trigger a false SSH health timeout");
            sshRelay.Paused=true;
            // Simulate the age observed after a long idle/suspend; the production
            // periodic watchdog and its normal 12-second deadline still run.
            typeof(SshTerminalSession).GetField("lastResponse",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(ssh,Stopwatch.GetTimestamp()-31*Stopwatch.Frequency);
            Until(()=>pane.VisualState==SessionVisualState.Failed,"Quiet SSH blackhole was not detected by the production watchdog",30);
            Check(ssh.TransportFailure is SshOperationTimeoutException,"Quiet SSH loss has no timeout reason");
            Wait(pane.ReconnectAsync()); Check(pane.IsConnected && pane.SelectedPanel==1,"Session did not recover after quiet-link/suspend simulation");
            var terminal=Field<TerminalControl>(pane,"terminal"); var marker=Guid.NewGuid().ToString("N")[..10]; Wait(terminal.SendInputAsync("printf '\\nRECOVERY_%s\\n' '"+marker+"'\r",default));
            Until(()=>SshAcceptanceChecks.Text(terminal).Replace("\n","").Contains("RECOVERY_"+marker),"Reconnected terminal cannot execute input");
            Console.WriteLine("PASS production quiet-link watchdog after simulated idle/suspend age, manual recovery and fresh terminal input; no commands or transfers replayed");
            var browser=proxy.Connections.Last(); browser.Paused=true; var refresh=Call(pane,"RefreshFilesAsync"); RedesignChecks.Frames(5);
            var closing=pane.CloseAsync(); Wait(closing); try { Wait(refresh); } catch(OperationCanceledException) { } catch(ObjectDisposedException) { } catch(SshException) { }
            Check(MainWindow.TransferSlots.CurrentCount==2 && pane.ActiveTransfers==0,"Closing recovery retained global transfer slots");
            Console.WriteLine("PASS close drains a silent SFTP listing and releases all transfer slots");
        }
        catch(Exception ex) { Console.WriteLine("FAIL network recovery: "+ex); throw; }
        finally
        {
            main.RemovePane(pane); Wait(pane.CloseAsync()); main.Close(); RedesignChecks.Frames(5);
            foreach(var file in Read(inspection.ListAsync(remote,default)))
            { if(file.IsDirectory || file.IsSymbolicLink || !file.FullPath.StartsWith(remote+"/",StringComparison.Ordinal))throw new Exception("Unsafe fixture cleanup"); Wait(inspection.DeleteAsync(file,default)); }
            Wait(inspection.DeleteAsync(new("fixture",remote,true,false,0,DateTime.Now,""),default));
        }
    }
    private sealed class InlineProgress<T>(Action<T> report):IProgress<T> { public void Report(T value)=>report(value); }
}
