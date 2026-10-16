using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class SftpLinkChecks
{
    private static T Field<T>(SessionPane pane,string name)=>(T)typeof(SessionPane).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(pane)!;
    private static Task Call(SessionPane pane,string name,params object[] args)=>(Task)typeof(SessionPane).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(pane,args)!;
    private static void Until(Func<bool> condition) { var limit=DateTime.UtcNow.AddSeconds(45); while(!condition() && DateTime.UtcNow<limit)RedesignChecks.Frames(1); if(!condition())throw new TimeoutException("SFTP link fixture timed out"); }
    private static void Wait(Task task) { Until(()=>task.IsCompleted); task.GetAwaiter().GetResult(); }
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    private static void Click(Window dialog,string tag)=>dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Fails(Task task)
    { try { Wait(task); } catch(IOException) { return; } throw new Exception("Unsafe or unavailable link operation did not fail"); }
    private static string Quote(string text)=>"'"+text.Replace("'","'\\''")+"'";
    internal static void Run(MainWindow main,ProfileRepository repo,string root,string host,string key)
    {
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language);
            var profile=new ServerProfile { Name="MVP SFTP links",Host=host,Port=22222,Username="damian",Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
            var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
            using var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo);
            var window=new Window { Width=1200,Height=820,Content=pane }; window.Show(); Wait(pane.ConnectAsync("serverdesk-test-passphrase",default)); Wait(pane.ShowAsync(1));
            using var ssh=Result(factory.SshAsync(profile,"serverdesk-test-passphrase",default));
            using var connection=new SftpSession(Result(factory.SftpAsync(profile,"serverdesk-test-passphrase",default)));
            var remote="/tmp/serverdesk-link-fixture-"+Guid.NewGuid().ToString("N"); var local=Path.Combine(root,"links-"+language+"-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(local); Wait(connection.CreateDirectoryAsync(remote,default));
            void Command(string command) { using var result=ssh.RunCommand(command); if(result.ExitStatus!=0)throw new Exception("Fixture command failed: "+result.Error); }
            void Select(string name) { var list=Field<ListBox>(pane,"remoteList"); list.SelectedItem=list.Items.OfType<FileEntry>().Single(e=>e.Name==name); Field<ComboBox>(pane,"fileTarget").SelectedIndex=1; }
            FileEntry Remote(string name)=>Field<ListBox>(pane,"remoteList").Items.OfType<FileEntry>().Single(e=>e.Name==name);
            void Refresh() { Field<TextBox>(pane,"remotePath").Text=remote; Field<TextBox>(pane,"localPath").Text=local; Wait(Call(pane,"RefreshAsync")); }
            var source=Path.Combine(local,"original żółć.txt"); var progress=new Progress<TransferProgress>(_=>{});
            try
            {
                System.IO.File.WriteAllText(source,"Original content żółć λ");
                Wait(connection.UploadAsync(source,remote+"/original.txt",false,progress,default));
                var other=Path.Combine(local,"different.txt"); System.IO.File.WriteAllText(other,"Different data — an alias change must never copy this file instead");
                Wait(connection.CreateDirectoryAsync(remote+"/directory",default)); Wait(connection.UploadAsync(other,remote+"/directory/inside.txt",false,progress,default));
                Wait(connection.CreateSymbolicLinkAsync(remote+"/original.txt",remote+"/file.link",default));
                Wait(connection.CreateSymbolicLinkAsync(remote+"/directory",remote+"/directory.link",default));
                Command("ln -s -- 'missing-target' "+Quote(remote+"/dangling.link"));
                Command("ln -s -- 'loop2.link' "+Quote(remote+"/loop1.link")+" && ln -s -- 'loop1.link' "+Quote(remote+"/loop2.link"));
                Command("ln -s -- '.' "+Quote(remote+"/directory/cycle.link"));
                Refresh();
                var relative=string.Concat(Enumerable.Repeat("../",connection.WorkingDirectory.Split('/',StringSplitOptions.RemoveEmptyEntries).Length))+remote.TrimStart('/');
                Field<TextBox>(pane,"remotePath").Text=relative; Wait(Call(pane,"RefreshAsync"));
                if(Field<TextBox>(pane,"remotePath").Text!=remote)throw new Exception("Relative SFTP directory was not resolved to the displayed actual location");
                if(!Remote("file.link").IsSymbolicLink || !Result(connection.ReadTransferSourceAsync(Remote("directory.link"),true,default)).IsDirectory)throw new Exception("Remote link metadata/resolve failed");
                Fails(connection.ReadTransferSourceAsync(Remote("dangling.link"),true,default));
                Fails(connection.ReadTransferSourceAsync(Remote("loop1.link"),true,default));
                Fails(connection.CreateSymbolicLinkAsync(remote+"/original.txt",remote+"/dangling.link",default));
                Fails(connection.EnsureTransferDirectoryAsync(remote+"/directory.link",default));
                Fails(connection.UploadAsync(source,remote+"/file.link",true,progress,default));
                if(!Result(connection.ListAsync(remote,default)).Single(e=>e.Name=="file.link").IsSymbolicLink)throw new Exception("Protected target link was replaced");
                var rejected=Path.Combine(local,"check-original.txt"); Wait(connection.DownloadAsync(remote+"/original.txt",rejected,false,progress,default)); if(System.IO.File.ReadAllText(rejected)!=System.IO.File.ReadAllText(source))throw new Exception("Target protection modified the link target");

                Select("directory.link"); Wait(Call(pane,"OpenDirectoryAsync",true));
                if(Field<TextBox>(pane,"remotePath").Text!=remote+"/directory" || !Field<ListBox>(pane,"remoteList").Items.OfType<FileEntry>().Any(e=>e.Name=="inside.txt"))throw new Exception("Directory link navigation did not reveal the resolved location");
                Refresh(); Select("file.link"); var details=Call(pane,"FileDetailsAsync"); Until(()=>main.OwnedWindows.Count>0);
                RedesignChecks.Frames();
                if(!main.OwnedWindows.Single().GetVisualDescendants().OfType<SelectableTextBlock>().Any(t=>t.Text?.Contains(remote+"/original.txt")==true))throw new Exception("Link details do not contain resolved target");
                main.OwnedWindows.Single().Close(); Wait(details);

                var copy=Call(pane,"CopyLinkContentsAsync"); Until(()=>main.OwnedWindows.Count>0); var dialog=main.OwnedWindows.Single();
                var text=string.Join("\n",dialog.GetVisualDescendants().OfType<TextBlock>().Select(t=>t.Text));
                if(!text.Contains(remote+"/original.txt") || !text.Contains(Path.Combine(local,"file.link")))throw new Exception("Copy confirmation omits source or destination");
                DesignSystem.ApplyPreferences(preferences with { InterfaceFontSize=16 }); DesignSystem.Apply(language=="pl"?"graphite":"light"); dialog.Width=680; RedesignChecks.Frames(); dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"link-confirm-{language}.png"));
                Click(dialog,"Anuluj"); Wait(copy); if(System.IO.File.Exists(Path.Combine(local,"file.link")) || pane.TransferSnapshots.Count!=0)throw new Exception("Cancelled link copy created a job/file");
                copy=Call(pane,"CopyLinkContentsAsync"); Until(()=>main.OwnedWindows.Count>0);
                // Changing the alias after the approval preview must not redirect the queued source.
                Wait(connection.DeleteAsync(Remote("file.link"),default)); Wait(connection.CreateSymbolicLinkAsync(remote+"/directory/inside.txt",remote+"/file.link",default));
                Click(main.OwnedWindows.Single(),"Potwierdź"); Wait(copy);
                if(pane.TransferSnapshots[0].State!=TransferState.Completed || !pane.TransferSnapshots[0].Paths.Contains(remote+"/original.txt") || new FileInfo(Path.Combine(local,"file.link")).LinkTarget!=null || System.IO.File.ReadAllText(Path.Combine(local,"file.link"))!=System.IO.File.ReadAllText(source))throw new Exception("Approved link copy was redirected or created a link object");
                Refresh(); Select("directory.link"); copy=Call(pane,"CopyLinkContentsAsync"); Until(()=>main.OwnedWindows.Count>0); Click(main.OwnedWindows.Single(),"Potwierdź"); Wait(copy);
                if(pane.TransferSnapshots[0].State!=TransferState.CompletedWithSkips || !System.IO.File.Exists(Path.Combine(local,"directory.link","inside.txt")) || Directory.Exists(Path.Combine(local,"directory.link","cycle.link")))throw new Exception("Nested directory link was followed or not reported as skipped");

                Refresh(); Select("original.txt"); var create=Call(pane,"CreateRemoteLinkAsync"); Until(()=>main.OwnedWindows.Count>0); dialog=main.OwnedWindows.Single();
                dialog.GetVisualDescendants().OfType<TextBox>().Single().Text="skrót 'λ.link"; Click(dialog,"OK");
                Wait(create); if(!Remote("skrót 'λ.link").IsSymbolicLink)throw new Exception("UI did not create literal Unicode/quote link name");

                if(!OperatingSystem.IsWindows())LocalLinks();
                window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"sftp-links-{language}.png"));
            }
            finally
            {
                async Task Remove(string directory)
                { foreach(var entry in await connection.ListAsync(directory,default)) { if(entry.IsDirectory && !entry.IsSymbolicLink)await Remove(entry.FullPath); await connection.DeleteAsync(entry,default); } }
                Wait(Remove(remote)); Wait(connection.DeleteAsync(new("fixture",remote,true,false,0,DateTime.Now,""),default)); pane.Dispose(); window.Close(); DesignSystem.ApplyPreferences(preferences);
            }

            void LocalLinks()
            {
                var link=Path.Combine(local,"upload.link"); System.IO.File.CreateSymbolicLink(link,"original żółć.txt");
                var uploadDirectory=Path.Combine(local,"upload-directory"); Directory.CreateDirectory(uploadDirectory); System.IO.File.Copy(source,Path.Combine(uploadDirectory,"inside.txt")); Directory.CreateSymbolicLink(Path.Combine(uploadDirectory,"cycle.link"),".");
                var directoryLink=Path.Combine(local,"directory-upload.link"); Directory.CreateSymbolicLink(directoryLink,"upload-directory");
                System.IO.File.CreateSymbolicLink(Path.Combine(local,"broken-local.link"),"missing-local");
                var target=LocalTransferSource.Read(link,true); if(target.FullPath!=source || target.IsSymbolicLink)throw new Exception("Local link resolution failed");
                Fails(connection.DownloadAsync(remote+"/original.txt",link,true,progress,default));
                if(new FileInfo(link).LinkTarget==null || System.IO.File.ReadAllText(source)!="Original content żółć λ")throw new Exception("Local target protection replaced a link or changed its original contents");
                Fails(Task.Run(()=>LocalTransferSource.Read(Path.Combine(local,"broken-local.link"),true)));
                Refresh(); var list=Field<ListBox>(pane,"localList"); list.SelectedItem=list.Items.OfType<FileEntry>().Single(e=>e.Name=="upload.link"); Field<ComboBox>(pane,"fileTarget").SelectedIndex=0;
                var upload=Call(pane,"CopyLinkContentsAsync"); Until(()=>main.OwnedWindows.Count>0); Click(main.OwnedWindows.Single(),"Potwierdź"); Wait(upload);
                var output=Path.Combine(local,"round-trip.txt"); Wait(connection.DownloadAsync(remote+"/upload.link",output,false,progress,default)); if(System.IO.File.ReadAllText(output)!=System.IO.File.ReadAllText(source))throw new Exception("Local symlink content upload differs");
                var localDirectory=LocalTransferSource.Read(directoryLink,true); if(!localDirectory.IsDirectory || localDirectory.IsSymbolicLink)throw new Exception("Directory link did not resolve as a regular directory");
                Refresh(); list.SelectedItem=list.Items.OfType<FileEntry>().Single(e=>e.Name=="directory-upload.link"); Field<ComboBox>(pane,"fileTarget").SelectedIndex=0;
                upload=Call(pane,"CopyLinkContentsAsync"); Until(()=>main.OwnedWindows.Count>0); Click(main.OwnedWindows.Single(),"Potwierdź"); Wait(upload);
                if(pane.TransferSnapshots[0].State!=TransferState.CompletedWithSkips || !Result(connection.ExistsAsync(remote+"/directory-upload.link/inside.txt",default)) || Result(connection.ExistsAsync(remote+"/directory-upload.link/cycle.link",default)))throw new Exception("Local directory link upload followed a nested cycle");
            }
        }
        AppText.SetLanguage("pl"); DesignSystem.Apply("dark");
        Console.WriteLine("PASS SFTP links PL/EN: real OpenSSH file/directory/relative/dangling/cyclic links, navigation/details, explicit resolved-source preview/cancel, alias changed after approval, regular-content copy and nested-link skip; remote creation including Unicode/quotes; destination-link protection and original bytes; no automatic traversal or elevation"+(OperatingSystem.IsWindows()?"; local link upload requires a Linux acceptance run":"; Linux local file/directory link resolution/upload and nested-cycle skip"));
    }
}
