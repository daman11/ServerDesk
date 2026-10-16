using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class CredentialSaveChecks
{
    private sealed class Vault(string root):ICredentialStore
    {
        private readonly SecretServiceCredentialStore missing=new(Path.Combine(root,"missing-secret-tool-"+Guid.NewGuid().ToString("N")));
        internal bool Fail=true,ReadOnly;
        internal int Saves,Deletes;
        internal Guid LastId;
        internal string? Saved;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult(Saved);
        public async Task SaveAsync(Guid id,string value,CancellationToken ct=default)
        {
            Saves++; LastId=id;
            if(ReadOnly)throw new UnauthorizedAccessException("Disposable locked store fixture");
            if(Fail)await missing.SaveAsync(id,value,ct);
            else Saved=value;
        }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Deletes++; return Task.CompletedTask; }
    }
    private static void Click(Window dialog,string label)
    { dialog.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==label).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(3); }
    private static Window Input(MainWindow main,string value,bool remember)
    {
        RedesignChecks.Frames(); var dialog=main.OwnedWindows.Single(); var box=dialog.GetVisualDescendants().OfType<TextBox>().Single();
        if(box.PasswordChar=='\0')throw new Exception("Password input is not masked"); box.Text=value;
        dialog.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked=remember; return dialog;
    }
    private static Window Warning(MainWindow main,string secret)
    {
        RedesignChecks.Frames(); var dialog=main.OwnedWindows.Single();
        if(dialog.Name!="CredentialSaveWarning" || dialog.Title!=AppText.T("Nie potwierdzono zapisu hasła"))throw new Exception("Save failure was shown as a generic application error");
        var labels=dialog.GetVisualDescendants().OfType<TextBlock>().ToArray();
        if(!labels.Any(t=>t.Name=="CredentialSaveNotice") || labels.Any(t=>t.Text?.Contains(secret)==true))throw new Exception("Credential notice is absent or exposes the supplied secret");
        if(!dialog.GetVisualDescendants().OfType<Button>().Any(b=>b.Tag as string=="Połącz bez zapisywania"))throw new Exception("No explicit use-without-saving action");
        return dialog;
    }
    internal static void Run(string root)
    {
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"data-"+language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
            var vault=new Vault(root); var main=new MainWindow(new MainViewModel(repo),repo,vault,new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            var profile=new ServerProfile { Name="Disposable credential fixture",Host="fixture.invalid",Username="fixture" };
            Task<string?> Prompt()=>(Task<string?>)typeof(MainWindow).GetMethod("SessionSecretAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,[profile])!;
            try
            {
                var manual=Prompt(); Click(Input(main,"manual-only-fixture",false),"Połącz"); RedesignChecks.Wait(manual);
                if(manual.Result!="manual-only-fixture" || vault.Saves!=0 || main.OwnedWindows.Count!=0)throw new Exception("Manual entry attempted persistence");
                foreach(var readOnly in new[]{false,true})
                {
                    vault.ReadOnly=readOnly; var attempts=vault.Saves; const string supplied="save-failure-disposable-fixture";
                    var cancel=Prompt(); Click(Input(main,supplied,true),"Połącz"); Click(Warning(main,supplied),"Anuluj"); RedesignChecks.Wait(cancel);
                    if(cancel.Result!=null || main.OpenSessions.Any() || vault.Saves!=attempts+1 || vault.Deletes!=0 || vault.Saved!=null)throw new Exception("Cancel retried persistence, removed data or returned a secret");
                    var once=Prompt(); Click(Input(main,supplied,true),"Połącz"); var warning=Warning(main,supplied);
                    foreach(var theme in new[]{"graphite","light"})
                    {
                        DesignSystem.Apply(theme); DesignSystem.ApplyPreferences(preferences with { InterfaceFontSize=16 }); warning.SetRenderScaling(2); RedesignChecks.Frames(3);
                        foreach(var button in warning.GetVisualDescendants().OfType<Button>())
                        { var point=button.TranslatePoint(default,warning)!.Value; if(point.X<0 || point.Y<0 || point.X+button.Bounds.Width>warning.ClientSize.Width+.5 || point.Y+button.Bounds.Height>warning.ClientSize.Height+.5)throw new Exception("Credential actions escape warning window"); }
                        if(!readOnly)warning.CaptureRenderedFrame()!.Save(Path.Combine(root,$"credential-save-{language}-{theme}.png"));
                    }
                    Click(warning,"Połącz bez zapisywania"); RedesignChecks.Wait(once);
                    if(once.Result!=supplied || vault.Saves!=attempts+2 || vault.Deletes!=0 || vault.Saved!=null || vault.LastId!=CredentialReference.ForProfile(profile))throw new Exception("Use once retried persistence or changed credential identity");
                }
                vault.ReadOnly=false; vault.Fail=false;
                var saved=Prompt(); Click(Input(main,"saved-fixture",true),"Połącz"); RedesignChecks.Wait(saved);
                if(saved.Result!="saved-fixture" || vault.Saved!="saved-fixture" || main.OwnedWindows.Count!=0)throw new Exception("Successful save showed a warning or lost its value");
                var next=Prompt(); RedesignChecks.Wait(next); if(next.Result!="saved-fixture" || main.OwnedWindows.Count!=0)throw new Exception("Stored value was not reused");
                using var cancelled=new CancellationTokenSource(); var pending=Dialogs.ContinueWithoutSavingCredentials(main,cancelled.Token); RedesignChecks.Frames(); cancelled.Cancel(); RedesignChecks.Wait(pending);
                if(pending.Result || main.OwnedWindows.Count!=0)throw new Exception("Cancellation accepted use once or retained the dialog");
                Console.WriteLine("PASS "+language+": missing real helper/read-only store -> explicit cancel/use-once; one save attempt, no delete/retry/plaintext fallback; masked input, successful save/read, cancellation, Graphite/Light font16 at 200% rendering");
            }
            finally { main.Close(); RedesignChecks.Frames(20); }
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark");
    }
    internal static void Network(string root,string host,string key)
    {
        var paths=new AppPaths(Path.Combine(root,"network-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync());
        var vault=new Vault(root); var factory=new SshConnectionFactory(repo); var main=new MainWindow(new MainViewModel(repo),repo,vault,factory,new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20); factory.ConfirmHostKey=(_,_)=>Task.FromResult(true);
        var profile=new ServerProfile { Name="Credential save recovery",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        try
        {
            const string passphrase="serverdesk-test-passphrase";
            var work=main.ConnectAsync(profile); Click(Input(main,passphrase,true),"Połącz"); var notice=Warning(main,passphrase);
            if(main.OpenSessions.Any() || work.IsCompleted)throw new Exception("Connection started before the persistence decision");
            Click(notice,"Połącz bez zapisywania"); RedesignChecks.Wait(work); var pane=main.OpenSessions.Single();
            if(!pane.IsConnected || vault.Saves!=1 || vault.Deletes!=0 || vault.Saved!=null)throw new Exception("Use once did not connect without further persistence");
            var terminal=(TerminalControl)typeof(SessionPane).GetField("terminal",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(pane)!;
            var marker=Guid.NewGuid().ToString("N"); RedesignChecks.Wait(terminal.SendInputAsync($"printf '%s:%s\\n' 'CRED_SAVE' '{marker}'\r",default));
            var until=DateTime.UtcNow.AddSeconds(15); while(!SshAcceptanceChecks.Text(terminal).Contains("CRED_SAVE:"+marker) && DateTime.UtcNow<until)RedesignChecks.Frames(1);
            if(!SshAcceptanceChecks.Text(terminal).Contains("CRED_SAVE:"+marker))throw new Exception("Connected terminal did not execute its marker");
            main.RemovePane(pane); RedesignChecks.Wait(pane.CloseAsync());
            var cancelled=main.ConnectAsync(profile); Click(Input(main,passphrase,true),"Połącz"); Click(Warning(main,passphrase),"Anuluj"); RedesignChecks.Wait(cancelled);
            if(main.OpenSessions.Any() || vault.Saves!=2 || vault.Deletes!=0)throw new Exception("Cancel opened a session or retried the save");
            Console.WriteLine("PASS real owned OpenSSH: missing credential helper -> explicit use once -> authenticated SSH and executed output; cancel starts no session; no extra save/delete");
        }
        finally { foreach(var pane in main.OpenSessions.ToArray()) { main.RemovePane(pane); RedesignChecks.Wait(pane.CloseAsync()); } main.Close(); RedesignChecks.Frames(20); }
    }
}
