using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class BitwardenSshAcceptanceChecks
{
    private static readonly Guid Account=Guid.Parse("4386bda9-b5da-4dba-beb6-99aca51db424"),Item=Guid.Parse("f1b85c1e-d216-4f16-998e-15517dc67050");
    private sealed class Vault:ICredentialStore
    {
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>throw new Exception("Vault-key authentication read a stored password");
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default)=>throw new Exception("Vault-key authentication saved secret material");
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>throw new Exception("Vault-key authentication deleted another credential");
    }
    private sealed class Runner:IBitwardenCommandRunner
    {
        internal string Pem=""; internal int Reads;
        public Task<byte[]> RunAsync(IReadOnlyList<string> args,string? session,string? masterPassword,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); string value;
            if(args[0]=="status")value=JsonSerializer.Serialize(new { status=session==null?"locked":"unlocked",userId=Account,serverUrl="https://fixture.invalid" });
            else if(args[0]=="unlock")value="SYNTHETIC_SESSION_KEY_abcdefghijklmnop==";
            else if(args[0]=="get") { Reads++; value=JsonSerializer.Serialize(new { id=Item,type=5,sshKey=new { privateKey=Pem } }); }
            else throw new Exception("Unexpected fixture command");
            return Task.FromResult(Encoding.UTF8.GetBytes(value));
        }
    }
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Control root,string name)=>Named<Button>(root,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    private static void Unlock(MainWindow main)
    {
        SshAcceptanceChecks.Until(()=>main.OwnedWindows.Any(),"Vault-key connection did not open a picker"); var picker=main.OwnedWindows.Single();
        SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled || Named<Button>(picker,"BitwardenUse").IsEnabled,"Vault-key picker status did not complete");
        if(Named<Button>(picker,"BitwardenUnlock").IsEnabled) { Named<TextBox>(picker,"BitwardenMasterPassword").Text="fixture-master"; Click(picker,"BitwardenUnlock"); }
        SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUse").IsEnabled,"Vault-key picker did not unlock"); Click(picker,"BitwardenUse");
    }
    internal static void Run(string root,string fixtureDirectory)
    {
        var directory=Path.GetFullPath(fixtureDirectory);
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Combine(directory,"fixture.json"))); var settings=json.RootElement;
        var host=settings.GetProperty("host").GetString()!; var port=settings.GetProperty("port").GetInt32(); var expected=settings.GetProperty("fingerprint").GetString()!; var home=settings.GetProperty("remoteHome").GetString()!;
        if(host!="127.0.0.1" || port!=22224 || home!="/tmp/serverdesk-vault-ssh-test/home")throw new Exception("Use only the explicit disposable vault-key fixture");
        var paths=new AppPaths(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
        Wait(repo.TrustHostAsync(host,port,expected,default));
        var application=Result(repo.ApplicationSettingsAsync()) with { Preferences=new() { ShowMetrics=false,ShowStatusBar=false,SftpLocalDirectory=Path.GetFullPath(root) } }; Wait(repo.SaveApplicationSettingsAsync(application));
        var factory=new SshConnectionFactory(repo) { ConfirmHostKey=(challenge,_)=>Task.FromResult(challenge.Host==host && challenge.Port==port && challenge.Fingerprint==expected) };
        var runner=new Runner(); var main=new MainWindow(new(repo),repo,new Vault(),factory,new ExternalRdpClient()) { BitwardenFactory=_=>new(runner) }; main.Show(); RedesignChecks.Frames(10);
        try
        {
            foreach(var name in new[]{"rsa","ed25519"})
            {
                runner.Pem=File.ReadAllText(Path.Combine(directory,name+".key")); var reference=new BitwardenReference(Item,Account,"https://fixture.invalid","Disposable "+name,BitwardenItemKind.SshKey);
                var profile=new ServerProfile { Name="Vault "+name,Host=host,Port=port,Username="serverdesk_fixture",Authentication=Authentication.VaultSshKey,Bitwarden=reference }; Wait(repo.SaveAsync(profile));
                var connect=main.ConnectAsync(profile); Unlock(main); Wait(connect); var pane=main.GetVisualDescendants().OfType<SessionPane>().Single(p=>p.Profile.Id==profile.Id);
                if(!pane.IsConnected || !pane.HasVaultKeyMaterial)throw new Exception("Vault key failed to authenticate the SSH terminal: "+name);
                var terminal=pane.Children.OfType<TerminalControl>().Single(); Wait(terminal.SendInputAsync("printf 'VAULT_KEY_TERMINAL_OK\\n'\r",default)); SshAcceptanceChecks.Until(()=>SshAcceptanceChecks.Text(terminal).Contains("VAULT_KEY_TERMINAL_OK"),"Vault-key terminal command failed");
                Wait(pane.ShowAsync(1));
                using var material=SshKeyMaterial.Create(runner.Pem); using var sftp=new SftpSession(Result(factory.SftpAsync(profile,"",material,default)));
                var filename="vault-key-"+Guid.NewGuid().ToString("N")+".txt"; var local=Path.Combine(root,filename); File.WriteAllText(local,"SFTP_KEY_CANARY_żółć");
                var upload=(Task)typeof(SessionPane).GetMethod("UploadFilesAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(pane,new object[]{new List<FileEntry> { new(filename,Path.GetFullPath(local),false,false,new FileInfo(local).Length,DateTime.UtcNow,"rw") } })!; Wait(upload);
                var entry=Result(sftp.ListAsync(home,default)).Single(e=>e.Name==filename); var download=Path.Combine(root,"download-"+filename); Wait(sftp.DownloadAsync(entry.FullPath,download,false,new Progress<TransferProgress>(),default));
                if(File.ReadAllText(download)!="SFTP_KEY_CANARY_żółć")throw new Exception("Vault-key queued SFTP transfer did not preserve content"); Wait(sftp.DeleteAsync(entry,default));
                Wait(pane.ShowAsync(2)); SshAcceptanceChecks.Until(()=>pane.LatestSnapshot!=null,"Monitoring could not create a key-authenticated connection");
                Wait(main.ApplyPluginsAsync(application.Preferences with { DisabledPlugins=new([PluginCatalog.Bitwarden]) }));
                if(!pane.IsConnected || !pane.HasVaultKeyMaterial)throw new Exception("Disabling optional vault integration interrupted core key session"); Wait(pane.ShowAsync(1)); Wait(main.ApplyPluginsAsync(application.Preferences));
                Wait(terminal.SendInputAsync("exit\r",default)); SshAcceptanceChecks.Until(()=>!pane.IsConnected && !pane.HasVaultKeyMaterial,"Remote exit retained vault-key bytes");
                var before=runner.Reads; var reconnect=main.ReconnectAsync(pane); Unlock(main); Wait(reconnect);
                if(!pane.IsConnected || !pane.HasVaultKeyMaterial || runner.Reads!=before+1)throw new Exception("Reconnect did not re-unlock and fetch the exact vault item");
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,"vault-key-"+name+".png")); Wait(pane.CloseAsync()); if(pane.HasVaultKeyMaterial)throw new Exception("Session close retained key material");
                Console.WriteLine("PASS vault "+name+": picker → real OpenSSH terminal, SFTP browser/queued transfer/readback, monitoring, plugin disable independence, remote exit cleanup and fresh reconnect unlock");
            }
            using var wrongRsa=RSA.Create(2048); using var wrong=SshKeyMaterial.Create(wrongRsa.ExportRSAPrivateKeyPem());
            var denied=new ServerProfile { Name="Denied fixture",Host=host,Port=port,Username="serverdesk_fixture",Authentication=Authentication.VaultSshKey };
            try { using var invalid=Result(factory.SshAsync(denied,"",wrong,default)); throw new Exception("Unauthorized key was accepted"); } catch(SshLoginException ex) when(ex.Method==Authentication.VaultSshKey) { }
            var failed=new SessionPane(main,denied,factory,new ExternalRdpClient(),repo); try { Wait(failed.ConnectAsync("",default,wrong)); throw new Exception("Invalid pane key was accepted"); } catch(SshLoginException) { }
            if(failed.HasVaultKeyMaterial)throw new Exception("Failed authentication retained key bytes"); Wait(failed.CloseAsync());
            foreach(var file in Directory.GetFiles(paths.Data,"*",SearchOption.AllDirectories))
            {
                using var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
                using var reader=new StreamReader(input);
                if(reader.ReadToEnd().Contains(runner.Pem.Split('\n')[1]))throw new Exception("Private key reached ServerDesk persistent data");
            }
            Console.WriteLine("PASS unauthorized key rejection, failed-connect cleanup and no private key in ServerDesk data");
        }
        finally { main.Close(); RedesignChecks.Frames(3); foreach(var dialog in main.OwnedWindows.ToArray())dialog.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Potwierdź")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(20); }
    }
}
