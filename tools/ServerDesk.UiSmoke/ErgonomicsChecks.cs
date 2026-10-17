using System.Diagnostics;
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

internal static class ErgonomicsChecks
{
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(owner)!;
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Verify(bool condition,string message) { if(!condition)throw new Exception(message); }
    private static void Press(Window window,Key key,RawInputModifiers modifiers=RawInputModifiers.None)
    {
        var physical=Enum.TryParse<PhysicalKey>(key.ToString(),out var parsed)?parsed:PhysicalKey.None;
        var text=key==Key.Enter?"\r":""; window.KeyPress(key,modifiers,physical,text); if(window.IsVisible)window.KeyRelease(key,modifiers,physical,text);
    }
    private static void Until(Func<bool> done)
    { var clock=Stopwatch.StartNew(); while(!done()) { if(clock.Elapsed>TimeSpan.FromSeconds(25))throw new TimeoutException("Ergonomics fixture timed out"); RedesignChecks.Frames(1); } }
    internal static void Run(string root)
    {
        var preferences=DesignSystem.Preferences;
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var repo=new ProfileRepository(new AppPaths(Path.Combine(root,"ergonomics-"+language+"-"+Guid.NewGuid().ToString("N")))); RedesignChecks.Wait(repo.InitializeAsync());
            var settings=repo.ApplicationSettingsAsync().GetAwaiter().GetResult(); RedesignChecks.Wait(repo.SaveApplicationSettingsAsync(settings with { Preferences=settings.Preferences with { Language=language } }));
            var a=new ServerProfile { Name="Alpha Linux",Host="alpha.fixture.invalid",Username="demo",Tags="prod,linux",Folder="Rack" };
            var b=a with { Id=Guid.NewGuid(),Name="Beta Windows",Host="beta.fixture.invalid",Protocol=Protocol.RDP,Port=3389,Tags="prod,windows" };
            var c=a with { Id=Guid.NewGuid(),Name="Gamma long administration session with a descriptive name",Host="gamma.fixture.invalid",Folder="" };
            foreach(var profile in new[]{a,b,c})RedesignChecks.Wait(repo.SaveAsync(profile));
            var vault=new NoCredentials(); var vm=new MainViewModel(repo); var main=new MainWindow(vm,repo,vault,new SshConnectionFactory(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20);
            try
            {
                vm.Search="beta"; vm.SelectedTags=["prod"]; RedesignChecks.Frames();
                var section=(Task)typeof(MainWindow).GetMethod("SelectSectionAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(main,["transfers"])!; RedesignChecks.Wait(section); RedesignChecks.Frames();
                main.Activate(); Press(main,Key.O,RawInputModifiers.Control|RawInputModifiers.Shift); RedesignChecks.Frames(); var search=Named<TextBox>(main,"ConnectionSearch");
                Verify(search.IsKeyboardFocusWithin && search.IsEffectivelyVisible && search.Text=="beta" && vm.SelectedTags.SequenceEqual(new[]{"prod"}),"Find connection did not reveal the browser or discarded filters");
                Press(main,Key.O,RawInputModifiers.Control|RawInputModifiers.Shift); RedesignChecks.Frames(); Verify(search.IsEffectivelyVisible,"Repeated find hid the sidebar");
                search.Text="alpha"; Press(main,Key.Enter); RedesignChecks.Frames(); var tree=Field<TreeView>(main,"tree");
                Verify(tree.IsKeyboardFocusWithin && tree.SelectedItem is TreeViewItem { Tag:ServerProfile picked } && picked.Id==a.Id && main.OpenSessions.Count==0 && main.OwnedWindows.Count==0,"Fast search Enter did not focus current results or opened a session");
                Press(main,Key.Escape); RedesignChecks.Frames(); Verify(search.IsKeyboardFocusWithin && search.Text=="alpha","Tree Escape did not return to search");
                Press(main,Key.Escape); RedesignChecks.Frames(); Verify(search.Text=="" && vm.SelectedTags.Length==1,"Search Escape discarded tags or failed to clear text");
                Press(main,Key.B,RawInputModifiers.Control); RedesignChecks.Frames(); Verify(!search.IsEffectivelyVisible,"Sidebar did not hide"); Press(main,Key.O,RawInputModifiers.Control|RawInputModifiers.Shift); RedesignChecks.Frames(); Verify(search.IsKeyboardFocusWithin && search.IsEffectivelyVisible,"Find did not reopen hidden browser");

                var quick=Dialogs.QuickConnection(main); RedesignChecks.Frames(); var dialog=main.OwnedWindows.Single(); var address=Named<TextBox>(dialog,"QuickConnectionAddress");
                Verify(address.Text=="" && !Named<Button>(dialog,"QuickConnectionConnect").IsEnabled,"Quick connect has an accidental default destination");
                address.Text="ssh://demo:disposable-password@fixture.invalid"; RedesignChecks.Frames(); Verify(!Named<Button>(dialog,"QuickConnectionConnect").IsEnabled && !Named<TextBlock>(dialog,"QuickConnectionFeedback").Text!.Contains("disposable-password"),"URL password accepted or echoed in validation");
                foreach(var (uri,endpoint) in new[]{("ssh://demo@fixture.invalid:2222","fixture.invalid:2222"),("rdp://fixture.invalid","fixture.invalid:3389"),("vnc://fixture.invalid","fixture.invalid:5900"),("serial://COM999?baud=9600","COM999")})
                {
                    address.Text=uri; RedesignChecks.Frames(3); Verify(Named<Button>(dialog,"QuickConnectionConnect").IsEnabled && Named<TextBlock>(dialog,"QuickConnectionEndpoint").Text==endpoint,"Quick connection preview mismatch: "+uri);
                }
                address.Text="ssh://demo@fixture.invalid:2222"; RedesignChecks.Frames(); address.Text="not-an-address";
                Click(Named<Button>(dialog,"QuickConnectionConnect")); RedesignChecks.Frames(); Verify(dialog.IsVisible && !quick.IsCompleted,"Fast activation reused a previously valid URL");
                address.Text="rdp://fixture.invalid:3390"; RedesignChecks.Frames(); dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"quick-connect-{language}.png"));
                Click(Named<Button>(dialog,"QuickConnectionEdit")); RedesignChecks.Wait(quick); Verify(quick.Result is { EditProfile:true,Profile:{ Protocol:Protocol.RDP,Port:3390 } } && repo.ListAsync().GetAwaiter().GetResult().Count==3,"Edit as profile did not preserve parsed target or saved automatically");
                quick=Dialogs.QuickConnection(main); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single(); address=Named<TextBox>(dialog,"QuickConnectionAddress"); address.Text="ssh://demo@fixture.invalid:2222"; address.Focus(); Press(dialog,Key.Enter); RedesignChecks.Wait(quick); Verify(quick.Result is { EditProfile:false,Profile:{ Port:2222 } },"Immediate Enter did not parse the current quick-connect address");

                var panes=new[]{new SessionPane(main,a,new SshConnectionFactory(repo),new ExternalRdpClient(),repo),new SessionPane(main,b,new SshConnectionFactory(repo),new ExternalRdpClient(),repo),new SessionPane(main,c,new SshConnectionFactory(repo),new ExternalRdpClient(),repo)};
                foreach(var pane in panes)main.RegisterPane(pane,SessionDock.Primary); RedesignChecks.Frames();
                var choices=panes.Select((pane,index)=>new SessionChoice(pane,new SessionSearchEntry(pane.WorkspaceId,pane.Profile.Name,pane.Profile.Host,pane.Profile.Username,pane.Profile.Protocol.ToString(),index==1?AppText.T("Panel 2"):AppText.T("Panel 1"),Dialogs.SessionStateName(pane.VisualState)))).ToArray();
                var switching=Dialogs.Sessions(main,()=>choices,panes[0].WorkspaceId); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single(); var sessionSearch=Named<TextBox>(dialog,"SessionSwitcherSearch");
                sessionSearch.Text="no-result"; Press(dialog,Key.Enter); RedesignChecks.Frames(); Verify(dialog.IsVisible && !switching.IsCompleted && !Named<Button>(dialog,"ChooseSession").IsEnabled,"Fast Enter activated a session excluded by current text");
                sessionSearch.Text="beta"; Press(dialog,Key.Enter); RedesignChecks.Wait(switching); Verify(switching.Result==panes[1].WorkspaceId,"Fast Enter did not choose the current matching session");
                foreach(var theme in new[]{"graphite","light"})
                {
                    DesignSystem.Apply(theme); DesignSystem.ApplyPreferences(preferences with { InterfaceFontSize=16 });
                    switching=Dialogs.Sessions(main,()=>choices,panes[0].WorkspaceId); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single(); dialog.Width=500; dialog.Height=500; RedesignChecks.Frames();
                    var choose=Named<Button>(dialog,"ChooseSession"); var point=choose.TranslatePoint(default,dialog)!.Value; Verify(point.Y+choose.Bounds.Height<=dialog.ClientSize.Height,"Compact switcher clips its action");
                    dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"session-switcher-{language}-{theme}.png")); dialog.Close(); RedesignChecks.Wait(switching);
                    quick=Dialogs.QuickConnection(main); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single(); dialog.Width=460; dialog.Height=460; Named<TextBox>(dialog,"QuickConnectionAddress").Text="ssh://demo@fixture.invalid:2222"; RedesignChecks.Frames();
                    foreach(var name in new[]{"QuickConnectionEdit","QuickConnectionConnect"}) { var button=Named<Button>(dialog,name); point=button.TranslatePoint(default,dialog)!.Value; Verify(point.X>=0 && point.Y>=0 && point.X+button.Bounds.Width<=dialog.ClientSize.Width && point.Y+button.Bounds.Height<=dialog.ClientSize.Height,"Compact quick connect clips "+name); }
                    dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"quick-compact-{language}-{theme}.png")); dialog.Close(); RedesignChecks.Wait(quick);
                }
                switching=Dialogs.Sessions(main,()=>[],null); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single(); Verify(Named<TextBlock>(dialog,"SessionSwitcherEmpty").IsVisible && !Named<Button>(dialog,"ChooseSession").IsEnabled,"Empty switcher has no guidance or enabled activation"); dialog.Close(); RedesignChecks.Wait(switching);
                Verify(vault.Reads==0 && main.OpenSessions.All(p=>!p.HasAttemptedConnection),"Ergonomics checks attempted a connection or read credentials");
                Console.WriteLine("PASS "+language+": find/reveal/preserve filters; search-to-tree keyboard flow; quick URL validation/preview/paste/current-text activation/edit intent; session current-query activation/empty state; compact Graphite/Light font16; zero connections/credential reads");
            }
            finally
            {
                foreach(var owned in main.OwnedWindows.ToArray())owned.Close(); main.Close(); RedesignChecks.Frames();
                foreach(var confirm in main.OwnedWindows.ToArray())foreach(var button in confirm.GetVisualDescendants().OfType<Button>().Where(b=>b.Tag as string=="Potwierdź").ToArray())Click(button);
                Until(()=>!main.IsVisible);
            }
        }
        AppText.SetLanguage("pl"); DesignSystem.ApplyPreferences(preferences); DesignSystem.Apply("dark");
    }
    private sealed class NoCredentials : ICredentialStore
    {
        internal int Reads;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Reads++; throw new InvalidOperationException("Fixture must not read credentials"); }
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default)=>throw new InvalidOperationException("Fixture must not save credentials");
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>Task.CompletedTask;
    }
}
