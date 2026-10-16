using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SftpWorkflowChecks
{
    private static T Field<T>(SessionPane pane,string name)=>(T)typeof(SessionPane).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(pane)!;
    private static Task Call(SessionPane pane,string method,params object[] args)=>(Task)typeof(SessionPane).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(pane,args)!;
    private static object LastJob(SessionPane pane)=>Field<IEnumerable>(pane,"transferJobs").Cast<object>().Last();
    private static T Property<T>(object job,string name)=>(T)job.GetType().GetProperty(name)!.GetValue(job)!;
    private static void AssertState(object job,TransferState state) { if(Property<TransferState>(job,"State")!=state)throw new Exception($"Expected {state}, got {Property<TransferState>(job,"State")}"); }
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Until(Func<bool> ready)
    { var until=DateTime.UtcNow.AddSeconds(45); while(!ready() && DateTime.UtcNow<until)RedesignChecks.Frames(1); if(!ready())throw new TimeoutException("SFTP workflow timed out"); }
    private static void Wait(Task task) { Until(()=>task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static FileEntry File(string path)=>new(Path.GetFileName(path),path,Directory.Exists(path),false,System.IO.File.Exists(path)?new FileInfo(path).Length:0,DateTime.Now,"");
    internal static void FocusControls(MainWindow main,ProfileRepository repo)
    {
        using var pane=new SessionPane(main,new() { Name="Focus fixture",Host="fixture.invalid",Username="user" },new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
        pane.Children.OfType<Iciclecreek.Terminal.TerminalControl>().Single().IsVisible=false; Field<Grid>(pane,"files").IsVisible=true;
        var window=new Window { Width=1200,Height=700,Content=pane }; window.Show(); RedesignChecks.Frames();
        foreach(var (label,side) in new[]{("Lokalny kierunek sortowania",0),("Zdalny kierunek sortowania",1),("Lokalny katalog nadrzędny",0),("Zdalny katalog nadrzędny",1)})
        {
            var button=window.GetVisualDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)==label); button.Focus(); RedesignChecks.Frames(2);
            if(Field<ComboBox>(pane,"fileTarget").SelectedIndex!=side)throw new Exception("Focused SFTP control did not activate its panel: "+label);
            window.KeyPress(Key.L,RawInputModifiers.Control,PhysicalKey.L,"l"); if(!(side==0?Field<TextBox>(pane,"localPath"):Field<TextBox>(pane,"remotePath")).IsKeyboardFocusWithin)throw new Exception("Ctrl+L used stale panel after "+label);
        }
        window.Close(); Console.WriteLine("PASS SFTP direction/parent control focus sets active panel and Ctrl+L follows it");
    }
    internal static void Run(MainWindow main,ProfileRepository repo,string root,string host,string key)
    {
        var profile=new ServerProfile { Name="SFTP workflow",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
        using var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo) { IsActive=true };
        var window=new Window { Width=1200,Height=820,Content=pane }; window.Show(); Wait(pane.ConnectAsync("serverdesk-test-passphrase",CancellationToken.None)); Wait(pane.ShowAsync(1));
        var local=Path.Combine(root,"sftp-fixture-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(local);
        var remote="/tmp/serverdesk-ui-files-"+Guid.NewGuid().ToString("N");
        var connect=factory.SftpAsync(profile,"serverdesk-test-passphrase",CancellationToken.None); Until(()=>connect.IsCompleted);
        using var connection=new SftpSession(connect.GetAwaiter().GetResult()); Wait(connection.CreateDirectoryAsync(remote,CancellationToken.None));
        var original=DesignSystem.Preferences;
        try
        {
            Directory.CreateDirectory(Path.Combine(local,"documents")); System.IO.File.WriteAllText(Path.Combine(local,"documents","notes.txt"),"Folder transfer żółć");
            var payload=RandomNumberGenerator.GetBytes(1<<20); var source=Path.Combine(local,"config.json"); System.IO.File.WriteAllBytes(source,payload);
            System.IO.File.WriteAllText(Path.Combine(local,".hidden"),"hidden"); System.IO.File.WriteAllText(Path.Combine(local,"z.log"),"log");
            foreach(var name in new[]{"file10.txt","file2.log","file1.txt"})System.IO.File.WriteAllText(Path.Combine(local,name),name);
            Field<TextBox>(pane,"localPath").Text=local; Field<TextBox>(pane,"remotePath").Text=remote; Wait(Call(pane,"RefreshAsync"));
            var localList=Field<ListBox>(pane,"localList"); var remoteList=Field<ListBox>(pane,"remoteList");
            if(!localList.Items.Cast<FileEntry>().Where(e=>e.Name.StartsWith("file",StringComparison.Ordinal)).Select(e=>e.Name).SequenceEqual(new[]{"file1.txt","file2.log","file10.txt"}))throw new Exception("Real local directory is not sorted naturally");
            localList.SelectedItem=localList.Items.Cast<FileEntry>().Single(e=>e.Name=="config.json");
            Field<TextBox>(pane,"localFilter").Text="CONFIG"; Field<ComboBox>(pane,"remoteSort").SelectedIndex=2;
            if(localList.ItemCount!=2 || remoteList.ItemCount!=1 || (localList.SelectedItem as FileEntry)?.Name!="config.json")throw new Exception("Independent filters lost selection or affected opposite pane");
            Wait(Call(pane,"RefreshAsync")); if((localList.SelectedItem as FileEntry)?.Name!="config.json")throw new Exception("Refresh lost stable file selection");
            Field<TextBox>(pane,"localFilter").Text="absent-file"; RedesignChecks.Frames(2); if(!Field<TextBlock>(pane,"localEmpty").IsVisible)throw new Exception($"No-results message is hidden by parent row; rows={localList.ItemCount}, width={Field<Grid>(pane,"files").Bounds.Width}, remote={Field<ComboBox>(pane,"fileTarget").SelectedIndex}");
            Field<TextBox>(pane,"localFilter").Text="";
            Field<CheckBox>(pane,"hidden").IsChecked=true; if(!localList.Items.Cast<FileEntry>().Any(e=>e.Name==".hidden"))throw new Exception("Hidden toggle failed"); Field<CheckBox>(pane,"hidden").IsChecked=false;
            Field<ComboBox>(pane,"localSort").SelectedIndex=1; Click(window.GetVisualDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)=="Lokalny kierunek sortowania"));
            if(localList.Items.Cast<FileEntry>().First(e=>!e.IsDirectory).Name!="config.json")throw new Exception("Descending size sort failed");
            localList.Focus(); if(Field<ComboBox>(pane,"fileTarget").SelectedIndex!=0)throw new Exception("Focused file list did not set operation target");
            localList.SelectedItem=localList.Items.Cast<FileEntry>().Single(e=>e.Name=="documents");
            window.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r"); Until(()=>Field<TextBox>(pane,"localPath").Text==Path.Combine(local,"documents") && localList.Items.Cast<FileEntry>().Any(e=>e.Name=="notes.txt"));
            window.KeyPress(Key.Up,RawInputModifiers.Alt,PhysicalKey.ArrowUp,""); Until(()=>Field<TextBox>(pane,"localPath").Text==local && localList.Items.Cast<FileEntry>().Any(e=>e.Name=="config.json"));
            localList.Focus(); window.KeyPress(Key.L,RawInputModifiers.Control,PhysicalKey.L,"l"); if(!Field<TextBox>(pane,"localPath").IsKeyboardFocusWithin)throw new Exception("Ctrl+L did not focus active path");
            System.IO.File.WriteAllText(Path.Combine(local,"new.txt"),"F5"); window.KeyPress(Key.F5,RawInputModifiers.None,PhysicalKey.F5,""); Until(()=>localList.Items.Cast<FileEntry>().Any(e=>e.Name=="new.txt"));
            Wait(Call(pane,"UploadEntryAsync",File(source))); AssertState(LastJob(pane),TransferState.Completed);
            var downloaded=Path.Combine(local,"downloaded"); Directory.CreateDirectory(downloaded); Field<TextBox>(pane,"localPath").Text=downloaded; Wait(Call(pane,"RefreshAsync"));
            var remoteFile=remoteList.Items.Cast<FileEntry>().Single(e=>e.Name=="config.json"); Wait(Call(pane,"DownloadEntryAsync",remoteFile)); AssertState(LastJob(pane),TransferState.Completed);
            if(!System.IO.File.ReadAllBytes(Path.Combine(downloaded,"config.json")).SequenceEqual(payload))throw new Exception("SFTP round-trip payload differs");
            foreach(var name in new[]{"file10.txt","file2.log","file1.txt"})Wait(Call(pane,"UploadEntryAsync",File(Path.Combine(local,name))));
            Field<ComboBox>(pane,"remoteSort").SelectedIndex=0;
            if(!remoteList.Items.Cast<FileEntry>().Where(e=>e.Name.StartsWith("file",StringComparison.Ordinal)).Select(e=>e.Name).SequenceEqual(new[]{"file1.txt","file2.log","file10.txt"}))throw new Exception("Real remote SFTP names are not sorted naturally");
            Click(window.GetVisualDescendants().OfType<Button>().Single(b=>b.Name=="RemoteSort_Name"));
            if(!remoteList.Items.Cast<FileEntry>().Where(e=>e.Name.StartsWith("file",StringComparison.Ordinal)).Select(e=>e.Name).SequenceEqual(new[]{"file10.txt","file2.log","file1.txt"}))throw new Exception("Real SFTP header direction did not change");
            Wait(Call(pane,"RefreshAsync")); if(!Field<bool>(pane,"remoteDescending"))throw new Exception("Network refresh reset SFTP direction");
            Field<ComboBox>(pane,"remoteSort").SelectedIndex=2;
            var declined=Call(pane,"UploadEntryAsync",File(source)); Until(()=>main.OwnedWindows.Count>0); Click(main.OwnedWindows.Single().GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Anuluj")); Wait(declined); AssertState(LastJob(pane),TransferState.Skipped);
            var interrupted=Call(pane,"UploadEntryAsync",File(source)); Until(()=>main.OwnedWindows.Count>0); var active=LastJob(pane); AssertState(active,TransferState.Running);
            Property<CancellationTokenSource>(active,"Cancellation").Cancel(); Wait(interrupted); AssertState(active,TransferState.Cancelled); if(main.OwnedWindows.Count!=0)throw new Exception("Cancellation left overwrite prompt open");
            var folder=File(Path.Combine(local,"documents")); Wait(Call(pane,"UploadEntryAsync",folder)); AssertState(LastJob(pane),TransferState.Completed);
            declined=Call(pane,"UploadEntryAsync",folder); Until(()=>main.OwnedWindows.Count>0); Click(main.OwnedWindows.Single().GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Anuluj")); Wait(declined); AssertState(LastJob(pane),TransferState.CompletedWithSkips);
            Wait(MainWindow.TransferSlots.WaitAsync()); Wait(MainWindow.TransferSlots.WaitAsync());
            object cancelled;
            try
            {
                var queued=Call(pane,"UploadEntryAsync",File(Path.Combine(local,"new.txt"))); cancelled=LastJob(pane); AssertState(cancelled,TransferState.Queued);
                RedesignChecks.Frames(); window.CaptureRenderedFrame()!.Save(Path.Combine(root,"sftp-queue-pending.png"));
                Click(Property<Button>(cancelled,"Cancel")); Wait(queued); AssertState(cancelled,TransferState.Cancelled);
            }
            finally { MainWindow.TransferSlots.Release(2); }
            var other=remote+"/other"; Wait(connection.CreateDirectoryAsync(other,CancellationToken.None)); Field<TextBox>(pane,"remotePath").Text=other; Wait(Call(pane,"RefreshAsync"));
            Click(Property<Button>(cancelled,"Retry")); Until(()=>!Property<bool>(cancelled,"IsActive")); AssertState(cancelled,TransferState.Completed);
            var originalTarget=connection.ExistsAsync(remote+"/new.txt",CancellationToken.None); Until(()=>originalTarget.IsCompleted); if(!originalTarget.Result)throw new Exception("Retry used changed panel target instead of original destination");
            var missing=Path.Combine(local,"recover.txt"); Wait(Call(pane,"UploadEntryAsync",File(missing))); var failed=LastJob(pane); AssertState(failed,TransferState.Failed);
            System.IO.File.WriteAllText(missing,"Recovered"); Click(Property<Button>(failed,"Retry")); Until(()=>!Property<bool>(failed,"IsActive")); AssertState(failed,TransferState.Completed);
            Field<TextBox>(pane,"localPath").Text=local; Field<TextBox>(pane,"remotePath").Text=remote; Wait(Call(pane,"RefreshAsync"));
            foreach(var width in new[]{1200,650})foreach(var font in new[]{13,16})
            {
                DesignSystem.ApplyPreferences(original with { InterfaceFontSize=font }); window.Width=width; window.Height=820; RedesignChecks.Frames();
                foreach(var control in window.GetVisualDescendants().OfType<Control>().Where(c=>c.IsEffectivelyVisible && (c==Field<TextBox>(pane,"remoteFilter") || c==Field<ComboBox>(pane,"remoteSort") || c==Field<StackPanel>(pane,"transferFooter"))))
                { var point=control.TranslatePoint(default,window)!.Value; if(point.X<0 || point.X+control.Bounds.Width>window.ClientSize.Width+1)throw new Exception("SFTP control escapes window"); }
                window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"sftp-workflow-{width}-font{font}.png"));
            }
            window.Width=1200; Field<ComboBox>(pane,"fileTarget").SelectedIndex=1; DesignSystem.ApplyPreferences(original); RedesignChecks.Frames(); window.CaptureRenderedFrame()!.Save(Path.Combine(root,"sftp-workflow.png"));
            Click(Field<Button>(pane,"queueToggle")); RedesignChecks.Frames(); if(Field<ScrollViewer>(pane,"queueScroll").IsVisible)throw new Exception("Queue cannot collapse"); pane.ExpandTransferQueue(); RedesignChecks.Frames(); if(!Field<ScrollViewer>(pane,"queueScroll").IsVisible)throw new Exception("Queue does not expand");
            Click(Field<Button>(pane,"queueClear")); if(Field<IEnumerable>(pane,"transferJobs").Cast<object>().Any() || pane.ActiveTransfers!=0)throw new Exception("Finished queue could not be cleared");
            for(var i=0;i<52;i++)Wait(Call(pane,"UploadEntryAsync",File(Path.Combine(local,"missing-history-"+i+".txt"))));
            if(Field<IEnumerable>(pane,"transferJobs").Cast<object>().Count()!=50)throw new Exception("Finished history is not bounded");
            Click(Field<Button>(pane,"queueClear"));
            if(MainWindow.TransferSlots.CurrentCount!=2)throw new Exception("Transfer slot leaked");
            Console.WriteLine("PASS real SFTP independent filters/sorts/selection, scoped navigation shortcuts, 1 MiB checksum round trip, declined overwrite/skipped folder, queued/active cancellation including pending overwrite dialog, original-target retry, failed-job recovery, collapsed/cleared queue, 50-job history limit, 650/1200px font13/16 layout");
        }
        finally
        {
            DesignSystem.ApplyPreferences(original);
            // Remove only the random remote fixture created by this run.
            async Task Remove(string directory)
            { var entries=await connection.ListAsync(directory,CancellationToken.None); foreach(var entry in entries) { if(entry.IsDirectory && !entry.IsSymbolicLink)await Remove(entry.FullPath); await connection.DeleteAsync(entry,CancellationToken.None); } }
            Wait(Remove(remote)); Wait(connection.DeleteAsync(new("fixture",remote,true,false,0,DateTime.Now,""),CancellationToken.None)); pane.Dispose(); window.Close();
        }
    }
}
