using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SftpResilienceChecks
{
    private static void Wait(Task task) { Until(()=>task.IsCompleted,"Fixture task did not finish"); task.GetAwaiter().GetResult(); }
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    private static T Field<T>(SessionPane pane,string name)=>(T)typeof(SessionPane).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pane)!;
    private static Task Call(SessionPane pane,string method,params object[] args)=>(Task)typeof(SessionPane).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pane,args)!;
    private static void Until(Func<bool> ready,string description)
    { var until=DateTime.UtcNow.AddSeconds(45); while(!ready() && DateTime.UtcNow<until)RedesignChecks.Frames(1); if(!ready())throw new TimeoutException(description); }
    internal static void Run(MainWindow main,ProfileRepository repo,string root,string host,string key)
    {
        using var trace=new TextWriterTraceListener(Console.Out); Trace.Listeners.Add(trace);
        using var proxy=new SftpFaultProxy(host,22222);
        var direct=new ServerProfile { Name="Resilience fixture",Host=host,Port=22222,Username="damian",Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var profile=direct with { Host="127.0.0.1",Port=proxy.Port };
        var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
        using var inspection=new SftpSession(Result(factory.SftpAsync(direct,"serverdesk-test-passphrase",default)));
        using var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo);
        var window=new Window { Width=1200,Height=800,Content=pane }; window.Show(); Wait(pane.ConnectAsync("serverdesk-test-passphrase",default)); Wait(pane.ShowAsync(1)); RedesignChecks.Frames(50);
        Console.WriteLine("INFO resilience SSH/browser ready");
        var id=Guid.NewGuid().ToString("N"); var remote="/tmp/serverdesk-resilience-"+id; Wait(inspection.CreateDirectoryAsync(remote,default));
        var local=Path.GetFullPath(Path.Combine(root,"fixture-"+id)); Directory.CreateDirectory(local);
        var payload=RandomNumberGenerator.GetBytes(8<<20); var source=Path.Combine(local,"large.bin"); File.WriteAllBytes(source,payload);
        var expected=SHA256.HashData(payload); var localList=Field<ListBox>(pane,"localList");
        Field<TextBox>(pane,"localPath").Text=local; Field<TextBox>(pane,"remotePath").Text=remote; Wait(Call(pane,"RefreshAsync"));
        var entry=localList.Items.OfType<FileEntry>().Single(e=>e.Name=="large.bin");
        SessionPane.TransferSnapshot Last()=>pane.TransferSnapshots.First();
        Task StartUpload(out Task<SftpFaultProxy.Connection> relay)
        { relay=proxy.ArmNext(); return Call(pane,"UploadEntryAsync",entry); }
        void Accept()
        { Until(()=>main.OwnedWindows.Count>0,"Overwrite prompt missing"); main.OwnedWindows.Single().GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Potwierdź").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
        try
        {
            // Simulate a silent cable loss after the remote temporary file exists.
            var upload=StartUpload(out var next); var relay=Result(next);
            Until(()=>Last().Progress>0,"Upload did not report progress"); relay.Paused=true; RedesignChecks.Frames(5);
            using var watchdog=new CancellationTokenSource();
            _=Task.Run(async()=> { try { await Task.Delay(8000,watchdog.Token); relay.Dispose(); } catch(OperationCanceledException) { } });
            var clock=Stopwatch.StartNew(); pane.CancelTransfer(Last().Id); Wait(upload); clock.Stop(); watchdog.Cancel();
            File.WriteAllText(Path.Combine(root,"cancel-latency.txt"),clock.Elapsed.TotalMilliseconds.ToString("F0"));
            Console.WriteLine($"INFO silent network cancellation latency {clock.Elapsed.TotalMilliseconds:F0}ms");
            relay.Dispose();
            if(Last().State!=TransferState.Cancelled || clock.Elapsed>TimeSpan.FromSeconds(5))throw new Exception("Silent network cancellation is too slow or misclassified");
            if(Result(inspection.ExistsAsync(remote+"/large.bin",default)))throw new Exception("Cancelled upload published partial destination");
            Console.WriteLine("PASS silent upload loss: bounded cancellation and no published partial file");
            Wait(pane.RetryTransferAsync(Last().Id)); if(Last().State!=TransferState.Completed)throw new Exception("Retry after cancellation failed");
            var destination=Path.Combine(local,"verified.bin"); Wait(inspection.DownloadAsync(remote+"/large.bin",destination,false,new Progress<TransferProgress>(),default));
            if(!SHA256.HashData(File.ReadAllBytes(destination)).SequenceEqual(expected))throw new Exception("Retry payload differs");
            // Overwrite fails after progress; original must survive. The SSH session
            // remains connected because the relay drops just this transfer connection.
            File.WriteAllBytes(source,RandomNumberGenerator.GetBytes(8<<20)); Console.WriteLine("INFO dropping overwrite after progress");
            upload=StartUpload(out next); relay=Result(next); Accept(); Until(()=>Last().Progress>0,"Overwrite upload did not start"); Wait(Task.Run(relay.Dispose)); Console.WriteLine("INFO relay dropped"); Wait(upload);
            if(Last().State!=TransferState.Failed || !pane.IsConnected || MainWindow.TransferSlots.CurrentCount!=2)throw new Exception("Dropped transfer poisoned queue/session or retained a slot");
            Wait(inspection.DownloadAsync(remote+"/large.bin",destination,true,new Progress<TransferProgress>(),default));
            if(!SHA256.HashData(File.ReadAllBytes(destination)).SequenceEqual(expected))throw new Exception("Dropped overwrite damaged original");
            Console.WriteLine("PASS dropped upload preserves remote original, releases transfer slots and keeps SSH session alive");
            upload=pane.RetryTransferAsync(Last().Id); Accept(); Wait(upload); if(Last().State!=TransferState.Completed)throw new Exception("Retry after network failure failed");
            Wait(inspection.DownloadAsync(remote+"/large.bin",destination,true,new Progress<TransferProgress>(),default));
            if(!File.ReadAllBytes(destination).SequenceEqual(File.ReadAllBytes(source)))throw new Exception("Retry after network failure copied different bytes");
            // Silent download loss: cancellation must preserve the existing local file.
            var localOriginal=File.ReadAllBytes(source); var remoteEntry=Result(inspection.ListAsync(remote,default)).Single(e=>e.Name=="large.bin");
            next=proxy.ArmNext(); var download=Call(pane,"DownloadEntryAsync",remoteEntry); relay=Result(next); Accept(); Until(()=>Last().Progress>0,"Download did not start"); relay.Paused=true;
            clock.Restart(); pane.CancelTransfer(Last().Id); Wait(download); clock.Stop(); relay.Dispose();
            if(Last().State!=TransferState.Cancelled || clock.Elapsed>TimeSpan.FromSeconds(5) || !File.ReadAllBytes(source).SequenceEqual(localOriginal))throw new Exception($"Silent download cancellation failed: state={Last().State}, time={clock.ElapsedMilliseconds}ms, original={File.ReadAllBytes(source).SequenceEqual(localOriginal)}, detail={Last().Detail}");
            if(Directory.GetFiles(local,"*.part").Length!=0)throw new Exception("Local partial file leaked");
            Console.WriteLine($"PASS silent download cancellation {clock.ElapsedMilliseconds}ms preserves local original and removes local partial file");
            using(var client=Result(factory.SshAsync(direct,"serverdesk-test-passphrase",default)))
            {
                var command=Task.Run(()=>client.RunCommand("python3 -c \"from pathlib import Path; p=Path('"+remote+"/many'); p.mkdir(); [(p/('file'+str(i)+'.txt')).touch() for i in range(5000)]\""));
                using var result=Result(command); if(result.ExitStatus!=0)throw new Exception("Large file fixture setup failed");
            }
            var listing=Result(inspection.ListAsync(remote+"/many",default)); if(listing.Count!=5000)throw new Exception("Large SFTP listing truncated");
            var remoteList=Field<ListBox>(pane,"remoteList"); Field<TextBox>(pane,"remotePath").Text=remote+"/many"; clock.Restart(); Wait(Call(pane,"RefreshAsync")); clock.Stop();
            if(remoteList.ItemCount!=5001 || remoteList.Items.OfType<FileEntry>().Skip(1).First().Name!="file0.txt")throw new Exception("Large directory UI count/order incorrect");
            remoteList.SelectedItem=remoteList.Items.OfType<FileEntry>().Single(e=>e.Name=="file4000.txt"); Field<TextBox>(pane,"remoteFilter").Text="file4000";
            RedesignChecks.Frames(3);
            if(remoteList.ItemCount!=2 || remoteList.SelectedItem is not FileEntry { Name:"file4000.txt" })throw new Exception($"Large directory filter lost selected identity: count={remoteList.ItemCount}, selected={(remoteList.SelectedItem as FileEntry)?.Name}, filter={Field<TextBox>(pane,"remoteFilter").Text}");
            Field<TextBox>(pane,"remoteFilter").Text="";
            Console.WriteLine($"PASS real 5000-entry SFTP directory: complete listing, natural order and retained filtered selection; refresh {clock.ElapsedMilliseconds}ms");
            next=proxy.ArmNext(); var slowClient=Result(factory.SftpAsync(profile,"serverdesk-test-passphrase",default)); relay=Result(next);
            using(var session=new SftpSession(slowClient))
            {
                relay.Paused=true; using var stop=new CancellationTokenSource(); var read=session.ListAsync(remote+"/many",stop.Token); RedesignChecks.Frames(3); clock.Restart(); stop.Cancel();
                try { Wait(read); throw new Exception("Cancelled directory read succeeded"); } catch(OperationCanceledException) { }
                finally { relay.Paused=false; }
                if(clock.Elapsed>TimeSpan.FromSeconds(5))throw new Exception("Directory cancellation too slow");
            }
            Console.WriteLine("PASS paused SFTP directory read honors cancellation");
            window.CaptureRenderedFrame()!.Save(Path.Combine(root,"resilience-results.png"));
        }
        finally
        {
            proxy.Dispose(); Wait(pane.CloseAsync()); window.Close();
            async Task Cleanup(string path)
            {
                if(path!=remote && !path.StartsWith(remote+"/",StringComparison.Ordinal))throw new Exception("Unsafe fixture path");
                foreach(var file in await inspection.ListAsync(path,default)) { if(file.IsDirectory && !file.IsSymbolicLink)await Cleanup(file.FullPath); await inspection.DeleteAsync(file,default); }
            }
            Wait(Task.Run(()=>Cleanup(remote)));
            Wait(inspection.DeleteAsync(new("fixture",remote,true,false,0,DateTime.Now,""),default));
        }
    }
}
