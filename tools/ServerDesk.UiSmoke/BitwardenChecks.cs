using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
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
            "list"=>mode=="empty"?"[]":mode=="keys"?"["+Entry()+","+KeyEntry()+"]":"["+Entry()+"]",
            "get"=>mode=="keys"?KeyEntry():Entry(),
            _=>throw new Exception("Unexpected CLI mutation")
        };
        Console.Write(json);
        static string Entry()=>JsonSerializer.Serialize(new { id=Item,type=1,name="Fixture vault server",login=new { username="fixture",password=Secret } });
        string KeyEntry()
        {
            var pem=File.ReadAllText(Path.Combine(root,"ssh-key.fixture"));
            using var stream=new MemoryStream(Encoding.UTF8.GetBytes(pem)); using var key=new Renci.SshNet.PrivateKeyFile(stream); var data=key.HostKeyAlgorithms.First().Data;
            return JsonSerializer.Serialize(new { id=Guid.Parse("c2f7a0ac-c378-4efb-8369-b21a402ab63a"),type=5,name="Fixture SSH key",sshKey=new { privateKey=pem,publicKey="ssh-rsa "+Convert.ToBase64String(data),keyFingerprint="SHA256:"+Convert.ToBase64String(SHA256.HashData(data)).TrimEnd('=') } });
        }
    }
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Control root,string name)=>Named<Button>(root,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    private static void PickerUsability(Window picker,string fixture,string root,string language)
    {
        var master=Named<TextBox>(picker,"BitwardenMasterPassword"); var search=Named<TextBox>(picker,"BitwardenSearch");
        var list=Named<ListBox>(picker,"BitwardenEntries"); var use=Named<Button>(picker,"BitwardenUse");
        var progress=Named<ProgressBar>(picker,"BitwardenProgress"); var error=Named<TextBlock>(picker,"BitwardenError");
        int Calls(string command)=>File.ReadAllLines(Path.Combine(fixture,"calls.jsonl")).Count(line=> { using var json=JsonDocument.Parse(line); return json.RootElement.GetProperty("args")[0].GetString()==command; });
        void Mode(string mode)=>File.WriteAllText(Path.Combine(fixture,"mode"),mode);
        void Enter()=>picker.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r");
        void Completed()=>SshAcceptanceChecks.Until(()=>!progress.IsVisible,"Picker operation did not complete");
        var reads=Calls("get");
        if(use.IsEnabled || use.IsVisible || Named<StackPanel>(picker,"BitwardenSearchSection").IsVisible || !master.IsKeyboardFocusWithin || master.PasswordChar!='●')throw new Exception("Locked picker did not show only the unlock step with protected input");
        var help=Named<Expander>(picker,"BitwardenSetupHelp"); help.IsExpanded=true; RedesignChecks.Frames(3);
        var technical=Named<Expander>(picker,"BitwardenTerminalInstructions");
        if(technical.IsExpanded || !help.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==AppText.T("1. Zainstaluj Bitwarden CLI")))throw new Exception("First-time setup did not start with plain numbered steps");
        technical.IsExpanded=true; RedesignChecks.Frames(2);
        if(!technical.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("bw config server")==true))throw new Exception("Detailed offline CLI instructions missing"); technical.IsExpanded=false; help.IsExpanded=false;
        picker.CaptureRenderedFrame()!.Save(Path.Combine(root,"vault-locked-"+language+".png"));
        Mode("failure"); Click(picker,"BitwardenRetry"); Completed();
        if(!error.IsVisible || error.Text!.Contains(Secret) || Named<Button>(picker,"BitwardenUnlock").IsEnabled)throw new Exception("Failed status check left a misleading unlock action");
        Mode("normal"); Click(picker,"BitwardenRetry"); SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled,"Status retry did not recover");
        master.Text="wrong-fixture"; master.Focus(); Enter(); Completed();
        if(!error.IsVisible || !string.IsNullOrEmpty(master.Text) || !master.IsKeyboardFocusWithin || !Named<Grid>(picker,"BitwardenUnlockRow").IsVisible || use.IsEnabled)throw new Exception("Rejected master password was retained or could not be retried from the keyboard");
        master.Text="master-fixture"; master.Focus(); Enter(); SshAcceptanceChecks.Until(()=>use.IsEnabled,"Unlock did not automatically search the profile host");
        if(Named<Grid>(picker,"BitwardenUnlockRow").IsVisible || !string.IsNullOrEmpty(master.Text) || !search.IsKeyboardFocusWithin || !Named<StackPanel>(picker,"BitwardenSearchSection").IsVisible || !use.IsVisible || Named<Border>(picker,"BitwardenSelectionDetails").IsVisible || !list.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==AppText.F($"Login: {"fixture"}")))throw new Exception("Unlocked picker did not show a single labeled selection step");
        search.Text="fixture immediate edit"; Click(picker,"BitwardenUse");
        if(!picker.IsVisible || use.IsEnabled || list.ItemCount!=0 || Calls("get")!=reads)throw new Exception("Immediate query edit accepted the previous result before TextChanged dispatch");
        search.Text="x"; RedesignChecks.Frames(2); var searches=Calls("list"); Enter(); RedesignChecks.Frames(2);
        if(use.IsEnabled || Named<Button>(picker,"BitwardenFind").IsEnabled || list.ItemCount!=0 || Calls("list")!=searches || Named<Border>(picker,"BitwardenSelectionDetails").IsVisible)throw new Exception("Short query retained stale results or launched a search");
        search.Text="fixture edited"; Click(picker,"BitwardenUse");
        if(!picker.IsVisible || Calls("get")!=reads)throw new Exception("Editing a query accepted a stale result");
        Mode("empty"); Enter(); Completed();
        if(use.IsEnabled || list.IsVisible || !Named<TextBlock>(picker,"BitwardenEmpty").IsVisible || !Named<TextBlock>(picker,"BitwardenEmpty").Text!.Contains(language=="pl"?"Brak pasujących":"No matching"))throw new Exception("Empty search did not explain how to recover");
        Mode("failure"); search.Text="fixture failed"; Enter(); Completed();
        if(!error.IsVisible || error.Text!.Contains(Secret) || use.IsEnabled || Named<TextBlock>(picker,"BitwardenEmpty").Text!.Contains('…') || !search.IsKeyboardFocusWithin)throw new Exception("Failed search retained stale results/loading state or lost keyboard focus");
        Mode("normal"); search.Text="fixture recovered"; Enter(); SshAcceptanceChecks.Until(()=>use.IsEnabled,"Search retry did not recover");
        if(error.IsVisible || Calls("get")!=reads)throw new Exception("Metadata browsing fetched a password or retained an old error");
        picker.KeyPress(Key.Down,RawInputModifiers.None,PhysicalKey.ArrowDown,""); RedesignChecks.Frames(2);
        if(!list.IsKeyboardFocusWithin)throw new Exception("Arrow Down did not focus results");
        picker.KeyPress(Key.F,RawInputModifiers.Control,PhysicalKey.F,"f"); RedesignChecks.Frames(2);
        if(!search.IsKeyboardFocusWithin)throw new Exception("Ctrl+F did not focus vault search");
        picker.CaptureRenderedFrame()!.Save(Path.Combine(root,"vault-picker-"+language+".png"));
        var purpose=Named<TextBlock>(picker,"BitwardenPurpose"); var scroll=Named<ScrollViewer>(picker,"DialogScroll");
        var purposeBottom=purpose.TranslatePoint(default,picker)!.Value.Y+purpose.Bounds.Height; var viewportBottom=scroll.TranslatePoint(default,picker)!.Value.Y+scroll.Bounds.Height;
        if(purposeBottom>viewportBottom+1)throw new Exception("The next-step explanation is clipped in the normal one-result layout");
        var width=picker.Width; picker.Width=400; RedesignChecks.Frames(4); picker.SetRenderScaling(1.5); RedesignChecks.Frames(4);
        var actions=Named<StackPanel>(picker,"DialogActions"); var row=Named<Grid>(picker,"BitwardenSearchRow");
        if(actions.Orientation!=Avalonia.Layout.Orientation.Vertical || row.ColumnDefinitions.Count!=1 || row.Children[0].Bounds.Width<150 || row.Children[1].Bounds.Y<row.Children[0].Bounds.Bottom)throw new Exception("Narrow picker did not stack controls cleanly");
        picker.CaptureRenderedFrame()!.Save(Path.Combine(root,"vault-picker-compact-"+language+".png")); picker.SetRenderScaling(1); picker.Width=width; RedesignChecks.Frames(4);
        Click(picker,"BitwardenClearSearch"); RedesignChecks.Frames(2);
        if(search.Text!="" || list.ItemCount!=0 || use.IsEnabled)throw new Exception("Clear search retained a selectable item");
        search.Text="fixture"; Enter(); SshAcceptanceChecks.Until(()=>use.IsEnabled,"Final fixture search did not complete");
        list.Focus(); Enter();
    }
    private sealed class Vault:ICredentialStore
    {
        internal int Reads,Writes;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Reads++; return Task.FromResult<string?>(null); }
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default) { Writes++; throw new Exception("Vault picker must not save credentials"); }
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>throw new Exception("Vault picker must not delete credentials");
    }
    private static void KeyPicker(MainWindow main,ProfileRepository repo,string fixture,string root,string language,Vault vault)
    {
        File.WriteAllText(Path.Combine(fixture,"mode"),"keys");
        var profile=new ServerProfile { Name="Key fixture",Host="fixture.invalid",Username="fixture-user",Authentication=Authentication.VaultSshKey };
        var reads=vault.Reads; var writes=vault.Writes;
        var choose=main.ChooseBitwardenAsync(main,profile,null,true); RedesignChecks.Frames(3); var picker=main.OwnedWindows.Single();
        SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled,"Key binding status did not finish");
        Named<TextBox>(picker,"BitwardenMasterPassword").Text="master-fixture"; Click(picker,"BitwardenUnlock"); SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUse").IsEnabled,"SSH key search did not finish");
        if(Named<ListBox>(picker,"BitwardenEntries").ItemCount!=1 || Named<ListBox>(picker,"BitwardenEntries").SelectedItem is not BitwardenEntry { Reference.Kind:BitwardenItemKind.SshKey })throw new Exception("Key mode mixed Login passwords with SSH Key items");
        picker.CaptureRenderedFrame()!.Save(Path.Combine(root,"vault-ssh-key-"+language+".png")); Click(picker,"BitwardenUse"); using var binding=Result(choose)!;
        if(binding.Reference?.Kind!=BitwardenItemKind.SshKey || binding.Password!=null || binding.TakeSshKey()!=null)throw new Exception("Binding retrieved SSH private material instead of metadata");
        profile=profile with { Bitwarden=binding.Reference }; Wait(repo.SaveAsync(profile));
        var obtain=main.ChooseBitwardenAsync(main,profile,profile.Bitwarden); RedesignChecks.Frames(3); picker=main.OwnedWindows.Single();
        if(picker.GetVisualDescendants().OfType<Button>().Any(b=>b.Name=="BitwardenManual"))throw new Exception("SSH key mode offered an invalid password fallback");
        SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled,"SSH key status did not finish"); Named<TextBox>(picker,"BitwardenMasterPassword").Text="master-fixture"; Click(picker,"BitwardenUnlock"); SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUse").IsEnabled,"SSH key picker did not unlock"); Click(picker,"BitwardenUse");
        using var choice=Result(obtain)!; using var material=choice.TakeSshKey()??throw new Exception("SSH key selection did not return owned material");
        if(choice.Password!=null || material.Algorithm!="RSA" || material.IsDisposed || vault.Reads!=reads || vault.Writes!=writes)throw new Exception("SSH private material passed through password or system-credential handling");
        var edit=Dialogs.Edit(main,profile,[]); RedesignChecks.Frames(4); var editor=main.OwnedWindows.Single();
        if(Named<ComboBox>(editor,"ProfileAuthentication").SelectedItem is not Authentication.VaultSshKey || Named<TextBox>(editor,"ProfilePrivateKey").IsEffectivelyVisible)throw new Exception("SSH vault mode asked for a local key file");
        Named<ComboBox>(editor,"ProfileProtocol").SelectedItem=Protocol.RDP; RedesignChecks.Frames(3);
        if(Named<ComboBox>(editor,"ProfileAuthentication").SelectedItem is not Authentication.Password || Named<Button>(editor,"ProfileBitwardenClear").IsVisible)throw new Exception("Changing protocol retained an SSH-only key binding");
        editor.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Anuluj")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(edit);
        if(Result(repo.ListAsync()).Single(p=>p.Id==profile.Id).Bitwarden!=profile.Bitwarden)throw new Exception("Cancelling an editor changed its saved key binding");
        using(var abandonedMaterial=material.Clone()) { var abandoned=new BitwardenChoice(profile.Bitwarden,"",null,sshKey:abandonedMaterial); abandoned.Dispose(); if(!abandonedMaterial.IsDisposed)throw new Exception("Abandoned picker choice retained key bytes"); }
        var pane=new SessionPane(main,profile,new(repo),new ExternalRdpClient(),repo); using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        try { Wait(pane.ConnectAsync("",cancelled.Token,material)); throw new Exception("Cancelled key connection started"); } catch(OperationCanceledException) { }
        if(pane.HasVaultKeyMaterial || material.IsDisposed)throw new Exception("Cancelled pane retained or stole caller-owned key material"); Wait(pane.CloseAsync());
        File.WriteAllText(Path.Combine(fixture,"mode"),"normal");
    }
    internal static void Run(string root)
    {
        var fixture=Path.Combine(root,"cli-fixture"); Directory.CreateDirectory(fixture); File.WriteAllText(Path.Combine(fixture,"fixture.enabled"),FixtureMarker); File.WriteAllText(Path.Combine(fixture,"mode"),"normal");
        using(var rsa=RSA.Create(2048))File.WriteAllText(Path.Combine(fixture,"ssh-key.fixture"),rsa.ExportRSAPrivateKeyPem());
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
                var unboundEdit=Dialogs.Edit(main,profile,[]); RedesignChecks.Frames(3); var unboundEditor=main.OwnedWindows.Single();
                if(Named<Button>(unboundEditor,"ProfileBitwardenClear").IsVisible || Avalonia.Automation.AutomationProperties.GetName(Named<Button>(unboundEditor,"ProfileBitwardenBind"))!=AppText.T("Przypisz wpis sejfu"))throw new Exception("Unbound editor offered an irrelevant remove action");
                unboundEditor.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Anuluj")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(unboundEdit);
                var pane=new SessionPane(main,profile,new(repo),new ExternalRdpClient(),repo); main.RegisterPane(pane); RedesignChecks.Frames(4); var emulator=pane.Children.OfType<Iciclecreek.Terminal.TerminalControl>().Single().Terminal;
                var choice=main.ChooseBitwardenAsync(main,profile,null,true); RedesignChecks.Frames(3); var picker=main.OwnedWindows.Single();
                SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled,"Picker status did not finish");
                PickerUsability(picker,fixture,root,language); var binding=Result(choice)!;
                if(binding.Reference?.ItemId!=Item || binding.Password!=null)throw new Exception("Binding picker fetched or retained a password");
                var linked=profile with { Bitwarden=binding.Reference }; Wait(repo.SaveAsync(linked));
                var editing=Dialogs.Edit(main,linked,[]); RedesignChecks.Frames(4); var editor=main.OwnedWindows.Single();
                if(!Named<TextBlock>(editor,"ProfileBitwardenReference").Text!.Contains(binding.Reference!.Name) || !Named<Button>(editor,"ProfileBitwardenClear").IsVisible || Avalonia.Automation.AutomationProperties.GetName(Named<Button>(editor,"ProfileBitwardenBind"))!=AppText.T("Zmień wpis sejfu"))throw new Exception("Bound profile did not clearly show its change/remove actions");
                if(OperatingSystem.IsWindows())
                {
                    Named<ComboBox>(editor,"ProfileProtocol").SelectedItem=Protocol.RDP; Named<TabControl>(editor,"ProfileEditorNavigation").SelectedIndex=3; RedesignChecks.Frames(3);
                    Named<ComboBox>(editor,"RdpDisplayMode").SelectedIndex=(int)RdpDisplayMode.External; Named<TabControl>(editor,"ProfileEditorNavigation").SelectedIndex=0; RedesignChecks.Frames(3);
                    if(Named<Button>(editor,"ProfileBitwardenBind").IsEnabled || !Named<TextBlock>(editor,"ProfileBitwardenAvailability").IsVisible)throw new Exception("External Windows RDP advertised unsupported vault credentials");
                    Named<ComboBox>(editor,"ProfileProtocol").SelectedItem=Protocol.SSH;
                }
                Click(editor,"ProfileBitwardenClear");
                if(Named<Button>(editor,"ProfileBitwardenClear").IsVisible || Avalonia.Automation.AutomationProperties.GetName(Named<Button>(editor,"ProfileBitwardenBind"))!=AppText.T("Przypisz wpis sejfu"))throw new Exception("Removing a draft binding did not restore the simple assign action");
                editor.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Zapisz")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if(Result(editing)?.Bitwarden!=null || Result(repo.ListAsync()).Single().Bitwarden!=linked.Bitwarden)throw new Exception("Removing an editor binding changed data before the profile save");
                var obtain=main.ChooseBitwardenAsync(main,linked,linked.Bitwarden); RedesignChecks.Frames(3); picker=main.OwnedWindows.Single();
                SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled,"Bound picker status did not finish");
                Named<TextBox>(picker,"BitwardenMasterPassword").Text="master-fixture"; Click(picker,"BitwardenUnlock"); SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUse").IsEnabled,"Bound picker did not unlock"); Click(picker,"BitwardenUse");
                if(Result(obtain)?.Password!=Secret || vault.Writes!=0)throw new Exception("Bound entry was not retrieved for this session only");
                var mismatch=main.ChooseBitwardenAsync(main,linked with { Username="different-fixture" },linked.Bitwarden); RedesignChecks.Frames(3); picker=main.OwnedWindows.Single();
                SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled,"Mismatch picker status did not finish");
                Named<TextBox>(picker,"BitwardenMasterPassword").Text="master-fixture"; Click(picker,"BitwardenUnlock"); SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUse").IsEnabled,"Mismatch picker did not unlock"); Click(picker,"BitwardenUse");
                SshAcceptanceChecks.Until(()=>picker.OwnedWindows.Any(),"Different username did not require confirmation");
                var mismatchConfirm=picker.OwnedWindows.Single(); mismatchConfirm.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Anuluj")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUse").IsEnabled,"Declined username confirmation did not return to the picker");
                if(mismatch.IsCompleted || !Named<Button>(picker,"BitwardenUse").IsKeyboardFocusWithin)throw new Exception("Declined username confirmation accepted a password or lost keyboard focus");
                Click(picker,"BitwardenManual"); if(Result(mismatch) is not { Manual:true,Password:null })throw new Exception("Bound picker did not preserve the session-only manual fallback");
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
                main.BitwardenFactory=_=>throw new BitwardenException(BitwardenFailure.Configuration);
                var setupNeeded=main.ChooseBitwardenAsync(main,profile,null); RedesignChecks.Frames(4); picker=main.OwnedWindows.Single();
                if(Named<Grid>(picker,"BitwardenUnlockRow").IsVisible || Named<StackPanel>(picker,"BitwardenSearchSection").IsVisible || Named<Button>(picker,"BitwardenUse").IsVisible || !Named<Expander>(picker,"BitwardenSetupHelp").IsExpanded || Named<TextBlock>(picker,"BitwardenState").Text!=AppText.T("Najpierw przygotuj sejf"))throw new Exception("Unconfigured vault did not guide the user to setup before asking for a password");
                picker.CaptureRenderedFrame()!.Save(Path.Combine(root,"vault-setup-required-"+language+".png"));
                main.BitwardenFactory=BitwardenCli.Create; Click(picker,"BitwardenRetry");
                SshAcceptanceChecks.Until(()=>Named<Button>(picker,"BitwardenUnlock").IsEnabled,"Setup retry did not recover after configuration");
                if(!Named<Grid>(picker,"BitwardenUnlockRow").IsVisible || !Named<TextBox>(picker,"BitwardenMasterPassword").IsKeyboardFocusWithin || Named<Button>(picker,"BitwardenUse").IsVisible)throw new Exception("Recovered setup did not return to the unlock step");
                picker.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Anuluj")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(setupNeeded);
                var edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(5); var settingsWindow=main.OwnedWindows.Single(); Named<TabControl>(settingsWindow,"SettingsCategories").SelectedIndex=6; RedesignChecks.Frames(3);
                if(Named<TextBox>(settingsWindow,"BitwardenExecutable").Text!=exe)throw new Exception("CLI configuration missing from settings");
                if(Named<Expander>(settingsWindow,"BitwardenSetupHelp").IsExpanded)throw new Exception("Settings overwhelmed the main fields with expanded setup instructions");
                settingsWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"vault-settings-"+language+".png")); settingsWindow.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Anuluj")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait(edit);
                KeyPicker(main,repo,fixture,root,language,vault);
                main.Close(); RedesignChecks.Frames(3); var confirm=main.OwnedWindows.Single(); confirm.GetVisualDescendants().OfType<Button>().Single(b=>Equals(b.Tag,"Potwierdź")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); SshAcceptanceChecks.Until(()=>!main.IsVisible,"Vault fixture did not close");
            }
            using var logFile=new FileStream(Path.Combine(root,"ui-diagnostics.log"),FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            using var logReader=new StreamReader(logFile); var diagnostics=logReader.ReadToEnd();
            if(new[]{Secret,"master-fixture","SYNTHETIC_SESSION_CANARY",File.ReadLines(Path.Combine(fixture,"ssh-key.fixture")).Skip(1).First()}.Any(diagnostics.Contains))throw new Exception("Vault secret reached application diagnostics");
            AppText.SetLanguage("pl"); Console.WriteLine("PASS Bitwarden native child safety, cancellation and credential isolation; PL/EN two-step unlock/select flow, labeled usernames, simple/collapsed detailed setup in picker/settings, unconfigured recovery, contextual profile actions, automatic search, stale-result guards, keyboard and compact 150% layout; synthetic data only");
        }
        finally { Environment.SetEnvironmentVariable("SERVERDESK_BITWARDEN_FIXTURE",null); }
    }
}
