using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Iciclecreek.Terminal;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class FourPanelChecks
{
    private static void Wait(Task task)=>RedesignChecks.Wait(task);
    private static T Result<T>(Task<T> task) { Wait(task); return task.Result; }
    private static T Named<T>(Control root,string name) where T:Control=>root.GetVisualDescendants().OfType<T>().Single(c=>c.Name==name);
    private static void Click(Window w,string tag)=>w.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string==tag).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Menu(MainWindow main,string label)
    {
        var layout=main.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Układ");
        layout.ContextMenu!.Items.OfType<MenuItem>().Single(m=>m.Header as string==AppText.T(label)).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); RedesignChecks.Frames(5);
    }
    private static TerminalControl Terminal(SessionPane pane)=>pane.Children.OfType<TerminalControl>().Single();
    private static Rect Bounds(Control control,Control relative)=>new(control.TranslatePoint(default,relative)!.Value,control.Bounds.Size);
    private static void Drag(MainWindow main,Control handle,Vector delta)
    {
        var start=Bounds(handle,main).Center; main.MouseMove(start); main.MouseDown(start,MouseButton.Left); RedesignChecks.Frames(2);
        main.MouseMove(start+delta,RawInputModifiers.LeftMouseButton); RedesignChecks.Frames(3); main.MouseUp(start+delta,MouseButton.Left); RedesignChecks.Frames(5);
    }
    private static void Layout(MainWindow main,int count)
    {
        RedesignChecks.Frames(8); var visible=main.OpenSessions.Where(p=>p.IsEffectivelyVisible && main.Workspace.Location(p.WorkspaceId)!=SessionDock.Floating).ToArray();
        if(visible.Length!=count)throw new Exception($"Expected {count} visible sessions, got {visible.Length}");
        var area=(Grid)visible[0].Parent!;
        foreach(var pane in visible)
        {
            var bounds=Bounds(pane,area);
            if(bounds.Width<180 || bounds.Height<80 || bounds.X<0 || bounds.Y<0 || bounds.Right>area.Bounds.Width+1 || bounds.Bottom>area.Bounds.Height+1 || !pane.IsActive)throw new Exception("Panel outside workspace or inactive");
            if(Terminal(pane).IsEffectivelyVisible && Terminal(pane).Bounds.Height<=0)throw new Exception("Terminal has no space inside its panel");
            var dock=main.Workspace.Location(pane.WorkspaceId);
            if(main.DockTargetAt(bounds.Center)!=dock)throw new Exception("Drop hit target does not match panel geometry");
            foreach(var other in visible.Where(p=>p!=pane))if(bounds.Intersects(Bounds(other,area)))throw new Exception("Workspace panes overlap");
        }
    }
    private static void Close(MainWindow main)
    {
        main.Close(); RedesignChecks.Frames(5);
        var confirm=main.OwnedWindows.FirstOrDefault(w=>w.GetVisualDescendants().OfType<Button>().Any(b=>b.Tag as string=="Potwierdź")); if(confirm!=null)Click(confirm,"Potwierdź");
        SshAcceptanceChecks.Until(()=>!main.IsVisible,"Main window did not finish closing"); RedesignChecks.Frames(5);
    }
    private sealed class NoReadStore : ICredentialStore
    {
        internal int Reads;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Reads++; throw new Exception("Layout restoration must not read credentials"); }
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>throw new NotSupportedException();
    }
    internal static void Run(string root)
    {
        foreach(var language in new[]{"pl","en"})
        {
            AppText.SetLanguage(language); var paths=new AppPaths(Path.Combine(root,language)); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync());
            var settings=Result(repo.ApplicationSettingsAsync()) with { Preferences=new() { Language=language,ShowMetrics=false } }; Wait(repo.SaveApplicationSettingsAsync(settings));
            var store=new NoReadStore(); MainWindow Create()=>new(new(repo),repo,store,new SshConnectionFactory(repo),new ExternalRdpClient());
            var profiles=Enumerable.Range(1,4).Select(i=>new ServerProfile { Name="Terminal "+i,Host="fixture.invalid",Username="fixture" }).ToArray(); foreach(var profile in profiles)Wait(repo.SaveAsync(profile));
            var main=Create(); main.Show(); RedesignChecks.Frames(20);
            var panes=profiles.Select(p=>new SessionPane(main,p,new SshConnectionFactory(repo),new ExternalRdpClient(),repo)).ToArray();
            for(var i=0;i<3;i++)main.RegisterPane(panes[i]); Menu(main,"Trzy panele (1 + 2)"); Layout(main,3);
            var parent=panes[0].Parent; var emulators=panes.Take(3).Select(p=>Terminal(p).Terminal).ToArray();
            Menu(main,"Cztery panele (2 × 2)"); Layout(main,3);
            Named<Button>(main,"DockNumber4").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); main.RegisterPane(panes[3]); Layout(main,4);
            if(main.Workspace.Location(panes[3].WorkspaceId)!=SessionDock.Quaternary)throw new Exception("Selecting empty panel did not direct new tab there");
            foreach(var pane in panes)Terminal(pane).Terminal.Write("PANEL_BUFFER_żółć\r\n");
            foreach(var theme in new[]{"graphite","light"})foreach(var width in new[]{900,1320,1920})
            {
                main.PreviewPreferences(main.CurrentPreferences with { InterfaceFontSize=16,Density=InterfaceDensity.Comfortable }); DesignSystem.Apply(theme); main.Width=width; main.Height=width==900?600:900;
                Layout(main,4); main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"grid-{language}-{theme}-{width}.png"));
            }
            main.SetRenderScaling(1.5); Layout(main,4); main.CaptureRenderedFrame()!.Save(Path.Combine(root,$"grid-{language}-dpi150.png")); main.SetRenderScaling(1);
            main.PreviewPreferences(main.CurrentPreferences with { TabStripHeight=30 }); RedesignChecks.Frames(5);
            foreach(var tab in main.GetVisualDescendants().OfType<TabItem>().Where(t=>t.Classes.Contains("session-tab") && t.IsEffectivelyVisible))
                foreach(var close in tab.GetVisualDescendants().OfType<Button>())
                { var bounds=Bounds(close,Named<TabControl>(main,"DockTabs"+(WorkspaceState.DockedGroups.ToList().IndexOf(main.Workspace.Location(((SessionPane)tab.Tag!).WorkspaceId))+1))); if(bounds.Top<0 || bounds.Bottom>30.5)throw new Exception("Compact tab strip clips its close action"); }
            main.PreviewPreferences(main.CurrentPreferences with { TabStripHeight=35 });
            var edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); var w=main.OwnedWindows.Single(); Named<TabControl>(w,"SettingsCategories").SelectedIndex=3; RedesignChecks.Frames();
            Named<NumericUpDown>(w,"TabStripHeight").Value=48; Named<NumericUpDown>(w,"PaneSeparatorSize").Value=12; Named<ComboBox>(w,"NewTabTarget").SelectedIndex=2; Named<ComboBox>(w,"WorkspaceOnStartup").SelectedIndex=4;
            var numbers=w.GetVisualDescendants().OfType<CheckBox>().Single(c=>AutomationProperties.GetName(c)==AppText.T("Pokaż numery paneli w podzielonym widoku")); numbers.IsChecked=false; RedesignChecks.Frames();
            if(Named<Button>(main,"DockNumber1").IsVisible || Named<TabControl>(main,"DockTabs4").Height!=48 || main.Workspace.Mode!=WorkspaceMode.Grid)throw new Exception("Workspace preference preview ignored sizes or changed startup layout immediately");
            Click(w,"Anuluj"); Wait(edit);
            if(Named<TabControl>(main,"DockTabs4").Height!=35 || !Named<Button>(main,"DockNumber1").IsVisible || Result(repo.ApplicationSettingsAsync())!=settings)throw new Exception("Workspace preference cancel persisted changes");
            edit=Dialogs.Settings(main,repo,()=>Task.CompletedTask); RedesignChecks.Frames(); w=main.OwnedWindows.Single(); Named<TextBox>(w,"SettingsSearch").Text=AppText.T("Wysokość paska kart"); RedesignChecks.Frames();
            if(Named<TabControl>(w,"SettingsCategories").SelectedIndex!=3)throw new Exception("Workspace preferences not searchable");
            Named<TextBox>(w,"SettingsSearch").Text=""; Named<ComboBox>(w,"NewTabTarget").SelectedIndex=2; Named<ComboBox>(w,"WorkspaceOnStartup").SelectedIndex=5; Named<NumericUpDown>(w,"PaneSeparatorSize").Value=8; Click(w,"Zapisz"); Wait(edit);
            var savedPreferences=Result(repo.PreferencesAsync()); if(savedPreferences.NewTabTarget!=NewTabPlacement.LeastPopulated || savedPreferences.WorkspaceOnStartup!=WorkspaceStartup.Grid || savedPreferences.PaneSeparatorSize!=8)throw new Exception("Workspace preferences not saved");
            var extra=new SessionPane(main,profiles[0],new SshConnectionFactory(repo),new ExternalRdpClient(),repo); main.RegisterPane(extra,SessionDock.Tertiary); main.Reorder(extra,-1);
            if(main.Workspace.Tertiary[0]!=extra.WorkspaceId)throw new Exception("Reordering third group failed");
            main.KeyPress(Key.Tab,RawInputModifiers.Control,PhysicalKey.Tab,"\t"); main.KeyRelease(Key.Tab,RawInputModifiers.Control,PhysicalKey.Tab,"\t"); RedesignChecks.Frames();
            if(main.Workspace.Focused!=main.Workspace.Tertiary[1])throw new Exception("Ctrl+Tab escaped third group");
            main.RemovePane(extra); var detached=panes.Single(p=>main.Workspace.Location(p.WorkspaceId)==SessionDock.Tertiary); main.DockSession(detached,SessionDock.Floating); RedesignChecks.Frames(); var floating=main.OwnedWindows.Single();
            floating.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Dokuj do panelu 3").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(main,4);
            for(var i=0;i<3;i++)if(!ReferenceEquals(parent,panes[i].Parent) || !ReferenceEquals(emulators[i],Terminal(panes[i]).Terminal) || !SshAcceptanceChecks.Text(Terminal(panes[i])).Contains("PANEL_BUFFER_żółć"))throw new Exception("Docking replaced terminal/buffer/parent");
            var oldRow=main.Workspace.RowRatio; Drag(main,Named<Border>(main,"DockRowSeparator"),new Vector(0,30));
            if(main.Workspace.RowRatio<=oldRow || main.GetVisualDescendants().OfType<TabControl>().Where(t=>t.Name?.StartsWith("DockTabs")==true).Any(t=>t.Height!=35))throw new Exception("Horizontal drag resized tab headers or failed to persist the row axis");
            var oldColumn=main.Workspace.Ratio; Drag(main,Named<GridSplitter>(main,"DockColumnSeparator"),new Vector(30,0)); if(main.Workspace.Ratio<=oldColumn)throw new Exception("Vertical drag failed to persist column axis");
            var rowHandle=Named<Border>(main,"DockRowSeparator"); rowHandle.Focus(); SshAcceptanceChecks.Press(main,Key.Home); if(main.Workspace.RowRatio!=.5)throw new Exception("Separator keyboard equalize failed");
            main.Workspace.SetRatio(.61); main.Workspace.SetRowRatio(.39); main.SetWorkspaceMode(WorkspaceMode.Grid); Layout(main,4);
            Wait(main.SaveSessionSnapshotAsync()); var snapshot=WorkspaceSnapshot.Parse(Result(repo.SettingAsync("lastWorkspace"))!);
            if(snapshot.Sessions.Count!=4 || snapshot.SelectedTertiary==null || snapshot.SelectedQuaternary==null || snapshot.RowRatio!=.39)throw new Exception("Four-panel snapshot incomplete");
            Close(main); var restored=Create(); restored.Show(); RedesignChecks.Frames(25); Layout(restored,4);
            if(store.Reads!=0 || restored.OpenSessions.Any(p=>p.IsConnected || p.HasAttemptedConnection) || restored.Workspace.Mode!=WorkspaceMode.Grid || Math.Abs(restored.Workspace.Ratio-.61)>.001 || Math.Abs(restored.Workspace.RowRatio-.39)>.001)throw new Exception("Restore lost axes/panels or connected automatically");
            // Policy chooses an empty visible group, while first-panel and focused policies remain available.
            var emptyPane=restored.OpenSessions.Single(p=>restored.Workspace.Location(p.WorkspaceId)==SessionDock.Quaternary); restored.RemovePane(emptyPane);
            var added=new SessionPane(restored,profiles[3],new SshConnectionFactory(repo),new ExternalRdpClient(),repo); restored.RegisterPane(added); Layout(restored,4); if(restored.Workspace.Location(added.WorkspaceId)!=SessionDock.Quaternary)throw new Exception("Least-populated policy ignored empty fourth group");
            restored.PreviewPreferences(savedPreferences with { NewTabTarget=NewTabPlacement.FirstPanel }); var first=new SessionPane(restored,profiles[0],new SshConnectionFactory(repo),new ExternalRdpClient(),repo); restored.RegisterPane(first); if(restored.Workspace.Location(first.WorkspaceId)!=SessionDock.Primary)throw new Exception("First-panel policy ignored"); restored.RemovePane(first);
            Menu(restored,"Wyrównaj wielkość paneli"); if(restored.Workspace.Ratio!=.5 || restored.Workspace.RowRatio!=.5)throw new Exception("Equalize did not reset both axes");
            Menu(restored,"Trzy panele (1 + 2)"); Layout(restored,3); Menu(restored,"Jeden panel"); Layout(restored,1); if(restored.Workspace.Primary.Count!=4 || restored.OpenSessions.Count!=4)throw new Exception("Merging lost cards"); Close(restored);
            Wait(repo.SaveApplicationSettingsAsync(settings with { Preferences=settings.Preferences with { WorkspaceOnStartup=WorkspaceStartup.Three } }));
            var startup=Create(); startup.Show(); RedesignChecks.Frames(25); Layout(startup,3); if(startup.Workspace.Mode!=WorkspaceMode.Three || startup.OpenSessions.Count!=4 || store.Reads!=0)throw new Exception("Explicit startup layout did not override saved single layout safely"); Close(startup);
        }
        AppText.SetLanguage("pl"); DesignSystem.Apply("dark");
        Console.WriteLine("PASS four-panel workspace PL/EN: 3/4 visible terminals, menu and hit targets, 900/1320/1920px font16 and 150% rendering, live settings/cancel/search/save, independent groups/Ctrl+Tab/reorder, floating/redock buffer continuity, both split axes, snapshot/forced startup without credential reads, tab-placement policies and safe merge");
    }
    internal static void Network(string root,string host,string key)
    {
        var paths=new AppPaths(Path.Combine(root,"data")); var repo=new ProfileRepository(paths); Wait(repo.InitializeAsync()); Wait(repo.SaveApplicationSettingsAsync(Result(repo.ApplicationSettingsAsync()) with { Preferences=new() { ShowMetrics=false } }));
        var factory=new SshConnectionFactory(repo); var main=new MainWindow(new(repo),repo,new CredentialStore(paths),factory,new ExternalRdpClient()); main.Show(); RedesignChecks.Frames(20); factory.ConfirmHostKey=(_,_)=>Task.FromResult(true);
        var panes=new List<SessionPane>();
        try
        {
            for(var i=0;i<4;i++)
            {
                var profile=new ServerProfile { Name="Live terminal "+(i+1),Host=host,Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(key) };
                var pane=new SessionPane(main,profile,factory,new ExternalRdpClient(),repo); panes.Add(pane); main.RegisterPane(pane,SessionDock.Primary); Wait(pane.ConnectAsync("serverdesk-test-passphrase",default)); Wait(Terminal(pane).SendInputAsync($"printf 'GRID_SESSION_{i} pid=%s\\n' $$\r",default));
            }
            RedesignChecks.Frames(25); var ids=panes.Select(p=>Terminal(p).SessionId).ToArray(); var emulators=panes.Select(p=>Terminal(p).Terminal).ToArray();
            void Continuous()
            {
                for(var i=0;i<panes.Count;i++)if(!panes[i].IsConnected || Terminal(panes[i]).SessionId!=ids[i] || !ReferenceEquals(emulators[i],Terminal(panes[i]).Terminal) || !SshAcceptanceChecks.Text(Terminal(panes[i])).Contains($"GRID_SESSION_{i}"))throw new Exception("Layout interrupted live SSH session/buffer");
            }
            main.SetWorkspaceMode(WorkspaceMode.Grid); Layout(main,4); Continuous(); main.CaptureRenderedFrame()!.Save(Path.Combine(root,"live-four-terminals.png"));
            main.SetWorkspaceMode(WorkspaceMode.Three); Layout(main,3); Continuous(); main.CaptureRenderedFrame()!.Save(Path.Combine(root,"live-three-terminals.png"));
            main.SetWorkspaceMode(WorkspaceMode.Grid); Layout(main,4); main.PreviewPreferences(main.CurrentPreferences with { TabStripHeight=48,PaneSeparatorSize=12 }); Layout(main,4); Continuous();
            Wait(panes[3].ShowAsync(1)); RedesignChecks.Frames(); Wait(panes[3].ShowAsync(0)); main.DockSession(panes[2],SessionDock.Floating); RedesignChecks.Frames(10); main.DockSession(panes[2],SessionDock.Tertiary); Layout(main,4); Continuous();
            for(var i=0;i<4;i++)Wait(Terminal(panes[i]).SendInputAsync($"printf 'GRID_AFTER_{i} pid=%s\\n' $$\r",default));
            SshAcceptanceChecks.Until(()=>panes.Select((p,i)=>SshAcceptanceChecks.Text(Terminal(p)).Contains($"GRID_AFTER_{i}")).All(x=>x),"SSH input after docking did not complete");
            foreach(var pane in panes) { Wait(pane.CloseAsync()); main.RemovePane(pane); } main.Close(); RedesignChecks.Frames(10);
            Console.WriteLine("PASS four independent real SSH terminals: 3/4 layouts, resize/preferences, SFTP switch, floating/redock, retained emulator/session/output and independent input after layout; drained all four transports");
        }
        finally { foreach(var pane in panes.Where(main.OpenSessions.Contains).ToArray()) { Wait(pane.CloseAsync()); main.RemovePane(pane); } if(main.IsVisible)Close(main); }
    }
}
