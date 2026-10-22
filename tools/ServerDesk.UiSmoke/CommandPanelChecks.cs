using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static partial class CommandPanelChecks
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
        var list=Named<ListBox>(panel,"CommandList"); list.SelectedItem=list.Items.OfType<CommandMatch>().FirstOrDefault(m=>m.Command.Id==id)??throw new Exception("Missing command "+id); panel.ShowCommandDetails(); RedesignChecks.Frames(3);
    }
    internal static void ProfileLayout(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync()); var vault=new Vault();
            Wait(repo.SaveApplicationSettingsAsync(Result(repo.ApplicationSettingsAsync()) with { Preferences=new() { Language=language } }));
            var main=new MainWindow(new(repo),repo,vault,new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(12); Wait(main.OpenCommandsPanelAsync()); RedesignChecks.Frames(5);
            var panel=Named<CommandPanel>(main,"CommandsPanel"); var options=Named<Expander>(panel,"CommandContextOptions"); options.IsExpanded=true; RedesignChecks.Frames(3);
            Named<ComboBox>(panel,"CommandMode").SelectedIndex=1;
            var system=Named<ComboBox>(panel,"CommandSystem"); system.SelectedItem="rhel";
            var shell=Named<ComboBox>(panel,"CommandShell"); shell.SelectedItem=CommandShell.Bash;
            var version=Named<TextBox>(panel,"CommandVersion"); version.Text="9.4";
            var fields=Named<ScrollViewer>(panel,"CommandFilters"); var list=Named<ListBox>(panel,"CommandList"); var preferences=main.CurrentPreferences;
            foreach(var size in new[]{new Size(1280,800),new Size(920,620)})foreach(var font in new[]{13,16})
            {
                main.Width=size.Width; main.Height=size.Height; main.PreviewPreferences(preferences with { InterfaceFontSize=font }); fields.Offset=default; RedesignChecks.Frames(8);
                var point=system.TranslatePoint(default,fields)!.Value;
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"commands-profile-{language}-{size.Width}-font{font}.png"));
                if(point.Y<0 || point.Y+system.Bounds.Height>fields.Bounds.Height+.5 || system.Bounds.Width<fields.Bounds.Width-20)throw new Exception($"System selector lacks a full visible row: {language}/{size}/{font}; system={system.Bounds}, viewport={fields.Bounds}, point={point}");
                var label=system.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Text=="Red Hat Enterprise Linux");
                if(label.Bounds.Width+1<label.TextLayout.WidthIncludingTrailingWhitespace)throw new Exception("Long system name is clipped");
                if(!list.IsEffectivelyVisible || list.Bounds.Height<70 || list.TranslatePoint(default,panel)!.Value.Y+list.Bounds.Height>panel.Bounds.Height+.5)throw new Exception("Profile editing pushes the command list offscreen");
                system.IsDropDownOpen=true; RedesignChecks.Frames(3);
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"commands-system-{language}-{size.Width}-font{font}.png"));
                system.IsDropDownOpen=false;
                version.Focus(); RedesignChecks.Frames(4);
                var versionPoint=version.TranslatePoint(default,fields)!.Value;
                if(versionPoint.Y<-.5 || versionPoint.Y+version.Bounds.Height>fields.Bounds.Height+.5)throw new Exception("Profile version cannot scroll into view");
            }
            fields.Offset=default; options.IsExpanded=false; RedesignChecks.Frames(4); options.IsExpanded=true; RedesignChecks.Frames(4);
            if(system.SelectedItem as string!="rhel" || shell.SelectedItem is not CommandShell.Bash || version.Text!="9.4")throw new Exception("Profile layout changes lost preferences");
            Select(panel,"general.hostname"); Wait(panel.DispatchAsync("copy")); Click(panel,"Lista komend"); RedesignChecks.Frames(5);
            var listPoint=list.TranslatePoint(default,panel)!.Value;
            if(list.Bounds.Height<70 || listPoint.Y+list.Bounds.Height>panel.Bounds.Height+.5)throw new Exception("Operation notice pushes command results outside the panel");
            system.Focus(); RedesignChecks.Frames(3);
            var systemPoint=system.TranslatePoint(default,fields)!.Value;
            if(systemPoint.Y<-.5 || systemPoint.Y+system.Bounds.Height>fields.Bounds.Height+.5)throw new Exception("System selector cannot receive visible focus with an operation notice");
            Wait(panel.Shutdown);
            var saved=Result(new CommandStore(repo,vault).ReadStateAsync()).Preferences;
            if(saved.Automatic || saved.System!="rhel" || saved.Shell!=CommandShell.Bash || saved.Version!="9.4" || vault.Reads!=0)throw new Exception("Profile selection did not persist independently of credentials");
            Close(main);
        }
        AppText.SetLanguage("pl"); Console.WriteLine("PASS system profile: full-width long OS names, dropdown, scrollable fields, visible command results, resize/font changes and preference retention; PL/EN 1280x800/920x620 font13/16, no remote sessions");
    }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language+"-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync()); var vault=new Vault();
            Wait(repo.SaveApplicationSettingsAsync(Result(repo.ApplicationSettingsAsync()) with { Preferences=new() { Language=language } }));
            var main=new MainWindow(new(repo),repo,vault,new(repo),new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20); Wait(main.OpenCommandsPanelAsync()); RedesignChecks.Frames(8);
            var panel=Named<CommandPanel>(main,"CommandsPanel"); var list=Named<ListBox>(panel,"CommandList"); if(list.ItemCount<40)throw new Exception("Offline library not loaded");
            var riskFilter=Named<ComboBox>(panel,"CommandRiskFilter");
            foreach(var index in new[]{1,2,3})
            {
                riskFilter.SelectedIndex=index; RedesignChecks.Frames(3);
                if(list.ItemCount==0 || list.Items.OfType<CommandMatch>().Any(m=>m.Command.Risk!=(CommandRisk)(index-1)))throw new Exception("Risk filter mixes command levels");
            }
            var query=Named<TextBox>(panel,"CommandSearch"); query.Text="qwerty-no-command-fixture"; panel.RefreshList(); RedesignChecks.Frames(3);
            if(!Named<ScrollViewer>(panel,"CommandEmptyState").IsEffectivelyVisible || list.IsEffectivelyVisible)throw new Exception("Missing command search recovery state");
            var environment=panel.Environment; Named<Button>(panel,"CommandEmptyAction").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(3);
            if(query.Text!="" || riskFilter.SelectedIndex!=0 || !query.IsKeyboardFocusWithin || panel.Environment!=environment || list.ItemCount<40)throw new Exception("Command filter reset lost profile, focus or results");
            var commandSection=Named<ComboBox>(panel,"CommandSection"); commandSection.SelectedIndex=1; RedesignChecks.Frames(3);
            Named<Button>(panel,"CommandEmptyAction").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(4);
            if(main.OwnedWindows.Count()!=1)throw new Exception("Empty My commands did not open its editor"); Click(main.OwnedWindows.Single(),"Anuluj");
            Wait((Task)typeof(CommandPanel).GetField("actionWork",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(panel)!);
            commandSection.SelectedIndex=2; RedesignChecks.Frames(3); Named<Button>(panel,"CommandEmptyAction").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(3);
            if(commandSection.SelectedIndex!=0)throw new Exception("Empty Favorites did not lead to the reference");
            commandSection.SelectedIndex=3; RedesignChecks.Frames(3);
            if(AutomationProperties.GetName(Named<Button>(panel,"CommandEmptyAction"))!=AppText.T("Włącz historię komend"))throw new Exception("Disabled history lacks an opt-in action");
            main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"commands-history-empty-{language}.png"));
            Named<Button>(panel,"CommandEmptyAction").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Wait((Task)typeof(CommandPanel).GetField("actionWork",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(panel)!);
            if(!Result(new CommandStore(repo,vault).ReadStateAsync()).Preferences.RememberRecent)throw new Exception("History empty-state opt-in failed");
            commandSection.SelectedIndex=0; panel.FocusSearch(); query.Text="otwarte porty";
            main.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r"); main.KeyRelease(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r"); RedesignChecks.Frames(3);
            if(!Named<Border>(panel,"CommandDetailCard").IsVisible || main.OpenSessions.Count!=0 || Named<TextBlock>(panel,"CommandDispatchHint").Text!=AppText.T("Kopiowanie dostępne. Wstawianie i wykonanie wymagają aktywnego terminala SSH."))throw new Exception("Immediate Enter did not expose offline details without dispatch");
            main.KeyPress(Key.F,RawInputModifiers.Control,PhysicalKey.F,"f"); main.KeyRelease(Key.F,RawInputModifiers.Control,PhysicalKey.F,"f"); RedesignChecks.Frames(3);
            if(!query.IsKeyboardFocusWithin || !list.IsEffectivelyVisible)throw new Exception("Ctrl+F did not restore command search");
            query.Text="systemctl start"; main.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r"); main.KeyRelease(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r");
            var service=Named<TextBox>(panel,"CommandParameter_service"); service.Text="fixture-service"; RedesignChecks.Frames(14);
            if(Named<TextBox>(panel,"CommandParameter_service").Text!="fixture-service")throw new Exception("Deferred search erased the parameter draft after immediate Enter");
            main.KeyPress(Key.F,RawInputModifiers.Control,PhysicalKey.F,"f"); main.KeyRelease(Key.F,RawInputModifiers.Control,PhysicalKey.F,"f"); RedesignChecks.Frames(3);
            main.KeyPress(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,""); main.KeyRelease(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,""); RedesignChecks.Frames(3);
            if(query.Text!="" || !list.IsEffectivelyVisible || !main.IsVisible)throw new Exception("Escape did not clear command search");
            if(Named<Border>(panel,"CommandDetailCard").IsVisible || Named<Grid>(panel,"CommandActions").IsVisible)throw new Exception("Command details/actions show before a card is opened");
            main.Activate(); list.Focus(); RedesignChecks.Frames(8); main.UpdateLayout();
            main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"commands-cards-{language}.png"));
            // Previous navigation can retain an offset in the virtualized list.
            // Exercise an actual visible card rather than assuming item zero is realized.
            var firstCard=list.GetVisualDescendants().OfType<Border>().First(b=>b.Name=="CommandCard" && b.TranslatePoint(new Point(20,15),list) is {} point && point.Y>=0 && point.Y<list.Bounds.Height);
            var first=list.Items.OfType<CommandMatch>().Single(m=>m.Command.Id==firstCard.Tag as string);
            if(firstCard.GetVisualDescendants().OfType<TextBlock>().Single(t=>t.Name=="CommandCardTemplate").Text!=first.Command.Template)throw new Exception("Card does not show its command template");
            var cardPoint=firstCard.TranslatePoint(new Point(20,15),main)!.Value; var hit=main.InputHitTest(cardPoint) as Control;
            main.MouseMove(cardPoint,RawInputModifiers.None); var pointerTimer=System.Diagnostics.Stopwatch.StartNew();
            main.MouseDown(cardPoint,MouseButton.Left,RawInputModifiers.None);
            var pointAfterDown=firstCard.TranslatePoint(new Point(20,15),main); var afterDown=main.InputHitTest(cardPoint) as Control; var pressTime=pointerTimer.ElapsedMilliseconds;
            main.MouseUp(cardPoint,MouseButton.Left,RawInputModifiers.None); RedesignChecks.Frames(4);
            if(!Named<Border>(panel,"CommandDetailCard").IsVisible || list.IsEffectivelyVisible || !Named<ScrollViewer>(panel,"CommandDetails").GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text==first.Command.Title))
            {
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"commands-pointer-failure-{language}.png"));
                throw new Exception($"Pointer click did not open selected details: {language}/{first.Command.Id}; point={cardPoint}, afterDown={pointAfterDown}, pressMs={pressTime}, hit={hit?.GetType().Name}/{hit?.Name}, afterHit={afterDown?.Name}, cardAttached={TopLevel.GetTopLevel(firstCard)!=null}, active={main.IsActive}, enabled={main.IsEnabled}, visible={Named<Border>(panel,"CommandDetailCard").IsVisible}, selected={(list.SelectedItem as CommandMatch)?.Command.Id}");
            }
            Click(panel,"Lista komend"); RedesignChecks.Frames(4);
            if(!list.IsEffectivelyVisible || list.SelectedItem is not CommandMatch kept || kept.Command.Id!=first.Command.Id)throw new Exception("Back did not preserve the command browser selection");
            if(!list.Focus())throw new Exception("Command list cannot receive keyboard focus after Back");
            main.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r"); RedesignChecks.Frames(3);
            if(!Named<Border>(panel,"CommandDetailCard").IsVisible)throw new Exception("Enter did not open selected command details");
            main.KeyPress(Key.Escape,RawInputModifiers.None,PhysicalKey.Escape,""); RedesignChecks.Frames(3);
            if(!list.IsEffectivelyVisible)throw new Exception("Escape did not return to the command list");
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
            // Apply the query explicitly; this check covers navigation and filter retention.
            panel.FocusSearch(); Named<TextBox>(panel,"CommandSearch").Text="otwarte porty"; panel.RefreshList(); RedesignChecks.Frames(3);
            if(list.ItemCount<1)throw new Exception("Command query returned no matching listeners");
            var filteredCount=list.ItemCount; Select(panel,"windows.ps.listeners"); Click(panel,"Lista komend"); RedesignChecks.Frames(3);
            if(Named<TextBox>(panel,"CommandSearch").Text!="otwarte porty" || list.ItemCount!=filteredCount)throw new Exception("Back lost the command search filter");
            Named<TextBox>(panel,"CommandSearch").Text=""; panel.RefreshList(); RedesignChecks.Frames(3);
            contextOptions.IsExpanded=false;
            foreach(var theme in new[]{"dark","light","graphite","nord","midnight"}) { DesignSystem.Apply(theme); panel.FocusSearch(); RedesignChecks.Frames(4); main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"commands-cards-{language}-{theme}.png")); Select(panel,"windows.ps.listeners"); main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"commands-{language}-{theme}.png")); }
            main.Width=920; main.Height=620; RedesignChecks.Frames(8); main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"commands-{language}-compact.png"));
            var preview=Named<TextBox>(panel,"CommandPreview"); var footer=panel.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Wstaw");
            if(preview.Bounds.Width<=0||footer.TranslatePoint(default,main) is not {} p||p.Y+footer.Bounds.Height>main.Bounds.Height)throw new Exception("Commands footer outside compact window");
            var preferences=main.CurrentPreferences;
            foreach(var font in new[]{13,16})foreach(var expanded in new[]{false,true})
            {
                main.PreviewPreferences(preferences with { InterfaceFontSize=font }); panel.FocusSearch(); contextOptions.IsExpanded=expanded; RedesignChecks.Frames(6);
                var searchPoint=query.TranslatePoint(default,panel)!.Value;
                if(!query.IsEffectivelyVisible || searchPoint.Y<0 || searchPoint.Y+query.Bounds.Height>panel.Bounds.Height || query.GetVisualAncestors().OfType<ScrollViewer>().Any(s=>s.Name=="CommandFilters"))throw new Exception("Expanded system profile scrolls the search away");
                if(list.Bounds.Height<70 || !list.IsEffectivelyVisible)throw new Exception("Command cards lost their compact browser viewport");
                main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"commands-cards-{language}-compact-font{font}-profile{expanded}.png")); Select(panel,"windows.ps.listeners");
                foreach(var button in panel.GetVisualDescendants().OfType<Button>().Where(b=>b.Tag as string is "Kopiuj" or "Wstaw" or "Wykonaj" or "Ulubione" or "Edytuj" or "Duplikuj"))
                {
                    var point=button.TranslatePoint(default,main)!.Value;
                    if(point.X<0||point.Y<0||point.X+button.Bounds.Width>main.ClientSize.Width+.5||point.Y+button.Bounds.Height>main.ClientSize.Height+.5)throw new Exception("Command action clipped at font "+font);
                }
                var card=Named<Border>(panel,"CommandDetailCard");
                if(list.IsEffectivelyVisible || card.Bounds.Height<85 || !Named<Button>(panel,"CommandBackToList").IsEffectivelyVisible)throw new Exception("Command browser overlaps full details or hides Back");
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
        AppText.SetLanguage("pl"); Console.WriteLine("PASS command cards/template/category/risk, risk filtering and empty-state recovery/create/history opt-in, immediate search Enter/Ctrl+F/Escape, sticky search above profile, visible offline action guidance, pointer/details/back, copy/favourites, PL/EN compact font13/16, RDP/Serial isolation");
        Navigation(Path.Combine(root,"navigation"));
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
        foreach(var view in new[]{1,2,1,0,2,1})
        {
            Wait(terminal.SendInputAsync("\u0015"));
            SshAcceptanceChecks.Until(()=>!CurrentLine(terminal).Contains("hostname"),"Fixture did not clear pending input");
            Wait(one.ShowAsync(view)); RedesignChecks.Frames(3);
            Select(panel,"general.hostname");
            rowBefore=terminal.Terminal.Buffer.YBase+terminal.Terminal.Buffer.Y;
            Wait(panel.DispatchAsync("insert")); RedesignChecks.Frames(6);
            if(!terminal.IsVisible || one.SelectedPanel!=0 || !terminal.IsKeyboardFocusWithin)
                throw new Exception("Insert from SFTP/monitoring did not show and focus the terminal");
            SshAcceptanceChecks.Until(()=>CurrentLine(terminal).Contains("hostname"),"Repeated Insert did not reach the current input line");
            if(terminal.Terminal.Buffer.YBase+terminal.Terminal.Buffer.Y!=rowBefore)
                throw new Exception("Repeated Insert was lost or submitted a command");
        }
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
        // A command waiting for a panel transition must not follow a later tab change.
        var gate=(SemaphoreSlim)typeof(SessionPane).GetField("connectionGate",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(two)!;
        Wait(gate.WaitAsync()); Task pending;
        try
        {
            var ticket=new CommandDispatchTicket(two.WorkspaceId,two.CommandConnectionId,"general.hostname");
            pending=two.SendCommandAsync(ticket,new("hostname",false,false,false),CommandCatalog.Default.BuiltIns.Single(c=>c.Id=="general.hostname"),false,false,false);
            if(pending.IsCompleted)throw new Exception("Command fixture did not wait for the panel transition");
            main.FocusSession(one);
        }
        finally { gate.Release(); }
        try { Wait(pending); throw new Exception("Command waiting for a panel transition accepted a stale destination"); } catch(InvalidOperationException) { }
        if(CurrentLine(otherTerminal).Contains("hostname"))throw new Exception("Rejected command still reached the old terminal");
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
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"commands-ssh.png")); Close(main); Console.WriteLine("PASS commands real SSH separate exec, cache/refresh, repeated Insert from terminal/SFTP/monitoring without Enter, focus, stale destination/panel wait, manual/pin, multiline guard");
        static string CurrentLine(TerminalControl control)=>control.Terminal.Buffer.Lines[control.Terminal.Buffer.YBase+control.Terminal.Buffer.Y]?.TranslateToString(true)??"";
    }
}
