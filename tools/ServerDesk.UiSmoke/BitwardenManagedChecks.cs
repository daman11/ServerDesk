using System.Text;
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class BitwardenManagedChecks
{
    private const string Master="managed-fixture-master",Otp="123456";
    private sealed class Vault:ICredentialStore
    {
        internal int Calls;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected credential access"); }
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected credential access"); }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Calls++; throw new Exception("Unexpected credential access"); }
    }
    private sealed class Runner(string server):IBitwardenAccountCommandRunner
    {
        public bool OwnsProfile=>true;
        public string Server=>server;
        internal bool LoggedIn;
        internal int Syncs,Logouts,Logins,Cancelled;
        internal readonly List<byte[]> Buffers=[];
        private byte[] Bytes(string text) { var bytes=Encoding.UTF8.GetBytes(text); Buffers.Add(bytes); return bytes; }
        public Task<byte[]> RunAsync(IReadOnlyList<string> args,string? session,string? password,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); AssertSafe(args);
            var result=args[0] switch {
                "status"=>JsonSerializer.Serialize(new { status=LoggedIn?(session==null?"locked":"unlocked"):"unauthenticated",userId="0752f423-c9ea-4303-a5df-42e3ec16fb48",serverUrl=Server }),
                "config"=>Server,
                "unlock"=>password==Master?"MANAGED_SESSION_CANARY_123456789==":throw new BitwardenException(BitwardenFailure.Locked),
                "sync"=>Sync(),
                "logout"=>Logout(),
                "list"=>"[]",
                _=>throw new Exception("Unexpected managed operation: "+args[0]) };
            return Task.FromResult(Bytes(result));
        }
        private string Sync() { Syncs++; return "synced"; }
        private string Logout() { Logouts++; LoggedIn=false; return "logged out"; }
        private static void AssertSafe(IReadOnlyList<string> args) { if(args.Any(a=>a.Contains(Master)||a.Contains(Otp)||a.Contains("MANAGED_SESSION")))throw new Exception("Secret in arguments"); }
        public async Task<byte[]> LoginAsync(BitwardenLogin login,Func<BitwardenChallenge,CancellationToken,Task<string>> challenge,CancellationToken ct)
        {
            try { Logins++; if(login.MasterPassword!=Master || await challenge(BitwardenChallenge.AuthenticatorCode,ct)!=Otp)throw new BitwardenException(BitwardenFailure.CommandFailed); ct.ThrowIfCancellationRequested(); LoggedIn=true; return Bytes("MANAGED_SESSION_CANARY_123456789=="); }
            catch(OperationCanceledException) { Cancelled++; throw; }
        }
    }
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Control root,string name)=>Named<Button>(root,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Action(Control root,string tag)=>root.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    internal static void Run(string root)
    {
        BitwardenWorkspaceChecks.Run(root);
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var repo=new ProfileRepository(new(Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N")))); Wait(repo.InitializeAsync());
            Wait(repo.SaveApplicationSettingsAsync((repo.ApplicationSettingsAsync().Result) with { Preferences=new() { Language=language } }));
            var vault=new Vault(); var calls=0; var runner=new Runner("https://vault.fixture.invalid");
            var main=new MainWindow(new(repo),repo,vault,new(repo),new ExternalRdpClient()) { BitwardenFactory=options=> { calls++; if(options.Server!=runner.Server || options.Executable.Length>0)throw new Exception("Wrong managed account configuration"); return new(runner); } };
            main.Show(); RedesignChecks.Frames(10);
            var edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(6); var settings=main.OwnedWindows.Single(); Named<TabControl>(settings,"SettingsCategories").SelectedIndex=6; RedesignChecks.Frames(4);
            if(Named<Expander>(settings,"BitwardenAdvanced").IsExpanded)throw new Exception("Fresh account requires an executable path");
            Named<ComboBox>(settings,"BitwardenService").SelectedIndex=2; Named<TextBox>(settings,"BitwardenServerAddress").Text=runner.Server; Named<TextBox>(settings,"BitwardenEmail").Text="fixture@example.invalid";
            Action(settings,"Anuluj"); Wait(edit);
            if(calls!=0 || repo.ApplicationSettingsAsync().Result.Preferences.Bitwarden.Server.Length!=0)throw new Exception("Cancelled draft changed vault configuration");
            edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(6); settings=main.OwnedWindows.Single(); Named<TabControl>(settings,"SettingsCategories").SelectedIndex=6; RedesignChecks.Frames(4);
            Named<ComboBox>(settings,"BitwardenService").SelectedIndex=2; Named<TextBox>(settings,"BitwardenServerAddress").Text="";
            Click(settings,"BitwardenSaveConnect"); RedesignChecks.Frames(4);
            if(calls!=0 || !settings.IsVisible || repo.ApplicationSettingsAsync().Result.Preferences.Bitwarden.Server.Length!=0)throw new Exception("Blank custom server silently selected a cloud account");
            Named<TextBox>(settings,"BitwardenServerAddress").Text="http://vault.fixture.invalid";
            Click(settings,"BitwardenSaveConnect"); RedesignChecks.Frames(4);
            if(calls!=0 || !settings.IsVisible)throw new Exception("Insecure server started login");
            Named<ComboBox>(settings,"BitwardenService").SelectedIndex=2; Named<TextBox>(settings,"BitwardenServerAddress").Text=runner.Server; Named<TextBox>(settings,"BitwardenEmail").Text="fixture@example.invalid";
            Named<TextBox>(settings,"SettingsSearch").Text="Bitwarden"; RedesignChecks.Frames(4);
            Named<Button>(settings,"BitwardenSaveConnect").BringIntoView(); RedesignChecks.Frames(3);
            settings.CaptureRenderedFrame()!.Save(Path.Combine(root,"managed-settings-"+language+".png"));
            Click(settings,"BitwardenSaveConnect");
            SshAcceptanceChecks.Until(()=>main.OwnedWindows.Any(w=>w.Name=="BitwardenPicker"),"Save and connect did not open the account dialog");
            var picker=main.OwnedWindows.Single(); SshAcceptanceChecks.Until(()=>Named<StackPanel>(picker,"BitwardenLoginFields").IsVisible,"Fresh account did not offer GUI login");
            if(calls!=1 || Named<Grid>(picker,"BitwardenUnlockRow").IsVisible || Named<TextBox>(picker,"BitwardenLoginEmail").Text!="fixture@example.invalid" || Named<TextBox>(picker,"BitwardenLoginPassword").PasswordChar!='●')throw new Exception("GUI login fields are incorrect");
            foreach(var width in new[]{720,430}) { picker.Width=width; RedesignChecks.Frames(4); picker.CaptureRenderedFrame()!.Save(Path.Combine(root,$"managed-login-{language}-{width}.png")); }
            Named<TextBox>(picker,"BitwardenLoginPassword").Text="rejected-fixture"; Click(picker,"BitwardenLogin");
            SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenLogin").IsEnabled && Named<TextBlock>(picker,"BitwardenError").IsVisible,"Failed login did not recover");
            if(!Named<TextBox>(picker,"BitwardenLoginPassword").IsKeyboardFocusWithin || Named<TextBox>(picker,"BitwardenLoginPassword").Text!="")throw new Exception("Failed sign-in retained its password or focused a hidden field");
            Named<TextBox>(picker,"BitwardenLoginPassword").Text=Master; Click(picker,"BitwardenLogin");
            SshAcceptanceChecks.Until(()=>picker.OwnedWindows.Count==1,"2FA dialog did not open"); var otp=picker.OwnedWindows.Single();
            var code=otp.GetVisualDescendants().OfType<TextBox>().Single(); if(code.PasswordChar!='●')throw new Exception("2FA code is visible");
            code.Text=Otp; Action(otp,"OK");
            SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenSync").IsVisible && Named<Button>(picker,"BitwardenSync").IsEnabled,"GUI login did not unlock the account");
            if(code.Text!="" || Named<TextBox>(picker,"BitwardenLoginPassword").Text!="" || runner.Logins!=2 || vault.Calls!=0 || main.OpenSessions.Count!=0)throw new Exception("Account login retained inputs or touched core credentials");
            Click(picker,"BitwardenSync"); SshAcceptanceChecks.Until(()=>runner.Syncs==1 && Named<Button>(picker,"BitwardenSync").IsEnabled,"GUI sync failed");
            picker.CaptureRenderedFrame()!.Save(Path.Combine(root,"managed-ready-"+language+".png"));
            Click(picker,"BitwardenLogout"); SshAcceptanceChecks.Until(()=>picker.OwnedWindows.Count==1,"Logout confirmation missing"); Action(picker.OwnedWindows.Single(),"Potwierdź");
            SshAcceptanceChecks.Until(()=>runner.Logouts==1 && Named<StackPanel>(picker,"BitwardenLoginFields").IsVisible,"GUI logout did not restore sign-in");
            Action(picker,"Zamknij"); Wait(edit);
            var pending=main.ConfigureBitwardenAccountAsync(main); SshAcceptanceChecks.Until(()=>main.OwnedWindows.Any(),"Account did not reopen"); picker=main.OwnedWindows.Single();
            SshAcceptanceChecks.Until(()=>Named<StackPanel>(picker,"BitwardenLoginFields").IsVisible,"Account did not return to sign-in");
            Named<TextBox>(picker,"BitwardenLoginPassword").Text=Master; Click(picker,"BitwardenLogin"); SshAcceptanceChecks.Until(()=>picker.OwnedWindows.Count==1,"Pending code missing");
            Wait(main.ApplyPluginsAsync(main.CurrentPreferences with { DisabledPlugins=new([PluginCatalog.Bitwarden]) })); Wait(pending);
            if(main.OwnedWindows.Any() || vault.Calls!=0 || main.OpenSessions.Count!=0)throw new Exception("Plugin cancellation retained an account dialog or changed sessions");
            if(runner.Buffers.Any(b=>b.Any(v=>v!=0)))throw new Exception("CLI response buffers were retained");
            main.Close(); RedesignChecks.Frames(5);
            Console.WriteLine("PASS managed vault "+language+": GUI server/email draft, save-before-connect, no executable picker, masked login/2FA, input clearing, sync/logout, compact layouts, plugin cancellation and no core credential/session access");
        }
        AppText.SetLanguage("pl");
    }
}
