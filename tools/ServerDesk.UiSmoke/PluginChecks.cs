using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class PluginChecks
{
    private sealed class Vault:ICredentialStore
    {
        internal readonly Dictionary<Guid,string> Data=[];
        internal int Reads;
        internal bool Block;
        internal TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource never=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Reads++; if(Block) { Entered.TrySetResult(); await never.Task.WaitAsync(ct); } return Data.GetValueOrDefault(id); }
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default) { Data[id]=value; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { Data.Remove(id); return Task.CompletedTask; }
    }
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(value)!;
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Control root,string tag)=>root.GetVisualDescendants().OfType<Button>().Single(c=>c.Tag as string==tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Close(MainWindow main) { main.Close(); RedesignChecks.Frames(3); if(main.OwnedWindows.FirstOrDefault() is {} dialog)Click(dialog,"Potwierdź"); SshAcceptanceChecks.Until(()=>!main.IsVisible,"Plugin fixture did not close"); }
    private static Window Settings(MainWindow main,ProfileRepository repo,out Task edit)
    { edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); var window=main.OwnedWindows.Single(); Named<TabControl>(window,"SettingsCategories").SelectedIndex=6; RedesignChecks.Frames(); return window; }
    private static void Toggles(Window window,bool monitoring,bool commands)
    { Named<CheckBox>(window,"PluginEnabled_linux_monitoring").IsChecked=monitoring; Named<CheckBox>(window,"PluginEnabled_commands").IsChecked=commands; RedesignChecks.Frames(); }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var repo=new ProfileRepository(new(Path.Combine(root,"data-"+language+"-"+Guid.NewGuid().ToString("N")))); Wait(repo.InitializeAsync()); var vault=new Vault(); var store=new CommandStore(repo,vault);
            var settings=Result(repo.ApplicationSettingsAsync()) with { Preferences=new() { Language=language,DisabledPlugins=new(["future-addon"]) } }; Wait(repo.SaveApplicationSettingsAsync(settings));
            var custom=new CommandDefinition { Id="user.plugin-fixture",Origin=CommandOrigin.User,Name="Preserved snippet",Category="Fixture",Template="printf fixture",Shell=CommandShell.Bash };
            Wait(store.SaveLibraryAsync(new() { Commands=[custom],Categories=["Fixture"] })); var state=new CommandUserState { Favorites=["linux.net.listeners"] }; Wait(store.SaveStateAsync(state)); var savedVault=vault.Data.ToDictionary();
            var main=new MainWindow(new(repo),repo,vault,new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            if(!Field<IWorkspacePlugin[]>(main,"plugins").Select(p=>p.Id).Order().SequenceEqual(PluginCatalog.BuiltIn.Select(p=>p.Id).Order()))throw new Exception("Plugin catalogue/lifecycle registrations differ");
            using var pane=new SessionPane(main,new() { Name="Plugin SSH",Host="fixture.invalid",Username="fixture" },new(repo),new ExternalRdpClient(),repo); main.RegisterPane(pane);
            if(!main.IsPluginEnabled(PluginCatalog.Commands)||!main.IsPluginEnabled(PluginCatalog.LinuxMonitoring))throw new Exception("Legacy defaults disabled plugins");
            Wait(main.OpenCommandsPanelAsync()); var old=Named<CommandPanel>(main,"CommandsPanel"); if(old.UserLibrary.Commands.Single().Id!=custom.Id)throw new Exception("Own snippet missing before toggle");
            var window=Settings(main,repo,out var edit); Toggles(window,false,false);
            if(!main.IsPluginEnabled(PluginCatalog.Commands)||!Field<Border>(main,"commandPanelHost").IsVisible)throw new Exception("Plugin preview changed live state before Save");
            foreach(var theme in new[]{"graphite","light"}) { DesignSystem.Apply(theme); main.PreviewPreferences(main.CurrentPreferences with { InterfaceFontSize=16 }); window.Width=680; window.Height=500; RedesignChecks.Frames(); window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"plugins-{language}-{theme}.png")); }
            Click(window,"Anuluj"); Wait(edit); if(Result(repo.ApplicationSettingsAsync())!=settings||!main.IsPluginEnabled(PluginCatalog.Commands))throw new Exception("Cancel saved plugin state");
            window=Settings(main,repo,out edit); Toggles(window,false,false); Click(window,"Zapisz"); Wait(edit);
            if(main.IsPluginEnabled(PluginCatalog.Commands)||main.IsPluginEnabled(PluginCatalog.LinuxMonitoring)||Field<Border>(main,"commandPanelHost").IsVisible)throw new Exception("Disable did not close optional panel");
            if(Field<Button>(pane,"commandsButton").IsVisible||Field<Dictionary<int,Button>>(pane,"panelButtons")[2].IsVisible||Field<MenuItem>(main,"commandsMenu").IsVisible||Field<Dictionary<string,Button>>(main,"activityButtons")["commands"].IsVisible)throw new Exception("Disabled plugin left visible entry points");
            if(!Field<Grid>(main,"workspaceStatus").IsVisible||Field<ServerMetricsBar>(main,"metricsBar").IsVisible)throw new Exception("Monitoring toggle affected core status or retained metrics");
            var reads=vault.Reads; Wait(main.OpenCommandsPanelAsync()); main.KeyPress(Key.K,RawInputModifiers.Control|RawInputModifiers.Shift,PhysicalKey.K,null); RedesignChecks.Frames();
            if(vault.Reads!=reads||Field<Border>(main,"commandPanelHost").IsVisible)throw new Exception("Disabled command entry point touched the vault");
            var palette=(Task)typeof(MainWindow).GetMethod("CommandsAsync",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(main,null)!; RedesignChecks.Frames(); window=main.OwnedWindows.Single();
            if(Named<ListBox>(window,"CommandResults").Items.OfType<string>().Any(s=>s.StartsWith(AppText.T("Komendy")+" ·")))throw new Exception("Disabled plugin retained palette action"); Click(window,"Anuluj"); Wait(palette);
            pane.PrepareRestored(2); if(pane.SelectedPanel!=0)throw new Exception("Restored tab selected disabled monitoring");
            var persisted=Result(repo.PreferencesAsync()); if(!persisted.DisabledPlugins.Contains("future-addon"))throw new Exception("Unknown plugin opt-out lost");
            if(savedVault.Count!=vault.Data.Count||savedVault.Any(p=>vault.Data.GetValueOrDefault(p.Key)!=p.Value)||!Result(store.ReadStateAsync()).Favorites.Contains("linux.net.listeners"))throw new Exception("Toggle modified stored user commands");
            Close(main); var resumed=new MainWindow(new(repo),repo,vault,new(repo),new ExternalRdpClient()); reads=vault.Reads; resumed.Show(); RedesignChecks.Frames(20);
            if(resumed.IsPluginEnabled(PluginCatalog.Commands)||resumed.IsPluginEnabled(PluginCatalog.LinuxMonitoring)||vault.Reads!=reads)throw new Exception("Disabled plugins resumed after restart");
            window=Settings(resumed,repo,out edit); Toggles(window,true,true); if(resumed.IsPluginEnabled(PluginCatalog.Commands))throw new Exception("Enable applied before Save"); Click(window,"Zapisz"); Wait(edit); Wait(resumed.OpenCommandsPanelAsync());
            if(Named<CommandPanel>(resumed,"CommandsPanel").UserLibrary.Commands.Single().Id!=custom.Id)throw new Exception("Own snippets not restored after enable");
            resumed.CloseCommandsPanel(); vault.Block=true; var opening=resumed.OpenCommandsPanelAsync(); // existing panel does not need vault; replace via a complete disable cycle
            Wait(opening); Wait(resumed.ApplyPluginsAsync(persisted)); var enabled=persisted with { DisabledPlugins=new(["future-addon"]) }; Wait(resumed.ApplyPluginsAsync(enabled)); opening=resumed.OpenCommandsPanelAsync();
            SshAcceptanceChecks.Until(()=>vault.Entered.Task.IsCompleted,"Delayed vault read did not start"); Wait(resumed.ApplyPluginsAsync(persisted)); Wait(opening); vault.Block=false;
            if(Field<Border>(resumed,"commandPanelHost").IsVisible)throw new Exception("Late command initialization reopened disabled plugin");
            Wait(resumed.ApplyPluginsAsync(enabled)); Wait(resumed.OpenCommandsPanelAsync()); Close(resumed);
            Console.WriteLine("PASS "+language+": plugin preview/cancel/save/restart, all entry points, preserved snippets/vault/favorites/unknown opt-outs, enable without restart and cancelled delayed initialization; compact font16 Graphite/Light");
        }
        AppText.SetLanguage("pl"); DesignSystem.Apply("dark");
    }
    internal static void Network(string root,string host,string key)
    {
        var repo=new ProfileRepository(new(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N")))); Wait(repo.InitializeAsync()); Wait(repo.SetSettingAsync("monitorInterval","2"));
        var factory=new SshConnectionFactory(repo); var main=new MainWindow(new(repo),repo,new Vault(),factory,new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20); factory.ConfirmHostKey=(_,_)=>Task.FromResult(true);
        var profile=new ServerProfile { Name="Disposable plugin SSH",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo); main.RegisterPane(pane);
        try
        {
            Wait(pane.ConnectAsync("serverdesk-test-passphrase",default)); SshAcceptanceChecks.Until(()=>pane.LatestSnapshot!=null&&pane.KnownCommandEnvironment!=null,"Optional SSH plugins did not start");
            var terminal=Field<SshTerminalSession>(pane,"session"); var identity=pane.CommandConnectionId; var monitoring=Field<LinuxMonitoring>(pane,"monitor");
            var client=(Renci.SshNet.SshClient)typeof(LinuxMonitoring).GetFields(BindingFlags.NonPublic|BindingFlags.Instance).Single(f=>f.FieldType==typeof(Renci.SshNet.SshClient)).GetValue(monitoring)!;
            var scope=Field<CancellationTokenSource>(pane,"commandPluginLifetime"); var token=scope.Token;
            async Task<CommandEnvironment> SlowProbe() { await terminal.ProbeAsync("sleep 20",token); return CommandDetection.Unknown(); }
            var pending=SlowProbe(); typeof(SessionPane).GetField("commandDiscovery",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(pane,pending); RedesignChecks.Frames(5);
            var disabled=main.CurrentPreferences with { DisabledPlugins=new([PluginCatalog.Commands,PluginCatalog.LinuxMonitoring]) }; var stop=DateTime.UtcNow; Wait(main.ApplyPluginsAsync(disabled));
            var monitorConnected=false; try { monitorConnected=client.IsConnected; } catch(ObjectDisposedException) { }
            if(DateTime.UtcNow-stop>TimeSpan.FromSeconds(8)||!pending.IsCompleted||monitorConnected||pane.LatestSnapshot!=null||pane.MetricsError.Length>0)throw new Exception("Disabled plugins retained pending SSH work");
            if(!ReferenceEquals(terminal,Field<SshTerminalSession>(pane,"session"))||!pane.IsConnected||pane.CommandConnectionId!=identity||pane.CanAcceptCommands)throw new Exception("Plugin disable restarted/disconnected the SSH terminal");
            var second=new SessionPane(main,profile with { Id=Guid.NewGuid(),Name="Plugins disabled before connect" },factory,new ExternalRdpClient(),repo); main.RegisterPane(second);
            Wait(second.ConnectAsync("serverdesk-test-passphrase",default)); RedesignChecks.Frames(10);
            if(Field<LinuxMonitoring?>(second,"monitor")!=null||second.KnownCommandEnvironment!=null||second.CanAcceptCommands)throw new Exception("Disabled plugins ran on a new SSH session");
            main.RemovePane(second); Wait(second.CloseAsync()); main.FocusSession(pane);
            var echo=Result(terminal.ProbeAsync("printf PLUGIN_CORE_ALIVE",default)); if(echo.Output!="PLUGIN_CORE_ALIVE")throw new Exception("Terminal unusable after plugin probe cancellation");
            Wait(pane.ShowAsync(1)); if(!pane.IsConnected)throw new Exception("SFTP stopped with optional plugins");
            Wait(main.ApplyPluginsAsync(main.CurrentPreferences with { DisabledPlugins=PluginSelection.Empty })); SshAcceptanceChecks.Until(()=>pane.LatestSnapshot!=null&&pane.CanAcceptCommands,"Plugins did not resume on existing terminal");
            if(ReferenceEquals(monitoring,Field<LinuxMonitoring>(pane,"monitor"))||!ReferenceEquals(terminal,Field<SshTerminalSession>(pane,"session")))throw new Exception("Enable reused a disposed monitor or replaced terminal");
            Console.WriteLine("PASS real OpenSSH plugin lifecycle: metrics and discovery, cancellation of a live slow exec probe, disposed dedicated monitor, no stale samples/errors, same SSH terminal/identity, working SFTP and enable on the existing connection");
        }
        finally { Close(main); }
    }
}
