using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class TransferErrorChecks
{
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static Task Call(SessionPane pane,string method,params object[] args)=>(Task)typeof(SessionPane).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pane,args)!;
    private static void Until(Func<bool> done,string message)
    { var watch=Stopwatch.StartNew(); while(!done()) { if(watch.Elapsed>TimeSpan.FromSeconds(40))throw new TimeoutException(message); RedesignChecks.Frames(1); } }
    private static void Wait(Task task) { Until(()=>task.IsCompleted,"Fixture task did not finish"); task.GetAwaiter().GetResult(); }
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    private static void Confirm(MainWindow main,Task task)
    {
        Until(()=>main.OwnedWindows.Any() || task.IsCompleted,"Overwrite confirmation did not open");
        if(main.OwnedWindows.SingleOrDefault() is {} dialog)
            dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Potwierdź").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Wait(task);
    }
    internal static void Run(MainWindow main,ProfileRepository repo,string root,string host,string key)
    {
        main.PreviewPreferences(main.Preferences with { ShowMetrics=false });
        var profile=new ServerProfile { Name="Owned transfer errors fixture",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
        using var inspection=new SftpSession(Result(factory.SftpAsync(profile,"serverdesk-test-passphrase",default)));
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language);
            using var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo);
            var window=new Window { Width=1000,Height=720,Content=pane }; window.Show();
            var remote="/tmp/serverdesk-transfer-errors-"+Guid.NewGuid().ToString("N"); var locked=remote+"/locked";
            var local=Path.GetFullPath(Path.Combine(root,"files-"+language+"-"+Guid.NewGuid().ToString("N"))); Directory.CreateDirectory(local);
            var old=Path.Combine(local,"original.txt"); File.WriteAllText(old,"Original must survive denied upload — żółć");
            var source=Path.Combine(local,"protected.txt"); File.WriteAllText(source,"Replacement after permissions are fixed — żółć");
            try
            {
                Wait(inspection.CreateDirectoryAsync(remote,default)); Wait(inspection.CreateDirectoryAsync(locked,default));
                Wait(inspection.UploadAsync(old,locked+"/protected.txt",false,new Progress<TransferProgress>(),default));
                Wait(pane.ConnectAsync("serverdesk-test-passphrase",default)); Wait(pane.ShowAsync(1));
                Field<TextBox>(pane,"localPath").Text=local; Field<TextBox>(pane,"remotePath").Text=locked; Wait(Call(pane,"RefreshAsync"));
                // Exercise the production prompt, not just the infrastructure API.
                var remoteList=Field<ListBox>(pane,"remoteList"); remoteList.SelectedItem=remoteList.Items.OfType<FileEntry>().Single(e=>e.Name=="protected.txt");
                foreach(var mode in new[]{"640","0644"})
                {
                    var chmod=Call(pane,"ChmodAsync"); Until(()=>main.OwnedWindows.Any(),"Chmod prompt did not open");
                    var prompt=main.OwnedWindows.Single(); prompt.GetVisualDescendants().OfType<TextBox>().Single().Text=mode;
                    prompt.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="OK").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(chmod);
                    if(Result(inspection.ListAsync(locked,default)).Single(e=>e.Name=="protected.txt").Permissions!=mode.TrimStart('0'))throw new Exception("Chmod prompt changed the wrong permission bits");
                }
                Wait(inspection.ChmodAsync(locked,500,default));
                if(Result(inspection.ListAsync(remote,default)).Single(e=>e.Name=="locked").Permissions!="500")throw new Exception("Fixture did not apply read-only directory permissions");
                Confirm(main,Call(pane,"UploadEntryAsync",LocalTransferSource.Read(source)));
                var failed=pane.TransferSnapshots.First();
                var permission=TransferErrorMessages.Describe(new UnauthorizedAccessException());
                if(failed.State!=TransferState.Failed || failed.Detail!=permission || !failed.CanRetry)throw new Exception($"Permission failure lost its localized guidance or retry action: {failed.State}, retry={failed.CanRetry}, {failed.Detail}");
                var verified=Path.Combine(local,"verified.txt");
                Wait(inspection.DownloadAsync(locked+"/protected.txt",verified,false,new Progress<TransferProgress>(),default));
                if(!File.ReadAllBytes(old).SequenceEqual(File.ReadAllBytes(verified)) || Result(inspection.ListAsync(locked,default)).Count!=1)throw new Exception("Denied upload changed its target or left a partial file");
                RedesignChecks.Frames();
                var detail=pane.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Text==permission);
                if(detail.TextWrapping!=TextWrapping.Wrap || detail.MaxLines!=3 || ToolTip.GetTip(detail) as string!=permission)throw new Exception("Failure guidance is not wrapped with a full tooltip");
                window.CaptureRenderedFrame()!.Save(Path.Combine(root,"transfer-permission-"+language+".png"));
                window.Width=640; RedesignChecks.Frames();
                if(detail.Bounds.Height<24)throw new Exception("Failure guidance did not wrap in a compact panel");
                window.CaptureRenderedFrame()!.Save(Path.Combine(root,"transfer-permission-compact-"+language+".png"));
                window.Width=1000; RedesignChecks.Frames();
                Wait(inspection.ChmodAsync(locked,700,default));
                Confirm(main,pane.RetryTransferAsync(failed.Id));
                Wait(inspection.DownloadAsync(locked+"/protected.txt",verified,true,new Progress<TransferProgress>(),default));
                if(pane.TransferSnapshots.First().State!=TransferState.Completed || !File.ReadAllBytes(source).SequenceEqual(File.ReadAllBytes(verified)))throw new Exception("Retry after fixing permissions failed or changed the payload");
                Wait(Call(pane,"UploadEntryAsync",new FileEntry("missing.txt",Path.Combine(local,"missing.txt"),false,false,0,DateTime.Now,"")));
                var missing=TransferErrorMessages.Describe(new FileNotFoundException());
                if(pane.TransferSnapshots.First().State!=TransferState.Failed || pane.TransferSnapshots.First().Detail!=missing)throw new Exception("Missing local file lost its localized guidance");
                var sentinel=Path.Combine(local,"gone.txt"); File.WriteAllText(sentinel,"Keep local target intact");
                Wait(Call(pane,"DownloadEntryAsync",new FileEntry("gone.txt",locked+"/gone.txt",false,false,20,DateTime.Now,"")));
                if(pane.TransferSnapshots.First().State!=TransferState.Failed || pane.TransferSnapshots.First().Detail!=missing || File.ReadAllText(sentinel)!="Keep local target intact" || MainWindow.TransferSlots.CurrentCount!=2)throw new Exception("Missing remote file changed its target, leaked a slot or hid its guidance");
                Console.WriteLine("PASS "+language+": production chmod 640/0644 sets exact bits; real permission denial preserves original/no partial file; wrapped localized guidance; retry after chmod succeeds with identical bytes; missing local/remote files preserve targets and release slots");
            }
            finally
            {
                Wait(pane.CloseAsync()); window.Close();
                if(Result(inspection.ExistsAsync(locked,default)))
                {
                    Wait(inspection.ChmodAsync(locked,700,default));
                    foreach(var entry in Result(inspection.ListAsync(locked,default)))
                    { if(entry.IsDirectory || entry.IsSymbolicLink || !entry.FullPath.StartsWith(locked+"/",StringComparison.Ordinal))throw new Exception("Unsafe fixture cleanup entry"); Wait(inspection.DeleteAsync(entry,default)); }
                    Wait(inspection.DeleteAsync(new("locked",locked,true,false,0,DateTime.Now,""),default));
                }
                if(Result(inspection.ExistsAsync(remote,default)))Wait(inspection.DeleteAsync(new("fixture",remote,true,false,0,DateTime.Now,""),default));
                AppText.SetLanguage("pl");
            }
        }
    }
}
