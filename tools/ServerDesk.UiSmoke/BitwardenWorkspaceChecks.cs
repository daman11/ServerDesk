using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class BitwardenWorkspaceChecks
{
    private const string Master="workspace-synthetic-master",Secret="workspace-synthetic-password",Token="WORKSPACE_SESSION_CANARY_abcdefghijklmnop==";
    private static readonly Guid Account=Guid.Parse("9f81d46f-9143-45dd-9122-9df01d6c2b25"),Item=Guid.Parse("c69ed90d-3c26-46c4-b546-1301fe76cb33");
    private sealed class Vault:ICredentialStore
    {
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>throw new Exception("Unexpected core credential read");
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default)=>throw new Exception("Unexpected core credential write");
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>throw new Exception("Unexpected core credential delete");
    }
    private sealed class Runner:IBitwardenAccountCommandRunner,IBitwardenProfileSnapshotRunner
    {
        public bool OwnsProfile=>true;
        public string Server=>"https://workspace.fixture.invalid";
        internal readonly List<string> Calls=[];
        internal readonly List<byte[]> Buffers=[];
        internal bool Block;
        public Task<string?> ProfileSnapshotAsync(string session,Guid accountId,CancellationToken ct)
        { ct.ThrowIfCancellationRequested(); return Task.FromResult<string?>("unchanged-synthetic-profile"); }
        public async Task<byte[]> RunAsync(IReadOnlyList<string> args,string? session,string? password,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested(); Calls.Add(args[0]);
            if(args.Any(a=>a.Contains(Master)||a.Contains(Secret)||a.Contains(Token)))throw new Exception("Secret in CLI arguments");
            if(Block)await Task.Delay(Timeout.Infinite,ct);
            var text=args[0] switch {
                "status"=>JsonSerializer.Serialize(new { status=session==null?"locked":"unlocked",userId=Account,serverUrl=Server }),
                "unlock"=>password==Master?Token:throw new BitwardenException(BitwardenFailure.Locked),
                "list"=>"["+Entry()+"]",
                "get"=>Entry(),
                "sync"=>"synced",
                _=>throw new Exception("Unexpected fixture operation") };
            var bytes=Encoding.UTF8.GetBytes(text); Buffers.Add(bytes); return bytes;
        }
        public Task<byte[]> LoginAsync(BitwardenLogin login,Func<BitwardenChallenge,CancellationToken,Task<string>> challenge,CancellationToken ct)=>throw new Exception("Existing account requested a full sign-in");
        private static string Entry()=>JsonSerializer.Serialize(new { id=Item,type=1,name="Workspace fixture",login=new { username="fixture",password=Secret } });
    }
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Control root,string name)=>Named<Button>(root,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Cancel(Window picker)=>picker.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag is "Anuluj" or "Zamknij" or "Pomiń").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Until(Func<bool> done,string message)=>SshAcceptanceChecks.Until(done,message);
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    private static Window Picker(MainWindow main)
    { Until(()=>main.OwnedWindows.Any(w=>w.Name=="BitwardenPicker"),"Vault dialog did not open"); return main.OwnedWindows.Single(); }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,"workspace-"+language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
            var options=new BitwardenCliOptions(Server:"https://workspace.fixture.invalid",Email:"fixture@example.invalid");
            var prefs=new ApplicationPreferences { Language=language,CheckUpdatesOnStartup=false,Bitwarden=options };
            Wait(repo.SaveApplicationSettingsAsync(repo.ApplicationSettingsAsync().Result with { Preferences=prefs }));
            var runner=new Runner(); var clients=new List<BitwardenCli>();
            MainWindow Open()=>new(new(repo),repo,new Vault(),new(repo),new ExternalRdpClient()) { BitwardenFactory=_=> { var client=new BitwardenCli(runner); clients.Add(client); return client; } };
            var main=Open(); main.Show(); var startup=Picker(main);
            Until(()=>Named<Button>(startup,"BitwardenUnlock").IsEnabled,"Startup unlock did not become ready");
            if(runner.Calls.Count!=1 || runner.Calls[0]!="status")throw new Exception("Prepared startup performed redundant CLI status commands");
            startup.CaptureRenderedFrame()!.Save(Path.Combine(root,"vault-startup-"+language+".png"));
            Named<TextBox>(startup,"BitwardenMasterPassword").Text=Master; Click(startup,"BitwardenUnlock"); Until(()=>!startup.IsVisible,"Successful startup unlock did not close automatically");
            if(clients.Count!=1 || !clients[0].IsUnlocked || runner.Calls.Count(c=>c=="unlock")!=1)throw new Exception("Startup did not retain one workspace session");
            var profile=new ServerProfile { Name="Workspace fixture",Host="fixture.invalid",Username="fixture" };
            var bind=main.ChooseBitwardenAsync(main,profile,null,true); var picker=Picker(main);
            Until(()=>Named<Button>(picker,"BitwardenUse").IsEnabled,"Unlocked picker did not search");
            if(Named<Grid>(picker,"BitwardenUnlockRow").IsVisible)throw new Exception("Opening a picker requested the master password again");
            Click(picker,"BitwardenUse"); Wait(bind); using var binding=bind.Result!;
            if(binding.Password!=null || runner.Calls.Contains("get"))throw new Exception("Binding fetched or cached a vault password");
            var before=runner.Calls.Count;
            var obtain=main.ChooseBitwardenAsync(main,profile,binding.Reference); picker=Picker(main); Until(()=>Named<Button>(picker,"BitwardenUse").IsEnabled,"Bound picker did not reuse unlock");
            if(runner.Calls.Count!=before)throw new Exception("Reopening an unlocked bound picker launched CLI work");
            picker.CaptureRenderedFrame()!.Save(Path.Combine(root,"vault-workspace-ready-"+language+".png"));
            Click(picker,"BitwardenUse"); Wait(obtain); using var chosen=obtain.Result!;
            if(chosen.Password!=Secret || !runner.Calls.Skip(before).SequenceEqual(new[]{"get"}))throw new Exception("Unchanged owned profile did not use one exact-ID fetch");
            var reopen=main.ChooseBitwardenAsync(main,profile,null,true); picker=Picker(main); Until(()=>Named<Button>(picker,"BitwardenUse").IsEnabled,"Search picker did not reopen");
            if(runner.Calls.Count(c=>c=="list")!=1)throw new Exception("Reopening repeated a recent metadata search"); Cancel(picker); Wait(reopen);
            var account=main.ConfigureBitwardenAccountAsync(main); picker=Picker(main); Until(()=>Named<Button>(picker,"BitwardenLock").IsVisible,"Manual lock missing");
            Click(picker,"BitwardenLock"); Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled,"Manual lock did not restore protected input");
            if(clients[0].IsUnlocked)throw new Exception("Manual lock retained the workspace key");
            Named<TextBox>(picker,"BitwardenMasterPassword").Text=Master; Click(picker,"BitwardenUnlock"); Until(()=>Named<Button>(picker,"BitwardenLock").IsVisible && !Named<ProgressBar>(picker,"BitwardenProgress").IsVisible,"Manual unlock failed"); Cancel(picker); Wait(account);
            Wait(main.ApplyPluginsAsync(prefs with { Bitwarden=options with { UnlockOnStartup=false } }));
            if(!clients[0].IsUnlocked)throw new Exception("Changing only startup preference discarded the current session");
            var settingsTask=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(5); var settings=main.OwnedWindows.Single(); Named<TabControl>(settings,"SettingsCategories").SelectedIndex=6; RedesignChecks.Frames(3);
            Named<CheckBox>(settings,"BitwardenUnlockOnStartup").IsChecked=false;
            settings.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag is "Anuluj").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(settingsTask);
            if(!repo.PreferencesAsync().Result.Bitwarden.UnlockOnStartup || !clients[0].IsUnlocked)throw new Exception("Cancelled startup preference changed persistence or unlock state");
            settingsTask=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(5); settings=main.OwnedWindows.Single(); Named<TabControl>(settings,"SettingsCategories").SelectedIndex=6; RedesignChecks.Frames(3);
            Named<CheckBox>(settings,"BitwardenUnlockOnStartup").IsChecked=false;
            settings.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag is "Zapisz").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(settingsTask);
            if(repo.PreferencesAsync().Result.Bitwarden.UnlockOnStartup || !clients[0].IsUnlocked)throw new Exception("Saved startup preference was lost or unnecessarily locked the vault");
            Wait(main.ApplyPluginsAsync(prefs with { Bitwarden=options with { Email="other@example.invalid" } }));
            if(clients[0].IsUnlocked)throw new Exception("Changing accounts retained the old workspace session");
            var first=main.ChooseBitwardenAsync(main,profile,null); picker=Picker(main); Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled,"Changed account did not require a fresh unlock");
            var second=main.ChooseBitwardenAsync(main,profile,null); RedesignChecks.Frames(2);
            if(main.OwnedWindows.Count!=1 || clients.Count!=2)throw new Exception("Concurrent requests created multiple vault dialogs/clients");
            Wait(main.ApplyPluginsAsync(prefs with { DisabledPlugins=new([PluginCatalog.Bitwarden]) })); Wait(first); Wait(second);
            if(main.OwnedWindows.Any() || clients.Any(c=>c.IsUnlocked) || main.OpenSessions.Count!=0)throw new Exception("Plugin disable failed to drain its work independently");
            Wait(main.ApplyPluginsAsync(prefs)); var pending=main.ConfigureBitwardenAccountAsync(main); picker=Picker(main); Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled,"Reenabled account not ready");
            runner.Block=true; Named<TextBox>(picker,"BitwardenMasterPassword").Text=Master; Click(picker,"BitwardenUnlock");
            Wait(main.ApplyPluginsAsync(prefs with { DisabledPlugins=new([PluginCatalog.Bitwarden]) })); Wait(pending); runner.Block=false;
            if(clients.Any(c=>c.IsUnlocked))throw new Exception("Cancelled unlock retained a session");
            main.Close(); Until(()=>!main.IsVisible,"Workspace test did not close");
            if(runner.Buffers.Any(bytes=>bytes.Any(b=>b!=0)))throw new Exception("Workspace retained CLI output buffers");
            Wait(repo.SaveApplicationSettingsAsync(repo.ApplicationSettingsAsync().Result with { Preferences=prefs }));
            // A new process/window starts locked; Skip does not schedule another prompt.
            main=Open(); main.Show(); startup=Picker(main); Until(()=>Named<Button>(startup,"BitwardenUnlock").IsEnabled,"Restart did not request unlock"); Cancel(startup); Wait(main.UnlockBitwardenAtStartupAsync());
            if(main.OwnedWindows.Any())throw new Exception("Skipped startup unlock prompted a second time"); main.Close(); Until(()=>!main.IsVisible,"Skipped startup window did not close");
            Wait(repo.SaveApplicationSettingsAsync(repo.ApplicationSettingsAsync().Result with { Preferences=prefs with { Bitwarden=options with { UnlockOnStartup=false } } }));
            var count=clients.Count; main=Open(); main.Show(); RedesignChecks.Frames(12);
            if(main.OwnedWindows.Any() || clients.Count!=count)throw new Exception("Startup opt-out launched the vault");
            var finalUnlock=main.ConfigureBitwardenAccountAsync(main); picker=Picker(main); Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled,"Final account did not become ready");
            Named<TextBox>(picker,"BitwardenMasterPassword").Text=Master; Click(picker,"BitwardenUnlock"); Until(()=>clients.Last().IsUnlocked && !Named<ProgressBar>(picker,"BitwardenProgress").IsVisible,"Final workspace unlock failed"); Cancel(picker); Wait(finalUnlock);
            main.Close(); Until(()=>!main.IsVisible,"Opt-out window did not close"); if(clients.Any(c=>c.IsUnlocked))throw new Exception("Closing the application retained its workspace unlock");
            // Default-US accounts may have been configured without saving an email.
            Wait(repo.SaveApplicationSettingsAsync(repo.ApplicationSettingsAsync().Result with { Preferences=prefs with { Bitwarden=new() } }));
            Directory.CreateDirectory(BitwardenRuntime.ProfileDirectory(paths.Data,new())); main=Open(); main.Show(); startup=Picker(main); Until(()=>Named<Button>(startup,"BitwardenUnlock").IsEnabled,"Existing default profile did not request startup unlock"); Cancel(startup); main.Close(); Until(()=>!main.IsVisible,"Default profile startup did not close");
            Console.WriteLine("PASS workspace vault "+language+": startup/skip/opt-out, one unlock across pickers, zero-command bound reopen, metadata-only search reuse, exact-ID account guards, manual lock, account change, serialization, cancellation, disable/re-enable and no core credential access");
        }
        AppText.SetLanguage("pl");
    }
}
