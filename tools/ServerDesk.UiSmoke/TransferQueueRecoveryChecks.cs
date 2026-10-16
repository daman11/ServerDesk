using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class TransferQueueRecoveryChecks
{
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static Task Call(SessionPane pane,string method,params object[] args)=>(Task)typeof(SessionPane).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pane,args)!;
    private static void Until(Func<bool> done,string message,int seconds=15)
    { var watch=Stopwatch.StartNew(); while(!done()) { if(watch.Elapsed>TimeSpan.FromSeconds(seconds))throw new TimeoutException(message); RedesignChecks.Frames(1); } }
    private static void Wait(Task task) { Until(()=>task.IsCompleted,"Fixture task did not finish",40); task.GetAwaiter().GetResult(); }
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }

    internal static void Run(MainWindow main,ProfileRepository repo,string root,string host,string key)
    {
        main.PreviewPreferences(main.Preferences with { ShowMetrics=false });
        using var proxy=new SftpFaultProxy(host,22222);
        var direct=new ServerProfile { Name="Owned queue recovery fixture",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var profile=direct with { Host="127.0.0.1",Port=proxy.Port };
        var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
        using var inspection=new SftpSession(Result(factory.SftpAsync(direct,"serverdesk-test-passphrase",default)));
        using var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo);
        var window=new Window { Width=1000,Height=720,Content=pane }; window.Show();
        var remote="/tmp/serverdesk-queue-recovery-"+Guid.NewGuid().ToString("N");
        var local=Path.GetFullPath(Path.Combine(root,"files")); Directory.CreateDirectory(local);
        var work=new List<Task>(); SftpFaultProxy.Connection? browser=null;
        FileEntry Entry(string name)=>LocalTransferSource.Read(Path.Combine(local,name));
        try
        {
            Wait(inspection.CreateDirectoryAsync(remote,default)); var sshReady=proxy.ArmNext(); var connecting=pane.ConnectAsync("serverdesk-test-passphrase",default); var ssh=Result(sshReady); Wait(connecting);
            var browserReady=proxy.ArmNext(); var show=pane.ShowAsync(1); browser=Result(browserReady); Wait(show);
            Field<TextBox>(pane,"localPath").Text=local; Field<TextBox>(pane,"remotePath").Text=remote; Wait(Call(pane,"RefreshAsync"));
            browser.Paused=true;
            foreach(var name in new[]{"first.txt","second.txt","third.txt"})File.WriteAllText(Path.Combine(local,name),"Owned queue recovery — "+name);
            // Enqueue the batch before any refresh starts. A browsing refresh
            // correctly blocks *new* file selections until paths are confirmed.
            foreach(var name in new[]{"first.txt","second.txt","third.txt"})work.Add(Call(pane,"UploadEntryAsync",Entry(name)));
            Until(()=>pane.TransferSnapshots.Count(j=>j.State==TransferState.Completed)>=2,"Uploads did not finish before browser refresh");
            if(work.All(task=>task.IsCompleted))throw new Exception("Fixture failed to hold directory refresh");
            Until(()=>pane.TransferSnapshots.Count(j=>j.State==TransferState.Completed)==3,"Queued transfer was blocked by a completed transfer's refresh");
            Until(()=>MainWindow.TransferSlots.CurrentCount==2,"Third completed upload retained its slot",3);
            browser.Dispose(); Wait(Task.WhenAll(work));
            if(pane.TransferSnapshots.Any(j=>j.State!=TransferState.Completed || !j.Detail.Contains(AppText.T(" · odśwież listę ręcznie"),StringComparison.Ordinal)))throw new Exception("A failed post-copy refresh changed transfer success or hid its warning");
            foreach(var name in new[]{"first.txt","second.txt","third.txt"})
            {
                var destination=Path.Combine(local,"verified-"+name); Wait(inspection.DownloadAsync(remote+"/"+name,destination,false,new Progress<TransferProgress>(),default));
                if(File.ReadAllBytes(destination).SequenceEqual(File.ReadAllBytes(Path.Combine(local,name)))==false)throw new Exception("Transferred payload differs");
            }
            MainWindow.TransferSlots.Wait(); MainWindow.TransferSlots.Wait();
            try
            {
                File.WriteAllText(Path.Combine(local,"cancelled.txt"),"This queued file must not be published");
                var pending=Call(pane,"UploadEntryAsync",Entry("cancelled.txt")); var cancelled=pane.TransferSnapshots.First();
                if(cancelled.State!=TransferState.Queued)throw new Exception("Fixture did not queue the cancellation case");
                pane.CancelTransfer(cancelled.Id); Wait(pending);
                if(pane.TransferSnapshots.First().State!=TransferState.Cancelled || Result(inspection.ExistsAsync(remote+"/cancelled.txt",default)))throw new Exception("Queued cancellation published a file or lost its state");
            }
            finally { MainWindow.TransferSlots.Release(2); }
            // Failed jobs remain retryable only while this SSH session is connected.
            Wait(Call(pane,"UploadEntryAsync",new FileEntry("missing.txt",Path.Combine(local,"missing.txt"),false,false,0,DateTime.Now,"")));
            var failed=pane.TransferSnapshots.First();
            if(failed.State!=TransferState.Failed || !failed.CanRetry)throw new Exception("Failed connected transfer cannot be retried");
            var retry=pane.GetVisualDescendants().OfType<Button>().Single(button=>button.IsVisible && Avalonia.Automation.AutomationProperties.GetName(button)==AppText.T("Ponów: "+failed.Label));
            ssh.Dispose(); Until(()=>!pane.IsConnected && !retry.IsEnabled,"SSH transport loss did not disable retry");
            if(retry.IsEnabled || pane.TransferSnapshots.First().CanRetry)throw new Exception("Disconnected retry action remains enabled");
            Wait(pane.ConnectAsync("serverdesk-test-passphrase",default));
            if(!retry.IsEnabled || !pane.TransferSnapshots.First().CanRetry)throw new Exception("Reconnect did not restore retry action");
            File.WriteAllText(Path.Combine(local,"missing.txt"),"Recovered retry — original destination");
            Wait(pane.RetryTransferAsync(failed.Id));
            if(pane.TransferSnapshots.First().State!=TransferState.Completed || !Result(inspection.ExistsAsync(remote+"/missing.txt",default)) || MainWindow.TransferSlots.CurrentCount!=2)throw new Exception("Retry after reconnect did not keep its destination or release its slot");
            window.CaptureRenderedFrame()!.Save(Path.Combine(root,"transfer-queue-recovery.png"));
            Console.WriteLine("PASS completed SFTP copies release both slots before a stalled browser refresh; queued jobs complete; refresh loss preserves success and byte integrity; queued cancellation publishes nothing; retry controls follow SSH transport loss/reconnect and retry retains its destination");
        }
        finally
        {
            browser?.Dispose(); Wait(Call(pane,"ResetConnectionsAsync")); if(work.Count>0)Wait(Task.WhenAll(work));
            foreach(var entry in Result(inspection.ListAsync(remote,default)))
            { if(entry.IsDirectory || entry.IsSymbolicLink || !entry.FullPath.StartsWith(remote+"/",StringComparison.Ordinal))throw new Exception("Unsafe fixture cleanup entry"); Wait(inspection.DeleteAsync(entry,default)); }
            Wait(inspection.DeleteAsync(new("fixture",remote,true,false,0,DateTime.Now,""),default));
            window.Close();
        }
    }
}
