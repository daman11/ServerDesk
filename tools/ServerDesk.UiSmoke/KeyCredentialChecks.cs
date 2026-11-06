using System.Reflection;
using System.Security.Cryptography;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class KeyCredentialChecks
{
    private sealed class Vault:ICredentialStore
    {
        internal int Reads;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Reads++; return Task.FromResult<string?>(null); }
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default)=>throw new Exception("Unexpected credential write");
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>throw new Exception("Unexpected credential delete");
    }
    private static Task<string?> Secret(MainWindow main,ServerProfile profile,bool force=false)=>(Task<string?>)typeof(MainWindow).GetMethod("SessionSecretCoreAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,[profile,force])!;
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"keys-"+language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(ApplicationSettings.Defaults with { Preferences=new() { Language=language,CheckUpdatesOnStartup=false,Bitwarden=new(UnlockOnStartup:false) } }));
            var vault=new Vault(); var main=new MainWindow(new(repo),repo,vault,new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            using var rsa=RSA.Create(2048); var key=Path.Combine(paths.Data,"synthetic-key"); File.WriteAllText(key,rsa.ExportRSAPrivateKeyPem());
            var profile=new ServerProfile { Name="Synthetic private key",Host="fixture.invalid",Username="fixture",Authentication=Authentication.PrivateKey,PrivateKeyPath=key };
            foreach(var force in new[]{false,true})
            {
                var request=Secret(main,profile,force); RedesignChecks.Wait(request);
                if(request.Result!="" || main.OwnedWindows.Any() || vault.Reads!=0)throw new Exception("Unencrypted key requested a password, read credentials or opened the vault");
            }
            File.WriteAllText(key,rsa.ExportEncryptedPkcs8PrivateKeyPem("synthetic-fixture-passphrase",new(PbeEncryptionAlgorithm.Aes256Cbc,HashAlgorithmName.SHA256,1000)));
            var encrypted=Secret(main,profile); SshAcceptanceChecks.Until(()=>main.OwnedWindows.Any(),"Encrypted key did not request a passphrase");
            var dialog=main.OwnedWindows.Single(); var input=dialog.GetVisualDescendants().OfType<TextBox>().Single(c=>c.Name=="CredentialPassword");
            if(input.PasswordChar=='\0')throw new Exception("Key passphrase input is unmasked");
            dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,"key-passphrase-"+language+".png"));
            input.Text="synthetic-fixture-passphrase"; dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag is "Połącz").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Wait(encrypted);
            if(encrypted.Result!="synthetic-fixture-passphrase" || vault.Reads!=1)throw new Exception("Encrypted key did not retain its normal one-time passphrase flow");
            File.WriteAllText(key,"not a key"); var malformed=Secret(main,profile); SshAcceptanceChecks.Until(()=>malformed.IsCompleted,"Malformed key preflight did not finish");
            try { malformed.GetAwaiter().GetResult(); throw new Exception("Malformed key was accepted"); }
            catch(SshPrivateKeyException ex) when(ex.Reason==SshPrivateKeyFailure.InvalidOrPassphrase) { }
            if(main.OwnedWindows.Any() || vault.Reads!=1 || main.OpenSessions.Count!=0)throw new Exception("Malformed key opened a credential dialog or session");
            main.Close(); SshAcceptanceChecks.Until(()=>!main.IsVisible,"Key fixture did not close");
        }
        AppText.SetLanguage("pl"); Console.WriteLine("PASS key prompts PL/EN: unencrypted initial/forced request bypasses dialogs and credential reads, encrypted key requests masked passphrase, malformed key reports a local error; no network");
    }
}
