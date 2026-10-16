using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class CommandPanelChecks
{
    private sealed class Vault : ICredentialStore
    {
        private readonly Dictionary<Guid,string> data=[];
        internal int Reads;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Reads++; return Task.FromResult(data.GetValueOrDefault(id)); }
        public Task SaveAsync(Guid id,string value,CancellationToken ct=default) { data[id]=value; return Task.CompletedTask; }
        public Task DeleteAsync(Guid id,CancellationToken ct=default) { data.Remove(id); return Task.CompletedTask; }
    }
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Control root,string tag)=>root.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Close(MainWindow main)
    {
        main.Close(); RedesignChecks.Frames(5); var confirmation=main.OwnedWindows.FirstOrDefault(); if(confirmation!=null)Click(confirmation,"Potwierdź"); SshAcceptanceChecks.Until(()=>!main.IsVisible,"Commands main did not close");
    }
    private static void Select(CommandPanel panel,string id)
    {
        var list=Named<ListBox>(panel,"CommandList"); list.SelectedItem=list.Items.OfType<CommandMatch>().FirstOrDefault(m=>m.Command.Id==id)??throw new Exception("Missing command "+id); RedesignChecks.Frames(3);
    }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync()); var vault=new Vault();
            Wait(repo.SaveApplicationSettingsAsync(Result(repo.ApplicationSettingsAsync()) with { Preferences=new() { Language=language } }));
            var main=new MainWindow(new(repo),repo,vault,new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20); Wait(main.OpenCommandsPanelAsync()); RedesignChecks.Frames(8);
            var panel=Named<CommandPanel>(main,"CommandsPanel"); var list=Named<ListBox>(panel,"CommandList"); if(list.ItemCount<40)throw new Exception("Offline library not loaded");
            var contextOptions=Named<Expander>(panel,"CommandContextOptions"); if(contextOptions.IsExpanded)throw new Exception("Profile settings hide command browsing by default");
            contextOptions.IsExpanded=true; RedesignChecks.Frames(3);
            Select(panel,"linux.net.listeners"); if(main.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Wstaw").IsEnabled)throw new Exception("Offline Insert enabled");
            Wait(panel.DispatchAsync("copy")); if(Result(main.Clipboard!.TryGetTextAsync())!="ss -tulpn")throw new Exception("Offline Copy failed");
            var draft=new CommandDefinition { Id="user.editor-fixture",Origin=CommandOrigin.User,Name="Custom fixture",Category="Team/Files",Template="echo {{text}}",Shell=CommandShell.Bash,Parameters=[new() { Key="text",Default="hello" }] };
            var editor=Dialogs.EditCommand(main,draft,["Team/Files"],null); RedesignChecks.Frames(5); var editorWindow=main.OwnedWindows.Single(); var template=Named<TextBox>(editorWindow,"CommandEditorTemplate");
            template.Text="echo \"{{text}}\""; Click(editorWindow,"Zapisz"); RedesignChecks.Frames(3); if(editor.IsCompleted)throw new Exception("Unsafe quoted parameter passed editor validation");
            template.Text="echo {{text}}"; editorWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,$"command-editor-{language}.png")); Click(editorWindow,"Zapisz"); if(Result(editor)?.Parameters.Length!=1)throw new Exception("Command editor did not save parameter metadata");
            var categoryEditor=Dialogs.EditCommandCategories(main,new() { Commands=[draft],Categories=["Team","Team/Files","Empty"] }); RedesignChecks.Frames(4); var categoryWindow=main.OwnedWindows.Single();
            categoryWindow.GetVisualDescendants().OfType<ListBox>().Single().SelectedItem="Team"; categoryWindow.GetVisualDescendants().OfType<TextBox>().Single().Text="Renamed"; Click(categoryWindow,"Zmień nazwę / przenieś"); Click(categoryWindow,"Zapisz");
            if(Result(categoryEditor)?.Commands.Single().Category!="Renamed/Files")throw new Exception("Category subtree move failed");
            Click(panel,"Ulubione"); RedesignChecks.Frames(5); var saved=Result(new CommandStore(repo,vault).ReadStateAsync()); if(!saved.Favorites.Contains("linux.net.listeners"))throw new Exception("Favourite did not persist");
            var mode=Named<ComboBox>(panel,"CommandMode"); mode.SelectedIndex=1; Named<ComboBox>(panel,"CommandSystem").SelectedItem="windows11"; Named<ComboBox>(panel,"CommandShell").SelectedItem=CommandShell.PowerShell; RedesignChecks.Frames(8);
            Select(panel,"windows.ps.listeners"); if(panel.Environment.Platform!=CommandPlatform.Windows)throw new Exception("Manual Windows profile failed");
            Named<TextBox>(panel,"CommandSearch").Text="otwarte porty"; RedesignChecks.Frames(15); if(list.ItemCount<1)throw new Exception("Polish search failed in "+language); Named<TextBox>(panel,"CommandSearch").Text=""; RedesignChecks.Frames(15);
            contextOptions.IsExpanded=false;
            foreach(var theme in new[]{"dark","light","graphite","nord","midnight"}) { DesignSystem.Apply(theme); RedesignChecks.Frames(8); main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"commands-{language}-{theme}.png")); }
            main.Width=920; main.Height=620; RedesignChecks.Frames(8); main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"commands-{language}-compact.png"));
            var preview=Named<TextBox>(panel,"CommandPreview"); var footer=panel.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Wstaw");
            if(preview.Bounds.Width<=0||footer.TranslatePoint(default,main) is not {} p||p.Y+footer.Bounds.Height>main.Bounds.Height)throw new Exception("Commands footer outside compact window");
            var preferences=main.CurrentPreferences;
            foreach(var font in new[]{13,16})foreach(var expanded in new[]{false,true})
            {
                main.PreviewPreferences(preferences with { InterfaceFontSize=font }); contextOptions.IsExpanded=expanded; RedesignChecks.Frames(6);
                foreach(var button in panel.GetVisualDescendants().OfType<Button>().Where(b=>b.Tag as string is "Kopiuj" or "Wstaw" or "Wykonaj" or "Ulubione" or "Edytuj" or "Duplikuj"))
                {
                    var point=button.TranslatePoint(default,main)!.Value;
                    if(point.X<0||point.Y<0||point.X+button.Bounds.Width>main.ClientSize.Width+.5||point.Y+button.Bounds.Height>main.ClientSize.Height+.5)throw new Exception("Command action clipped at font "+font);
                }
                var card=Named<Border>(panel,"CommandDetailCard");
                if(list.Bounds.Height<70 || card.Bounds.Height<85 || list.TranslatePoint(default,panel)!.Value.Y+list.Bounds.Height>card.TranslatePoint(default,panel)!.Value.Y+.5)throw new Exception("Command list/detail overlap");
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"commands-{language}-compact-font{font}-profile{expanded}.png"));
            }
            main.PreviewPreferences(preferences); contextOptions.IsExpanded=false;
            // RDP and Serial never call discovery and never obtain shell capability.
            foreach(var protocol in new[]{Protocol.RDP,Protocol.Serial})
            {
                var pane=new SessionPane(main,new() { Name="Offline "+protocol,Host="fixture.invalid",Protocol=protocol },new(repo),new ExternalRdpClient(),repo); main.RegisterPane(pane); main.FocusSession(pane); RedesignChecks.Frames(5);
                if(pane.CanAcceptCommands||Result(pane.DiscoverCommandsAsync()).Detected)throw new Exception("Non-SSH discovery capability leaked");
            }
            if(vault.Reads!=0)throw new Exception("Offline/RDP/Serial command browsing read connection credentials");
            Close(main);
        }
        AppText.SetLanguage("pl"); Console.WriteLine("PASS commands offline, PL/EN, manual Windows, copy, favourites, compact dark/light, RDP/Serial capability isolation");
    }
    internal static void Network(string root,string host,string key)
    {
        var paths=new AppPaths(Path.Combine(root,"data-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync()); var factory=new SshConnectionFactory(repo); var vault=new Vault();
        var dangerous=new CommandDefinition { Id="user.harmless-danger-fixture",Name="Danger confirmation fixture",Origin=CommandOrigin.User,Category="Fixture",Platform=CommandPlatform.Linux,Shell=CommandShell.Bash,Risk=CommandRisk.Dangerous,Template="printf SERVERDESK_COMMAND_DANGER_OK" };
        Wait(new CommandStore(repo,vault).SaveLibraryAsync(new() { Commands=[dangerous] }));
        var main=new MainWindow(new(repo),repo,vault,factory,new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20); factory.ConfirmHostKey=(_,_)=>Task.FromResult(true);
        var profile=new ServerProfile { Name="Commands fixture one",Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
        var one=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo); main.RegisterPane(one); Wait(one.ConnectAsync("serverdesk-test-passphrase",CancellationToken.None));
        var detected=Result(one.DiscoverCommandsAsync()); if(detected.Platform!=CommandPlatform.Linux||!detected.Detected)throw new Exception("Real exec-channel OS detection failed");
        if(!ReferenceEquals(detected,Result(one.DiscoverCommandsAsync())))throw new Exception("Per-session discovery cache missed");
        var terminal=one.Children.OfType<TerminalControl>().Single(); var before=SshAcceptanceChecks.Text(terminal);
        var quoteDefinition=new CommandDefinition { Id="user.quote-fixture",Origin=CommandOrigin.User,Name="Quote fixture",Category="Fixture",Shell=CommandShell.Bash,Template="printf '%s' {{value}}",Parameters=[new() { Key="value" }] };
        var quoteValue="a'b;$(2+3) text"; var rendered=CommandRenderer.Render(quoteDefinition,CommandShell.Bash,new Dictionary<string,string> { ["value"]=quoteValue });
        var transport=(SshTerminalSession)typeof(SessionPane).GetField("session",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(one)!;
        var quoted=Result(transport.ProbeAsync(rendered.Text,CancellationToken.None)); if(quoted.ExitCode!=0||quoted.Output!=quoteValue)throw new Exception("Real remote Bash did not preserve parameter as literal data");
        Wait(one.DiscoverCommandsAsync(true)); if(SshAcceptanceChecks.Text(terminal).TrimEnd()!=before.TrimEnd())throw new Exception("Discovery polluted interactive terminal");
        Wait(main.OpenCommandsPanelAsync()); RedesignChecks.Frames(8); var panel=Named<CommandPanel>(main,"CommandsPanel"); Named<Expander>(panel,"CommandContextOptions").IsExpanded=true; RedesignChecks.Frames(3); Select(panel,"general.hostname");
        RedesignChecks.Frames(5); var rowBefore=terminal.Terminal.Buffer.YBase+terminal.Terminal.Buffer.Y;
        Wait(panel.DispatchAsync("insert")); RedesignChecks.Frames(10); var after=SshAcceptanceChecks.Text(terminal); if(!after.Contains("hostname"))throw new Exception("Insert did not reach terminal");
        if(terminal.Terminal.Buffer.YBase+terminal.Terminal.Buffer.Y!=rowBefore)throw new Exception("Insert sent a newline / executed a command");
        // Discard typed input; this only affects the disposable fixture line editor.
        Wait(terminal.SendInputAsync("\u0015"));
        var two=new SessionPane(main,profile with { Id=Guid.NewGuid(),Name="Commands fixture two" },factory,new ExternalRdpClient(),repo); main.RegisterPane(two); Wait(two.ConnectAsync("serverdesk-test-passphrase",CancellationToken.None)); Wait(two.DiscoverCommandsAsync());
        var old=new CommandDispatchTicket(one.WorkspaceId,one.CommandConnectionId,"general.hostname");
        try { Wait(one.SendCommandAsync(old,new("hostname",false,false,false),CommandCatalog.Default.BuiltIns.Single(c=>c.Id=="general.hostname"),false,false,false)); throw new Exception("Stale destination accepted"); } catch(InvalidOperationException) { }
        main.FocusSession(one); Wait(panel.ResolveContextAsync()); Named<ComboBox>(panel,"CommandSection").SelectedIndex=1; Select(panel,dangerous.Id);
        var changedDuringPrompt=panel.DispatchAsync("execute"); RedesignChecks.Frames(3); var prompt=main.OwnedWindows.Single(); main.FocusSession(two); Click(prompt,"Potwierdź");
        try { Wait(changedDuringPrompt); throw new Exception("Command changed destination during confirmation"); } catch(InvalidOperationException) { }
        Wait(panel.ResolveContextAsync()); Select(panel,dangerous.Id); var execution=panel.DispatchAsync("execute"); RedesignChecks.Frames(3); Click(main.OwnedWindows.Single(),"Potwierdź"); RedesignChecks.Frames(4);
        if(execution.IsCompleted||main.OwnedWindows.Count()!=1)throw new Exception("Dangerous command skipped its second confirmation"); Click(main.OwnedWindows.Single(),"Potwierdź"); Wait(execution);
        var otherTerminal=two.Children.OfType<TerminalControl>().Single(); SshAcceptanceChecks.Until(()=>SshAcceptanceChecks.Text(otherTerminal).Contains("SERVERDESK_COMMAND_DANGER_OK"),"Explicitly confirmed command did not execute");
        // Pin manual Windows while switching between Linux sessions.
        Named<ComboBox>(panel,"CommandMode").SelectedIndex=1; Named<ComboBox>(panel,"CommandSystem").SelectedItem="windows11"; Named<ComboBox>(panel,"CommandShell").SelectedItem=CommandShell.PowerShell; Named<CheckBox>(panel,"CommandPin").IsChecked=true;
        main.FocusSession(one); RedesignChecks.Frames(8); if(panel.Environment.Platform!=CommandPlatform.Windows)throw new Exception("Pinned context followed active host");
        Named<CheckBox>(panel,"CommandPin").IsChecked=false; Named<ComboBox>(panel,"CommandMode").SelectedIndex=0; Wait(panel.ResolveContextAsync());
        Named<ComboBox>(panel,"CommandSection").SelectedIndex=0;
        Select(panel,"linux.net.report"); if(panel.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Wstaw").IsEnabled)throw new Exception("Multiline Insert enabled");
        main.FocusSession(one); var previousConnection=one.CommandConnectionId; var previousDiscovery=one.KnownCommandEnvironment;
        Wait(one.ReconnectAsync()); var nextDiscovery=Result(one.DiscoverCommandsAsync());
        if(one.CommandConnectionId==previousConnection||ReferenceEquals(previousDiscovery,nextDiscovery))throw new Exception("Reconnect reused stale command identity or discovery cache");
        try { Wait(one.SendCommandAsync(old,new("hostname",false,false,false),CommandCatalog.Default.BuiltIns.Single(c=>c.Id=="general.hostname"),false,false,false)); throw new Exception("Pre-reconnect command ticket accepted"); } catch(InvalidOperationException) { }
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"commands-ssh.png")); Close(main); Console.WriteLine("PASS commands real SSH separate exec, cache/refresh, Insert, stale destination, manual/pin, multiline guard");
    }
}
