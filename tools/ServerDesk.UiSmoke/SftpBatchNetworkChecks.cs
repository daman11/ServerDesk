using System.Collections;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static partial class SftpBatchChecks
{
    private static object[] Jobs(SessionPane pane)=>Field<IEnumerable>(pane,"transferJobs").Cast<object>().ToArray();
    private static T Value<T>(object job,string name)=>(T)job.GetType().GetProperty(name)!.GetValue(job)!;
    private static FileEntry FileEntryFor(string path)=>new(Path.GetFileName(path),path,Directory.Exists(path),false,System.IO.File.Exists(path)?new FileInfo(path).Length:0,DateTime.Now,"");
    internal static void Network(MainWindow main,ProfileRepository repo,string root,string host,string key)
    {
        var profile=new ServerProfile { Name="Batch SFTP integration",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
        using var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo);
        var w=new Window { Width=1200,Height=820,Content=pane }; w.Show(); RedesignChecks.Frames();
        Wait(pane.ConnectAsync("serverdesk-test-passphrase",CancellationToken.None)); Wait(pane.ShowAsync(1));
        var connectionTask=factory.SftpAsync(profile,"serverdesk-test-passphrase",CancellationToken.None); Wait(connectionTask);
        using var inspection=new SftpSession(connectionTask.Result);
        var id=Guid.NewGuid().ToString("N"); var remoteRoot="/tmp/serverdesk-batch-"+id;
        var localRoot=Path.GetFullPath(Path.Combine(root,"fixture-"+id)); var source=Path.Combine(localRoot,"source"); var downloads=Path.Combine(localRoot,"downloads"); var alternate=Path.Combine(localRoot,"other");
        Directory.CreateDirectory(source); Directory.CreateDirectory(downloads); Directory.CreateDirectory(alternate); Directory.CreateDirectory(Path.Combine(source,"documents"));
        System.IO.File.WriteAllBytes(Path.Combine(source,"żółć.bin"),RandomNumberGenerator.GetBytes(1<<20));
        System.IO.File.WriteAllText(Path.Combine(source,"config.json"),"{\"fixture\":true}"); System.IO.File.WriteAllText(Path.Combine(source,"documents","notes.txt"),"SFTP batch folder żółć");
        Wait(inspection.CreateDirectoryAsync(remoteRoot,CancellationToken.None));
        var original=remoteRoot+"/original"; var changed=remoteRoot+"/changed";
        Wait(inspection.CreateDirectoryAsync(original,CancellationToken.None)); Wait(inspection.CreateDirectoryAsync(changed,CancellationToken.None));
        var local=Field<ListBox>(pane,"localList"); var remote=Field<ListBox>(pane,"remoteList");
        void Refresh(string localDirectory,string remoteDirectory)
        { Field<TextBox>(pane,"localPath").Text=localDirectory; Field<TextBox>(pane,"remotePath").Text=remoteDirectory; Wait((Task)Call(pane,"RefreshAsync")!); }
        void Complete(Task task,bool acceptOverwrite=false)
        {
            var until=DateTime.UtcNow.AddSeconds(60);
            while(!task.IsCompleted && DateTime.UtcNow<until)
            {
                RedesignChecks.Frames(1);
                if(Jobs(pane).Count(j=>Value<TransferState>(j,"State") is TransferState.Connecting or TransferState.Running)>2)throw new Exception("Batch exceeded global two-transfer limit");
                foreach(var prompt in main.OwnedWindows.ToArray())
                { var button=prompt.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==(acceptOverwrite?"Potwierdź":"Anuluj")); button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
            }
            Wait(task);
        }
        try
        {
            Refresh(source,original); Select(local,"żółć.bin","config.json","documents"); Wait((Task)Call(pane,"RefreshAsync")!);
            if(Names(local).Length!=3)throw new Exception("Real refresh lost group selection");
            Wait(MainWindow.TransferSlots.WaitAsync()); Wait(MainWindow.TransferSlots.WaitAsync()); Task upload;
            try
            {
                upload=(Task)Call(pane,"UploadAsync")!;
                if(Jobs(pane).Length!=3 || Jobs(pane).Any(j=>Value<TransferState>(j,"State")!=TransferState.Queued))throw new Exception("Batch did not enqueue every selection before starting");
                Field<TextBox>(pane,"remotePath").Text=changed; Field<TextBox>(pane,"localPath").Text=alternate;
                RedesignChecks.Frames(); w.CaptureRenderedFrame()!.Save(Path.Combine(root,"batch-queued.png"));
            }
            finally { MainWindow.TransferSlots.Release(2); }
            Complete(upload);
            if(Jobs(pane).Any(j=>Value<TransferState>(j,"State")!=TransferState.Completed))throw new Exception("Upload batch did not finish successfully");
            var empty=inspection.ListAsync(changed,CancellationToken.None); Wait(empty); if(empty.Result.Count!=0)throw new Exception("Queued uploads followed changed panel destination");
            Refresh(downloads,original); Select(remote,"żółć.bin","config.json","documents");
            Wait(MainWindow.TransferSlots.WaitAsync()); Wait(MainWindow.TransferSlots.WaitAsync()); Task download;
            try { download=(Task)Call(pane,"DownloadAsync")!; Field<TextBox>(pane,"localPath").Text=alternate; }
            finally { MainWindow.TransferSlots.Release(2); }
            Complete(download);
            foreach(var file in new[]{"żółć.bin","config.json",Path.Combine("documents","notes.txt")})
            { if(!SHA256.HashData(System.IO.File.ReadAllBytes(Path.Combine(source,file))).SequenceEqual(SHA256.HashData(System.IO.File.ReadAllBytes(Path.Combine(downloads,file)))))throw new Exception("Batch round-trip checksum differs: "+file); }
            if(Directory.EnumerateFileSystemEntries(alternate).Any())throw new Exception("Queued downloads followed changed panel destination");
            Refresh(source,original); Select(local,"żółć.bin","config.json"); var skipStart=Jobs(pane).Length; Complete((Task)Call(pane,"UploadAsync")!);
            if(Jobs(pane).Skip(skipStart).Any(j=>Value<TransferState>(j,"State")!=TransferState.Skipped))throw new Exception("Declined batch overwrite was not skipped");
            foreach(var name in new[]{"cancel-a.txt","cancel-b.txt","cancel-c.txt"})System.IO.File.WriteAllText(Path.Combine(source,name),name);
            Refresh(source,original); Select(local,"cancel-a.txt","cancel-b.txt","cancel-c.txt"); var cancelStart=Jobs(pane).Length;
            Wait(MainWindow.TransferSlots.WaitAsync()); Wait(MainWindow.TransferSlots.WaitAsync());
            try
            {
                var task=(Task)Call(pane,"UploadAsync")!; Wait((Task)Call(pane,"CancelTransfersAsync")!); Complete(task);
                if(Jobs(pane).Skip(cancelStart).Any(j=>Value<TransferState>(j,"State")!=TransferState.Cancelled))throw new Exception("Cancel-all did not cancel every queued batch member");
            }
            finally { MainWindow.TransferSlots.Release(2); }
            Field<TextBox>(pane,"remotePath").Text=changed;
            var cancelled=Jobs(pane)[cancelStart]; Value<Button>(cancelled,"Retry").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var retryUntil=DateTime.UtcNow.AddSeconds(45); while(Value<bool>(cancelled,"IsActive") && DateTime.UtcNow<retryUntil)RedesignChecks.Frames(1);
            if(Value<TransferState>(cancelled,"State")!=TransferState.Completed)throw new Exception("Retry after batch cancellation failed");
            var originalFile=inspection.ExistsAsync(original+"/cancel-a.txt",CancellationToken.None); Wait(originalFile); if(!originalFile.Result)throw new Exception("Retry lost original batch destination");
            var failedStart=Jobs(pane).Length;
            var entries=new[]{FileEntryFor(Path.Combine(source,"config.json")),FileEntryFor(Path.Combine(source,"missing.txt"))}; Complete((Task)Call(pane,"UploadFilesAsync",(object)entries)!,true);
            var last=Jobs(pane).Skip(failedStart).Select(j=>Value<TransferState>(j,"State")).ToArray();
            if(!last.Contains(TransferState.Failed) || !last.Contains(TransferState.Completed) || MainWindow.TransferSlots.CurrentCount!=2)throw new Exception("One failed batch member blocked others or leaked a slot");
            w.CaptureRenderedFrame()!.Save(Path.Combine(root,"batch-results.png"));
            Console.WriteLine("PASS actual OpenSSH SFTP batch: file/folder round trip and SHA-256, selection after refresh, queue snapshots keep original local/remote destinations, <=2 simultaneous transfers, declined overwrite, cancel-all, original-target retry, one failed member does not block others, slots restored");
        }
        finally
        {
            async Task RemoveFixture(string directory)
            {
                if(directory!=remoteRoot && !directory.StartsWith(remoteRoot+"/",StringComparison.Ordinal))throw new Exception("Fixture cleanup outside test root");
                foreach(var entry in await inspection.ListAsync(directory,CancellationToken.None))
                { if(entry.IsDirectory && !entry.IsSymbolicLink)await RemoveFixture(entry.FullPath); await inspection.DeleteAsync(entry,CancellationToken.None); }
            }
            Wait(RemoveFixture(remoteRoot)); Wait(inspection.DeleteAsync(new("fixture",remoteRoot,true,false,0,DateTime.Now,""),CancellationToken.None)); pane.Dispose(); w.Close();
        }
    }
}
