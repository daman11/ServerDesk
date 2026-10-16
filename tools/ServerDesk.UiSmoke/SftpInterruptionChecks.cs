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

internal static class SftpInterruptionChecks
{
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static Task Call(object owner,string method,params object[] args)=>(Task)owner.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(owner,args)!;
    private static void Until(Func<bool> done,string message)
    { var watch=Stopwatch.StartNew(); while(!done()) { if(watch.Elapsed>TimeSpan.FromSeconds(40))throw new TimeoutException(message); RedesignChecks.Frames(1); } }
    private static void Wait(Task task) { Until(()=>task.IsCompleted,"Fixture operation timed out"); task.GetAwaiter().GetResult(); }
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    private static void Accept(MainWindow main)
    {
        Until(()=>main.OwnedWindows.Any(),"Overwrite confirmation did not open");
        main.OwnedWindows.Single().GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Potwierdź").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    internal static void Run(MainWindow main,ProfileRepository repo,string root,string host,string key)
    {
        main.PreviewPreferences(main.Preferences with { ShowMetrics=false });
        using var proxy=new SftpFaultProxy(host,22222);
        var direct=new ServerProfile { Name="Owned interrupted transfer",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var profile=direct with { Host="127.0.0.1",Port=proxy.Port };
        var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
        using var inspection=new SftpSession(Result(factory.SftpAsync(direct,"serverdesk-test-passphrase",default)));
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language);
            using var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo); main.RegisterPane(pane);
            var id=Guid.NewGuid().ToString("N"); var remote="/tmp/serverdesk-interruption-"+id;
            var local=Path.GetFullPath(Path.Combine(root,"files-"+id)); Directory.CreateDirectory(local);
            var source=Path.Combine(local,"large.bin"); var verified=Path.Combine(local,"verified.bin");
            var original=RandomNumberGenerator.GetBytes(1024); File.WriteAllBytes(source,original);
            var sentinel=remote+"/.serverdesk-00000000000000000000000000000000.part";
            byte[] ReadRemote(string path)
            { Wait(inspection.DownloadAsync(path,verified,File.Exists(verified),new Progress<TransferProgress>(),default)); return SHA256.HashData(File.ReadAllBytes(verified)); }
            void Same(byte[] expected,string path) { if(!SHA256.HashData(expected).SequenceEqual(ReadRemote(path)))throw new Exception("Remote payload changed unexpectedly"); }
            SessionPane.TransferSnapshot Last()=>pane.TransferSnapshots.First();
            Task Upload(out Task<SftpFaultProxy.Connection> next) { next=proxy.ArmNext(); return Call(pane,"UploadEntryAsync",LocalTransferSource.Read(source)); }
            try
            {
                Wait(inspection.CreateDirectoryAsync(remote,default));
                Wait(inspection.UploadAsync(source,remote+"/large.bin",false,new Progress<TransferProgress>(),default));
                Wait(inspection.UploadAsync(source,sentinel,false,new Progress<TransferProgress>(),default));
                Wait(pane.ConnectAsync("serverdesk-test-passphrase",default)); Wait(pane.ShowAsync(1));
                Field<TextBox>(pane,"localPath").Text=local; Field<TextBox>(pane,"remotePath").Text=remote; Wait(Call(pane,"RefreshAsync"));
                File.WriteAllBytes(source,RandomNumberGenerator.GetBytes(4<<20));
                var upload=Upload(out var next); var relay=Result(next); Accept(main); Until(()=>Last().Progress>0,"Upload made no progress"); relay.Dispose(); Wait(upload);
                var failed=Last();
                var part=Result(inspection.ListAsync(remote,default)).Single(e=>e.Name.StartsWith(".serverdesk-",StringComparison.Ordinal) && e.FullPath!=sentinel);
                var partialHash=ReadRemote(part.FullPath);
                if(failed.State!=TransferState.Failed || !failed.HasCleanupWarning || !failed.CanRetry || !failed.Detail.Contains(part.FullPath,StringComparison.Ordinal) || !failed.Status.Contains(AppText.T(" · sprawdź plik częściowy"),StringComparison.Ordinal))throw new Exception("Dropped upload hid the possible partial path or lost retry");
                Same(original,remote+"/large.bin");
                var replacement=RandomNumberGenerator.GetBytes(4<<20); File.WriteAllBytes(source,replacement);
                upload=pane.RetryTransferAsync(failed.Id); Accept(main); Wait(upload);
                if(Last().State!=TransferState.Completed || !Last().HasCleanupWarning || !Last().Detail.Contains(part.FullPath,StringComparison.Ordinal))throw new Exception("Successful retry forgot the earlier cleanup warning");
                Same(replacement,remote+"/large.bin");
                if(!ReadRemote(part.FullPath).SequenceEqual(partialHash))throw new Exception("Retry changed an older attempt's partial file"); Same(original,sentinel);
                Field<CheckBox>(pane,"hidden").IsChecked=true; RedesignChecks.Frames(); Wait(Field<Task>(pane,"fileViewUpdate"));
                if(!Field<ListBox>(pane,"remoteList").Items.OfType<FileEntry>().Any(e=>e.FullPath==part.FullPath))throw new Exception("Hidden partial file cannot be inspected through SFTP");
                Wait(Call(main,"SelectSectionAsync","transfers")); RedesignChecks.Frames();
                Field<ComboBox>(main,"transferFilter").SelectedIndex=3;
                Field<TextBox>(main,"transferSearch").Text=part.Name; RedesignChecks.Frames();
                if(Field<StackPanel>(main,"transferRows").Children.OfType<Border>().Count(b=>b.IsVisible)!=1)throw new Exception("Successful retry with warning is absent from attention filtering or partial-path search");
                if(!Field<StackPanel>(main,"transferRows").GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==Last().Detail))throw new Exception("Transfers overview lost cleanup guidance");
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,"partial-warning-"+language+".png"));
                Field<ComboBox>(main,"transferFilter").SelectedIndex=0; Field<TextBox>(main,"transferSearch").Text="";
                Wait(Call(main,"SelectSectionAsync","connections"));
                // An interrupted download cleans its local temporary and preserves
                // a different original. The terminal connection stays alive.
                var localOriginal=RandomNumberGenerator.GetBytes(4<<20); File.WriteAllBytes(source,localOriginal);
                var entry=Result(inspection.ListAsync(remote,default)).Single(e=>e.Name=="large.bin");
                next=proxy.ArmNext(); var download=Call(pane,"DownloadEntryAsync",entry); relay=Result(next); Accept(main); Until(()=>Last().Progress>0,"Download made no progress");
                var clock=Stopwatch.StartNew(); pane.CancelTransfer(Last().Id); Wait(download); clock.Stop();
                if(Last().State!=TransferState.Cancelled || Last().HasCleanupWarning || !File.ReadAllBytes(source).SequenceEqual(localOriginal) || Directory.GetFiles(local,"*.part").Length!=0 || !Last().Detail.Contains(AppText.T("Anulowano. Ukończone pliki pozostają na miejscu. Ponów przesyła plik od początku; nadpisanie wymaga potwierdzenia."),StringComparison.Ordinal))throw new Exception("Download cancellation changed its original, leaked a partial or lost guidance");
                download=pane.RetryTransferAsync(Last().Id); Accept(main); Wait(download);
                if(Last().State!=TransferState.Completed || !File.ReadAllBytes(source).SequenceEqual(replacement))throw new Exception("Download retry did not restart and verify its payload");
                // Replace only the active owned temporary leaf with a symlink.
                // Cleanup must unlink that entry, never the sentinel it targets.
                var before=Result(inspection.ListAsync(remote,default)).Select(e=>e.FullPath).ToHashSet();
                File.WriteAllBytes(source,RandomNumberGenerator.GetBytes(4<<20));
                upload=Upload(out next); relay=Result(next); Accept(main); Until(()=>Last().Progress>0,"Symlink-race upload made no progress"); relay.Paused=true;
                var swapped=Result(inspection.ListAsync(remote,default)).Single(e=>e.Name.StartsWith(".serverdesk-",StringComparison.Ordinal) && !before.Contains(e.FullPath));
                Wait(inspection.RenameAsync(swapped.FullPath,remote+"/held.bin",default));
                Wait(inspection.CreateSymbolicLinkAsync(sentinel,swapped.FullPath,default));
                pane.CancelTransfer(Last().Id); relay.Paused=false; Wait(upload);
                if(Last().State!=TransferState.Cancelled || Last().HasCleanupWarning || Result(inspection.ListAsync(remote,default)).Any(e=>e.FullPath==swapped.FullPath))throw new Exception("Cleanup did not unlink the replaced temporary entry");
                Same(original,sentinel); Same(replacement,remote+"/large.bin");
                // Silent loss must preserve Cancelled, even if remote cleanup
                // cannot be confirmed after the bounded transport abort.
                File.WriteAllBytes(source,RandomNumberGenerator.GetBytes(4<<20));
                upload=Upload(out next); relay=Result(next); Accept(main); Until(()=>Last().Progress>0,"Silent-loss upload made no progress"); relay.Paused=true;
                clock.Restart(); pane.CancelTransfer(Last().Id); Wait(upload); clock.Stop(); relay.Dispose();
                if(clock.Elapsed>TimeSpan.FromSeconds(5) || Last().State!=TransferState.Cancelled || !Last().HasCleanupWarning || !Last().CanRetry || !pane.IsConnected || MainWindow.TransferSlots.CurrentCount!=2)throw new Exception("Silent-loss cancellation lost its state, warning, live SSH or slots");
                Same(replacement,remote+"/large.bin"); Same(original,sentinel);
                // Explicitly remove just the captured orphan through the normal
                // preview/execute service, preserving the unrelated sentinel.
                var deletion=inspection.CreateDeletionService(); var plan=Result(deletion.PrepareAsync([part],null,default));
                if(!Result(deletion.ExecuteAsync(plan,null,default)).Succeeded || Result(inspection.ExistsAsync(part.FullPath,default)))throw new Exception("Explicit partial-file cleanup failed"); Same(original,sentinel);
                Console.WriteLine($"PASS {language}: dropped overwrite preserves original; exact partial path/status/overview; retry verifies SHA-256 and retains old warning/file; hidden inspection and scoped deletion; cancelled download cleans local partial and retries; cleanup unlinks replaced temporary symlink without touching target; silent cancellation {clock.ElapsedMilliseconds}ms preserves original, Cancelled, live SSH and both slots");
            }
            finally
            {
                Wait(pane.CloseAsync()); main.RemovePane(pane);
                foreach(var entry in Result(inspection.ListAsync(remote,default)))
                { if(entry.IsDirectory || entry.IsSymbolicLink || !entry.FullPath.StartsWith(remote+"/",StringComparison.Ordinal))throw new Exception("Unsafe owned-fixture cleanup path"); Wait(inspection.DeleteAsync(entry,default)); }
                Wait(inspection.DeleteAsync(new("fixture",remote,true,false,0,DateTime.Now,""),default)); AppText.SetLanguage("pl");
            }
        }
    }
}
