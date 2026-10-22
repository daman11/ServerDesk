using System.Text;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Headless;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class BitwardenChecks
{
    private const string FixtureMarker="ServerDesk isolated Bitwarden fixture v1";
    private static readonly Guid Account=Guid.Parse("33071d36-9559-458e-b70b-d710c4325ec2");
    private static readonly Guid Item=Guid.Parse("7bf09a47-c569-4c72-9918-96d51578c8e1");
    private const string Secret="SYNTHETIC_VAULT_PASSWORD_CANARY";
    internal static bool IsFixture=>Environment.GetEnvironmentVariable("SERVERDESK_BITWARDEN_FIXTURE") is {} root && File.Exists(Path.Combine(root,"fixture.enabled")) && File.ReadAllText(Path.Combine(root,"fixture.enabled"))==FixtureMarker;
    internal static async Task Fixture(string[] args)
    {
        var root=Environment.GetEnvironmentVariable("SERVERDESK_BITWARDEN_FIXTURE")!;
        var mode=File.ReadAllText(Path.Combine(root,"mode"));
        File.AppendAllText(Path.Combine(root,"calls.jsonl"),JsonSerializer.Serialize(new { args,session=Environment.GetEnvironmentVariable("BW_SESSION")!=null,password=Environment.GetEnvironmentVariable("SERVERDESK_BW_PASSWORD")!=null,debug=Environment.GetEnvironmentVariable("BITWARDENCLI_DEBUG")!=null,node=Environment.GetEnvironmentVariable("NODE_OPTIONS")!=null })+"\n");
        if(mode=="delay") { await Task.Delay(30000); return; }
        if(mode=="overflow") { Console.Write(new string('x',9*1024*1024)); return; }
        if(mode=="failure") { Console.Error.Write(Secret); Environment.ExitCode=1; return; }
        var json=args[0] switch
        {
            "status"=>JsonSerializer.Serialize(new { status=Environment.GetEnvironmentVariable("BW_SESSION")!=null?"unlocked":"locked",userId=Account,serverUrl="https://fixture.invalid" }),
            "unlock"=>Environment.GetEnvironmentVariable("SERVERDESK_BW_PASSWORD")=="master-fixture"?"SYNTHETIC_SESSION_CANARY_abcdef==":throw new Exception("Fixture password missing"),
            "list"=>"["+Entry()+"]",
            "get"=>Entry(),
            _=>throw new Exception("Unexpected CLI mutation")
        };
        Console.Write(json);
        static string Entry()=>JsonSerializer.Serialize(new { id=Item,type=1,name="Fixture vault server",login=new { username="fixture",password=Secret } });
    }
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Control root,string name)=>Named<Button>(root,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    private sealed class Vault:ICredentialStore
    {
        internal int Reads,Writes;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Reads++; return Task.FromResult<string?>(null); }
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default) { Writes++; throw new Exception("Vault picker must not save credentials"); }
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>throw new Exception("Vault picker must not delete credentials");
    }
    internal static void Run(string root)
    {
        var fixture=Path.Combine(root,"cli-fixture"); Directory.CreateDirectory(fixture); File.WriteAllText(Path.Combine(fixture,"fixture.enabled"),FixtureMarker); File.WriteAllText(Path.Combine(fixture,"mode"),"normal");
        Environment.SetEnvironmentVariable("SERVERDESK_BITWARDEN_FIXTURE",fixture);
        try
        {
            var exe=Path.Combine(AppContext.BaseDirectory,OperatingSystem.IsWindows()?"ServerDesk.UiSmoke.exe":"ServerDesk.UiSmoke");
            var options=new BitwardenCliOptions(exe);
            using(var service=BitwardenCli.Create(options))
            {
                Environment.SetEnvironmentVariable("BW_SESSION","inherited-session-canary"); Environment.SetEnvironmentVariable("BITWARDENCLI_DEBUG","true"); Environment.SetEnvironmentVariable("NODE_OPTIONS","--untrusted-fixture-option");
                try { Wait(service.UnlockAsync("master-fixture",default)); }
                finally { Environment.SetEnvironmentVariable("BW_SESSION",null); Environment.SetEnvironmentVariable("BITWARDENCLI_DEBUG",null); Environment.SetEnvironmentVariable("NODE_OPTIONS",null); }
                var selected=Result(service.SearchAsync("fixture ; $() --session",default)).Single(); var password=Result(service.GetPasswordAsync(selected.Reference,default));
                if(password.Password!=Secret || JsonSerializer.Serialize(selected).Contains(Secret))throw new Exception("Real child process did not preserve password/metadata isolation");
                File.WriteAllText(Path.Combine(fixture,"mode"),"failure");
                try { Wait(service.StatusAsync(default)); throw new Exception("Failed CLI accepted"); } catch(BitwardenException ex) { if(ex.ToString().Contains(Secret))throw new Exception("Raw CLI stderr leaked"); }
                File.WriteAllText(Path.Combine(fixture,"mode"),"overflow");
                try { Wait(service.StatusAsync(default)); throw new Exception("Oversized CLI output accepted"); } catch(BitwardenException) { }
                File.WriteAllText(Path.Combine(fixture,"mode"),"delay");
                using var cancelled=new CancellationTokenSource(250);
                try { Wait(service.StatusAsync(cancelled.Token)); throw new Exception("CLI cancellation ignored"); } catch(OperationCanceledException) { }
                File.WriteAllText(Path.Combine(fixture,"mode"),"normal");
            }
            foreach(var line in File.ReadAllLines(Path.Combine(fixture,"calls.jsonl")))
            {
                using var call=JsonDocument.Parse(line); if(call.RootElement.GetProperty("debug").GetBoolean()||call.RootElement.GetProperty("node").GetBoolean()||line.Contains(Secret)||line.Contains("master-fixture")||line.Contains("SYNTHETIC_SESSION_CANARY"))throw new Exception("CLI child environment/arguments leaked inherited hooks or secrets");
            }
            foreach(var language in new[]{"pl","en"})
            {
                AppText.SetLanguage(language); var repo=new ProfileRepository(new(Path.Combine(root,language,"data-"+Guid.NewGuid().ToString("N")))); Wait(repo.InitializeAsync());
                var settings=Result(repo.ApplicationSettingsAsync()) with { Preferences=new() { Language=language,Bitwarden=options,ShowMetrics=false } }; Wait(repo.SaveApplicationSettingsAsync(settings));
                var vault=new Vault(); var main=new MainWindow(new(repo),repo,vault,new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(15);
                var profile=new ServerProfile { Name="Vault fixture",Host="fixture.invalid",Username="fixture" }; Wait(repo.SaveAsync(profile));
                var pane=new SessionPane(main,profile,new(repo),new ExternalRdpClient(),repo); main.RegisterPane(pane); RedesignChecks.Frames(4); var emulator=pane.Children.OfType<Iciclecreek.Terminal.TerminalControl>().Single().Terminal;
                var choice=main.ChooseBitwardenAsync(main,profile,null,true); RedesignChecks.Frames(3); var picker=main.OwnedWindows.Single();
                SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled,"Picker status did not finish");
                Named<TextBox>(picker,"BitwardenMasterPassword").Text="master-fixture"; Click(picker,"BitwardenUnlock");
                SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenFind").IsEnabled,"Picker did not unlock"); Click(picker,"BitwardenFind");
                SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUse").IsEnabled,"Picker search did not finish");
                picker.CaptureRenderedFrame()!.Save(Path.Combine(root,"vault-picker-"+language+".png")); Click(picker,"BitwardenUse"); var binding=Result(choice)!;
                if(binding.Reference?.ItemId!=Item || binding.Password!=null)throw new Exception("Binding picker fetched or retained a password");
                var linked=profile with { Bitwarden=binding.Reference }; Wait(repo.SaveAsync(linked));
                var editing=Dialogs.Edit(main,linked,[]); RedesignChecks.Frames(4); var editor=main.OwnedWindows.Single();
                if(!Named<TextBlock>(editor,"ProfileBitwardenReference").Text!.Contains(binding.Reference!.Name))throw new Exception("Profile editor lost the vault binding");
                if(OperatingSystem.IsWindows())
                {
                    Named<ComboBox>(editor,"ProfileProtocol").SelectedItem=Protocol.RDP; Named<TabControl>(editor,"ProfileEditorNavigation").SelectedIndex=3; RedesignChecks.Frames(3);
                    Named<ComboBox>(editor,"RdpDisplayMode").SelectedIndex=(int)RdpDisplayMode.External; Named<TabControl>(editor,"ProfileEditorNavigation").SelectedIndex=0; RedesignChecks.Frames(3);
                    if(Named<Button>(editor,"ProfileBitwardenBind").IsEnabled || !Named<TextBlock>(editor,"ProfileBitwardenAvailability").IsVisible)throw new Exception("External Windows RDP advertised unsupported vault credentials");
                    Named<ComboBox>(editor,"ProfileProtocol").SelectedItem=Protocol.SSH;
                }
                Click(editor,"ProfileBitwardenClear"); editor.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Zapisz")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(Result(editing)?.Bitwarden!=null || Result(repo.ListAsync()).Single().Bitwarden!=linked.Bitwarden)throw new Exception("Removing an editor binding changed data before the profile save");
                var obtain=main.ChooseBitwardenAsync(main,linked,linked.Bitwarden); RedesignChecks.Frames(3); picker=main.OwnedWindows.Single();
                SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled,"Bound picker status did not finish");
                Named<TextBox>(picker,"BitwardenMasterPassword").Text="master-fixture"; Click(picker,"BitwardenUnlock"); SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUse").IsEnabled,"Bound picker did not unlock"); Click(picker,"BitwardenUse");
                if(Result(obtain)?.Password!=Secret || vault.Writes!=0)throw new Exception("Bound entry was not retrieved for this session only");
                var credentials=Dialogs.Credentials(main,false,profile:profile); RedesignChecks.Frames(4); var credentialDialog=main.OwnedWindows.Single(); Click(credentialDialog,"CredentialBitwarden"); RedesignChecks.Frames(3); picker=credentialDialog.OwnedWindows.Single();
                SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled,"One-time picker status did not finish");
                Named<TextBox>(picker,"BitwardenMasterPassword").Text="master-fixture"; Click(picker,"BitwardenUnlock"); SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenFind").IsEnabled,"One-time picker did not unlock"); Click(picker,"BitwardenFind"); SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUse").IsEnabled,"One-time search did not finish"); Click(picker,"BitwardenUse");
                SshAcceptanceChecks.Until(()=>Named<TextBox>(credentialDialog,"CredentialPassword").Text==Secret,"One-time password did not fill credentials");
                if(credentialDialog.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked==true || vault.Writes!=0)throw new Exception("One-time selection opted into saving");
                credentialDialog.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Anuluj")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(credentials);
                // Cancel/drain work without changing the existing terminal, profile or binding.
                File.WriteAllText(Path.Combine(fixture,"mode"),"delay"); var pending=main.ChooseBitwardenAsync(main,linked,linked.Bitwarden); RedesignChecks.Frames(3);
                Wait(main.ApplyPluginsAsync(settings.Preferences with { DisabledPlugins=new([PluginCatalog.Bitwarden]) })); Wait(pending);
                if(pending.Result!=null || main.OwnedWindows.Any() || !ReferenceEquals(emulator,pane.Children.OfType<Iciclecreek.Terminal.TerminalControl>().Single().Terminal) || Result(repo.ListAsync()).Single().Bitwarden!=linked.Bitwarden)throw new Exception("Disabling vault plugin changed core session/data or returned a stale secret");
                if(Result(main.ChooseBitwardenAsync(main,linked,linked.Bitwarden))!=null)throw new Exception("Disabled plugin opened a picker");
                File.WriteAllText(Path.Combine(fixture,"mode"),"normal"); Wait(main.ApplyPluginsAsync(settings.Preferences));
                main.BitwardenFactory=_=>throw new InvalidOperationException(Secret);
                var failed=main.ChooseBitwardenAsync(main,profile,null); RedesignChecks.Frames(4); picker=main.OwnedWindows.Single();
                if(!Named<TextBlock>(picker,"BitwardenError").IsVisible || Named<TextBlock>(picker,"BitwardenError").Text!.Contains(Secret))throw new Exception("Unexpected vault error leaked raw data");
                picker.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Anuluj")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(failed); main.BitwardenFactory=BitwardenCli.Create;
                var edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(5); var settingsWindow=main.OwnedWindows.Single(); Named<TabControl>(settingsWindow,"SettingsCategories").SelectedIndex=6; RedesignChecks.Frames(3);
                if(Named<TextBox>(settingsWindow,"BitwardenExecutable").Text!=exe)throw new Exception("CLI configuration missing from settings");
                settingsWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"vault-settings-"+language+".png")); settingsWindow.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Anuluj")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(edit);
                main.Close(); RedesignChecks.Frames(3); var confirm=main.OwnedWindows.Single(); confirm.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Potwierdź")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); SshAcceptanceChecks.Until(()=>!main.IsVisible,"Vault fixture did not close");
            }
            using var logFile=new FileStream(Path.Combine(root,"ui-diagnostics.log"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            using var logReader=new StreamReader(logFile); var diagnostics=logReader.ReadToEnd();
            if(new[]{Secret,"master-fixture","SYNTHETIC_SESSION_CANARY"}.Any(diagnostics.Contains))throw new Exception("Vault secret reached application diagnostics");
            AppText.SetLanguage("pl"); Console.WriteLine("PASS Bitwarden native child environment/arguments, bounded output/stderr, cancellation, metadata-only binding, selected/one-time password, editor removal, PL/EN picker/settings, safe error diagnostics and plugin disable/drain/re-enable without core session/data changes; synthetic data only");
        }
        finally { Environment.SetEnvironmentVariable("SERVERDESK_BITWARDEN_FIXTURE",null); }
    }
}
