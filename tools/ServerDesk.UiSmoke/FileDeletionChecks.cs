using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using L=ServerDesk.Core.AppText;

internal static class FileDeletionChecks
{
    private static void Until(Func<bool> ready)
    { var deadline=DateTime.UtcNow.AddSeconds(45); while(!ready() && DateTime.UtcNow<deadline)RedesignChecks.Frames(1); if(!ready())throw new TimeoutException("Deletion workflow timed out"); }
    private static void Wait(Task task) { Until(()=>task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    private static T Find<T>(Window window,string name) where T:Control=>window.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static Window Dialog(MainWindow main) { Until(()=>main.OwnedWindows.Any(w=>w.Name=="FileDeletionDialog")); return main.OwnedWindows.Single(w=>w.Name=="FileDeletionDialog"); }
    private sealed class SlowStore : IDeletionFileSystem
    {
        private readonly LocalDeletionFileSystem local=new();
        internal bool BlockPreview,BlockSecondDelete,FailPreview;
        internal int Deletes;
        public bool IsLocal=>true;
        public async Task<FileEntry> ReadAsync(string path,CancellationToken ct)
        { if(FailPreview)throw new IOException("Owned preview fault fixture"); if(BlockPreview)await Task.Delay(Timeout.Infinite,ct); return await local.ReadAsync(path,ct); }
        public Task<IReadOnlyList<FileEntry>> ListAsync(string path,CancellationToken ct)=>local.ListAsync(path,ct);
        public async Task DeleteAsync(FileEntry expected,CancellationToken ct)
        { Deletes++; if(BlockSecondDelete && Deletes==2)await Task.Delay(Timeout.Infinite,ct); await local.DeleteAsync(expected,ct); }
    }
    internal static void Run(MainWindow main,ProfileRepository repository,string root)
    {
        var original=AppText.Language; var preferences=DesignSystem.Preferences;
        try
        {
            foreach(var language in new[]{"pl","en"})
            {
                AppText.SetLanguage(language);
                var fixture=Path.GetFullPath(Path.Combine(root,"delete-ui-"+Guid.NewGuid().ToString("N"))); Directory.CreateDirectory(fixture);
                var selected=Directory.CreateDirectory(Path.Combine(fixture,"folder żółć")).FullName; var nested=Directory.CreateDirectory(Path.Combine(selected,"nested")).FullName;
                File.WriteAllText(Path.Combine(selected,".hidden"),"owned hidden fixture"); File.WriteAllText(Path.Combine(nested,"notes.txt"),"owned nested fixture");
                var sentinel=Path.Combine(fixture,"keep.txt"); File.WriteAllText(sentinel,"keep");
                var local=new LocalDeletionFileSystem(); var entry=Result(local.ReadAsync(selected,default));
                var show=FileDeletionDialog.Show(main,new(local),[entry],default); var dialog=Dialog(main);
                Until(()=>Find<Button>(dialog,"FileDeletionConfirm").IsEnabled);
                if(!Directory.Exists(selected) || Find<Button>(dialog,"FileDeletionConfirm").IsDefault)throw new Exception("Preview mutated data or destructive action is the default");
                if(Find<ListBox>(dialog,"FileDeletionTargets").Items.Cast<FileEntry>().Single().FullPath!=selected)throw new Exception("Deletion preview changed selected user path");
                if(Find<TextBlock>(dialog,"FileDeletionSummary").Text!=L.F($"Pliki: {2} · katalogi: {2} · dowiązania: {0}"))throw new Exception("Preview counts or translation are wrong");
                foreach(var theme in new[]{"dark","light"})
                {
                    DesignSystem.ApplyPreferences(preferences with { InterfaceFontSize=16 }); DesignSystem.Apply(theme); dialog.Width=480; dialog.Height=440; RedesignChecks.Frames();
                    var button=Find<Button>(dialog,"FileDeletionConfirm"); var point=button.TranslatePoint(default,dialog)!.Value;
                    if(point.X<0 || point.Y<0 || point.X+button.Bounds.Width>dialog.ClientSize.Width+1 || point.Y+button.Bounds.Height>dialog.ClientSize.Height+1)throw new Exception("Deletion action escapes small dialog");
                    dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"delete-preview-{language}-{theme}.png"));
                }
                Click(Find<Button>(dialog,"FileDeletionCancel")); if(Result(show)!=null || !Directory.Exists(selected))throw new Exception("Preview cancellation deleted data");
                show=FileDeletionDialog.Show(main,new(local),[entry],default); dialog=Dialog(main); Until(()=>Find<Button>(dialog,"FileDeletionConfirm").IsEnabled);
                Click(Find<Button>(dialog,"FileDeletionConfirm")); Until(()=>!Directory.Exists(selected) && Find<Button>(dialog,"FileDeletionCancel").Content as string==L.T("Zamknij"));
                RedesignChecks.Frames(); dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"delete-complete-{language}.png")); Click(Find<Button>(dialog,"FileDeletionCancel"));
                var result=Result(show); if(result is not { Succeeded:true,Deleted:4 } || File.ReadAllText(sentinel)!="keep")throw new Exception("Recursive local delete or result counts failed");
                // Cancel while scanning: no mutation and no orphan dialog/task.
                var slow=new SlowStore { BlockPreview=true }; entry=Result(local.ReadAsync(sentinel,default));
                show=FileDeletionDialog.Show(main,new(slow),[entry],default); dialog=Dialog(main); Click(Find<Button>(dialog,"FileDeletionCancel"));
                if(Result(show)!=null || !File.Exists(sentinel))throw new Exception("Scan cancellation failed");
                slow=new() { FailPreview=true }; show=FileDeletionDialog.Show(main,new(slow),[entry],default); dialog=Dialog(main);
                Until(()=>Find<TextBlock>(dialog,"FileDeletionSummary").Text==L.T("Nie udało się przygotować usuwania. Żadne pliki nie zostały usunięte."));
                if(Find<Button>(dialog,"FileDeletionConfirm").IsEnabled)throw new Exception("Failed preview still permits deletion"); Click(Find<Button>(dialog,"FileDeletionCancel")); Wait(show);
                // Mid-delete cancellation leaves an explicit partial result open.
                var two=Path.Combine(fixture,"second.txt"); File.WriteAllText(two,"remaining"); slow=new() { BlockSecondDelete=true };
                show=FileDeletionDialog.Show(main,new(slow),[entry,Result(local.ReadAsync(two,default))],default); dialog=Dialog(main); Until(()=>Find<Button>(dialog,"FileDeletionConfirm").IsEnabled);
                Click(Find<Button>(dialog,"FileDeletionConfirm")); Until(()=>slow.Deletes==2); Click(Find<Button>(dialog,"FileDeletionCancel"));
                Until(()=>Find<Button>(dialog,"FileDeletionCancel").IsEnabled); RedesignChecks.Frames();
                if(show.IsCompleted || Find<TextBlock>(dialog,"FileDeletionSummary").Text!=L.F($"Usunięto {1} z {2} elementów"))throw new Exception("Partial cancellation result was lost");
                Click(Find<Button>(dialog,"FileDeletionCancel")); result=Result(show); if(result is not { Cancelled:true,Deleted:1 } || !File.Exists(two))throw new Exception("Cancellation changed remaining data");
            }
        }
        finally { AppText.SetLanguage(original); DesignSystem.ApplyPreferences(preferences); }
        if(OperatingSystem.IsLinux())
        {
            var fixture=Path.GetFullPath(Path.Combine(root,"local-links-"+Guid.NewGuid().ToString("N"))); Directory.CreateDirectory(fixture);
            var selected=Directory.CreateDirectory(Path.Combine(fixture,"selected")).FullName; var keep=Directory.CreateDirectory(Path.Combine(fixture,"keep")).FullName;
            var valuable=Path.Combine(keep,"valuable.txt"); File.WriteAllText(valuable,"keep target");
            File.CreateSymbolicLink(Path.Combine(selected,"file-link"),valuable); Directory.CreateSymbolicLink(Path.Combine(selected,"dir-link"),keep); File.CreateSymbolicLink(Path.Combine(selected,"dangling-link"),Path.Combine(keep,"missing"));
            var local=new LocalDeletionFileSystem(); var service=new FileDeletionService(local); var plan=Result(service.PrepareAsync([Result(local.ReadAsync(selected,default))],null,default));
            if(plan.Links!=3 || !Result(service.ExecuteAsync(plan,null,default)).Succeeded || File.ReadAllText(valuable)!="keep target")throw new Exception("Local Linux deletion followed a link");
            var alias=Path.Combine(fixture,"ancestor-link"); Directory.CreateSymbolicLink(alias,keep);
            try { Result(local.ReadAsync(Path.Combine(alias,"valuable.txt"),default)); throw new Exception("Local ancestor link accepted"); } catch(IOException) { }
            Console.WriteLine("PASS real Linux file/directory/dangling symlink removal preserves target and rejects a linked ancestor");
        }
        Console.WriteLine("PASS PL/EN deletion preview, exact scope/counts, Dark/Light small layouts, no implicit default delete, preview cancel/fault, recursive local delete, explicit partial cancellation result and retained siblings");
    }

    private static T Field<T>(SessionPane pane,string name)=>(T)typeof(SessionPane).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pane)!;
    private static Task Call(SessionPane pane,string method)=>(Task)typeof(SessionPane).GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pane,null)!;
    private sealed class InlineProgress(Action<FileDeletionProgress> action) : IProgress<FileDeletionProgress> { public void Report(FileDeletionProgress value)=>action(value); }
    internal static void Network(MainWindow main,ProfileRepository repo,string root,string host,string key)
    {
        var profile=new ServerProfile { Name="Owned SFTP deletion fixture",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
        using var raw=Result(factory.SftpAsync(profile,"serverdesk-test-passphrase",default));
        using var connection=new SftpSession(raw);
        var scope="/tmp/serverdesk-delete-"+Guid.NewGuid().ToString("N"); raw.CreateDirectory(scope);
        try
        {
            raw.CreateDirectory(scope+"/keep"); raw.WriteAllText(scope+"/keep/valuable.txt","outside selected tree");
            raw.CreateDirectory(scope+"/tree"); raw.CreateDirectory(scope+"/tree/sub"); raw.WriteAllText(scope+"/tree/.hidden","hidden fixture"); raw.WriteAllText(scope+"/tree/sub/notes.txt","nested fixture");
            raw.SymbolicLink(scope+"/keep/valuable.txt",scope+"/tree/file-link"); raw.SymbolicLink(scope+"/keep",scope+"/tree/dir-link"); raw.SymbolicLink(scope+"/missing",scope+"/tree/dangling-link");
            FileEntry Entry(string path)=>Result(connection.ListAsync(RemotePath.Parent(path),default)).Single(e=>e.FullPath==path);
            var service=connection.CreateDeletionService(); var tree=Entry(scope+"/tree"); var plan=Result(service.PrepareAsync([tree],null,default));
            if(plan.Links!=3 || plan.Files!=2 || plan.Directories!=2 || !raw.Exists(scope+"/tree/.hidden"))throw new Exception("Remote preview followed links, missed hidden data or mutated content");
            var result=Result(service.ExecuteAsync(plan,null,default));
            if(!result.Succeeded || result.Deleted!=7 || raw.Exists(tree.FullPath) || raw.ReadAllText(scope+"/keep/valuable.txt")!="outside selected tree")throw new Exception("Remote recursive deletion followed a symlink or lost scope: "+result.Error);
            raw.SymbolicLink(scope+"/keep/valuable.txt",scope+"/rename-link");
            Wait(connection.RenameAsync(scope+"/rename-link",scope+"/renamed-link",default));
            if(!Entry(scope+"/renamed-link").IsSymbolicLink || raw.ReadAllText(scope+"/keep/valuable.txt")!="outside selected tree")throw new Exception("Rename moved the link target");
            try { Wait(connection.ChmodAsync(scope+"/renamed-link",Convert.ToInt16("600",8),default)); throw new Exception("Chmod followed a link"); }
            catch(IOException) { }
            Wait(connection.DeleteAsync(Entry(scope+"/renamed-link"),default));
            if(raw.ReadAllText(scope+"/keep/valuable.txt")!="outside selected tree")throw new Exception("Nonrecursive delete removed the link target");
            raw.CreateDirectory(scope+"/replace"); raw.WriteAllText(scope+"/replace/valuable.txt","original fixture");
            plan=Result(service.PrepareAsync([Entry(scope+"/replace")],null,default));
            raw.RenameFile(scope+"/replace",scope+"/kept-original"); raw.SymbolicLink(scope+"/keep",scope+"/replace");
            result=Result(service.ExecuteAsync(plan,null,default));
            if(result.Deleted!=0 || result.Error==null || raw.ReadAllText(scope+"/keep/valuable.txt")!="outside selected tree" || raw.ReadAllText(scope+"/kept-original/valuable.txt")!="original fixture")throw new Exception("Changed ancestor symlink escaped prepared scope");
            raw.WriteAllText(scope+"/cancel-a","a"); raw.WriteAllText(scope+"/cancel-b","b"); using var cancel=new CancellationTokenSource();
            plan=Result(service.PrepareAsync([Entry(scope+"/cancel-a"),Entry(scope+"/cancel-b")],null,default));
            result=Result(service.ExecuteAsync(plan,new InlineProgress(_=>cancel.Cancel()),cancel.Token));
            if(result is not { Cancelled:true,Deleted:1 } || !raw.Exists(scope+"/cancel-b"))throw new Exception("Actual SFTP cancellation did not preserve remaining file");
            // Connection loss after an acknowledged delete, using a separate owned client.
            using(var faultRaw=Result(factory.SftpAsync(profile,"serverdesk-test-passphrase",default)))
            using(var faultSession=new SftpSession(faultRaw))
            {
                raw.WriteAllText(scope+"/fault-a","a"); raw.WriteAllText(scope+"/fault-b","b"); var faults=faultSession.CreateDeletionService();
                plan=Result(faults.PrepareAsync([Entry(scope+"/fault-a"),Entry(scope+"/fault-b")],null,default));
                result=Result(faults.ExecuteAsync(plan,new InlineProgress(p=> { if(p.Completed==1)faultRaw.Disconnect(); }),default));
                if(result.Deleted!=1 || result.Error==null || !raw.Exists(scope+"/fault-b"))throw new Exception("Actual SFTP loss did not stop/report partial work");
            }
            // Real pane selection/keyboard and dialog integration, with UI target changes after capture.
            using var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo); var window=new Window { Width=1100,Height=760,Content=pane }; window.Show();
            try
            {
                Wait(pane.ConnectAsync("serverdesk-test-passphrase",default)); Wait(pane.ShowAsync(1));
                Field<TextBox>(pane,"localPath").Text=Path.GetFullPath(root); Field<TextBox>(pane,"remotePath").Text=scope;
                Wait(Call(pane,"RefreshAsync")); var list=Field<ListBox>(pane,"remoteList");
                list.SelectedItems!.Clear(); foreach(var entry in list.Items.OfType<FileEntry>().Where(e=>e.Name is "cancel-b" or "fault-b"))list.SelectedItems.Add(entry);
                list.Focus(); window.KeyPress(Avalonia.Input.Key.Delete,Avalonia.Input.RawInputModifiers.None,Avalonia.Input.PhysicalKey.Delete,"");
                var dialog=Dialog(main); Until(()=>Find<Button>(dialog,"FileDeletionConfirm").IsEnabled);
                if(Find<TextBlock>(dialog,"FileDeletionContext").Text?.Contains(profile.Endpoint,StringComparison.Ordinal)!=true)throw new Exception("Remote delete dialog does not identify its server");
                Field<ComboBox>(pane,"fileTarget").SelectedIndex=0; Field<TextBox>(pane,"remotePath").Text=scope+"/keep";
                Click(Find<Button>(dialog,"FileDeletionConfirm")); Until(()=>Find<Button>(dialog,"FileDeletionCancel").Content as string==L.T("Zamknij") && !raw.Exists(scope+"/fault-b"));
                Click(Find<Button>(dialog,"FileDeletionCancel")); Until(()=>main.OwnedWindows.All(w=>w.Name!="FileDeletionDialog")); RedesignChecks.Frames();
                if(raw.Exists(scope+"/cancel-b") || raw.ReadAllText(scope+"/keep/valuable.txt")!="outside selected tree")throw new Exception("Pane delete used changed selection/target");
            }
            finally { pane.Dispose(); window.Close(); }
            Console.WriteLine("PASS actual OpenSSH/SFTP recursive hidden-file deletion, file/directory/dangling symlink unlink with target preservation, acknowledged partial cancellation and connection loss, real pane group Delete shortcut and captured target scope");
        }
        finally
        {
            // Cleanup only this random owned fixture; no traversal through links.
            if(!scope.StartsWith("/tmp/serverdesk-delete-",StringComparison.Ordinal) || scope["/tmp/serverdesk-delete-".Length..].Length!=32)throw new Exception("Unsafe remote cleanup scope");
            async Task Cleanup(string path)
            {
                foreach(var entry in raw.ListDirectory(path).Where(e=>e.Name is not ("." or "..")).ToArray())
                {
                    if(entry.FullName!=RemotePath.Join(path,entry.Name) || !entry.FullName.StartsWith(scope+"/",StringComparison.Ordinal))throw new Exception("Unsafe cleanup entry");
                    if(entry.IsDirectory && !entry.IsSymbolicLink)await Cleanup(entry.FullName); await entry.DeleteAsync();
                }
            }
            Wait(Cleanup(scope)); raw.DeleteDirectory(scope);
        }
    }
}
