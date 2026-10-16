using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Iciclecreek.Terminal;

internal static class SessionWorkflowChecks
{
    internal static void Run(string root)
    {
        var paths=new AppPaths(Path.Combine(root,"resume-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); RedesignChecks.Wait(repo.InitializeAsync()); var store=new CountingStore();
        var a=new ServerProfile { Name="Operations",Host="saved-server.invalid",Username="admin",PrivateKeyPath="C:/private/not-in-snapshot.key",Color="#69CFB6" }; var b=a with { Id=Guid.NewGuid(),Name="Database",Host="deleted-profile.invalid",IconId="database" };
        RedesignChecks.Wait(repo.SaveAsync(a)); RedesignChecks.Wait(repo.SaveAsync(b));
        MainWindow Create()=>new(new MainViewModel(repo),repo,store,new SshConnectionFactory(repo),new ExternalRdpClient());
        var main=Create(); main.Show(); RedesignChecks.Frames(20);
        var panes=new[]{new SessionPane(main,a,new SshConnectionFactory(repo),new ExternalRdpClient(),repo),new SessionPane(main,a,new SshConnectionFactory(repo),new ExternalRdpClient(),repo),new SessionPane(main,b,new SshConnectionFactory(repo),new ExternalRdpClient(),repo),new SessionPane(main,a with { Id=Guid.NewGuid(),Host="ephemeral-quick.invalid" },new SshConnectionFactory(repo),new ExternalRdpClient(),repo)};
        foreach(var pane in panes)main.RegisterPane(pane,SessionDock.Primary); panes[0].PrepareRestored(1); panes[2].PrepareRestored(2); main.DockSession(panes[2],SessionDock.Secondary); main.SetWorkspaceMode(WorkspaceMode.Rows); main.Workspace.SetRatio(.61); main.DockSession(panes[1],SessionDock.Floating); RedesignChecks.Frames();
        var floating=main.OwnedWindows.Single(); floating.Width=800; floating.Height=560; floating.Position=new PixelPoint(30,50); RedesignChecks.Frames(); main.FocusSession(panes[0]); main.FocusSession(panes[2]); RedesignChecks.Wait(main.SaveSessionSnapshotAsync());
        var json=repo.SettingAsync("lastWorkspace").GetAwaiter().GetResult()!; var snapshot=WorkspaceSnapshot.Parse(json);
        if(snapshot.Sessions.Count!=3 || snapshot.Sessions.Count(s=>s.ProfileId==a.Id)!=2 || snapshot.Sessions.Single(s=>s.Dock==SessionDock.Floating).Window==null || json.Contains("invalid") || json.Contains("private") || json.Contains("credential-secret"))throw new Exception("Workspace captured configuration/secret or lost duplicate/geometry");
        Close(main); RedesignChecks.Wait(repo.DeleteAsync(b.Id)); RedesignChecks.Wait(repo.SaveAsync(a with { Name="Updated Operations",Color="#E5B65C" }));
        var restored=Create(); restored.Show(); RedesignChecks.Frames(30);
        if(restored.OpenSessions.Count!=2 || restored.OpenSessions.Any(p=>p.HasAttemptedConnection || p.IsConnected || p.Children.OfType<TerminalControl>().Single().IsLive) || store.Reads!=0)throw new Exception("Restoration connected or read credentials automatically");
        if(restored.OpenSessions.Any(p=>p.PresentationProfile.Name!="Updated Operations" || p.PresentationProfile.Color!="#E5B65C") || restored.Workspace.Mode!=WorkspaceMode.Rows || Math.Abs(restored.Workspace.Ratio-.61)>.001)throw new Exception("Restoration lost current profile metadata or layout");
        var ready=restored.OpenSessions.Single(p=>restored.Workspace.Location(p.WorkspaceId)==SessionDock.Primary);
        if(ready.SelectedPanel!=1 || !ready.GetLogicalDescendants().OfType<Button>().Any(button=>button.Tag as string=="Połącz" && button.IsEnabled) || ready.GetLogicalDescendants().OfType<Button>().Any(button=>button.Tag as string is "SFTP" or "Server Info" && button.IsEnabled))throw new Exception("Restored panel/connection controls incorrect");
        if(!restored.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("pominięto usunięte profile: 1")==true))throw new Exception("Missing profile restoration notice absent");
        restored.CaptureRenderedFrame()!.Save(Path.Combine(root,"restored-workspace.png")); restored.OwnedWindows.Single().CaptureRenderedFrame()!.Save(Path.Combine(root,"restored-floating.png"));
        restored.KeyPress(Key.L,RawInputModifiers.Control|RawInputModifiers.Shift,PhysicalKey.L,"L"); RedesignChecks.Frames(); var switcher=restored.OwnedWindows.Single(w=>w.Title=="Otwarte sesje");
        var search=switcher.GetLogicalDescendants().OfType<TextBox>().Single(t=>AutomationProperties.GetName(t)=="Szukaj otwartych sesji"); var list=switcher.GetLogicalDescendants().OfType<ListBox>().Single(); search.Text="updated osobne okno"; RedesignChecks.Frames(); if(list.ItemCount!=1)throw new Exception("Session search did not combine name/location");
        var selectedSurface=switcher.GetVisualDescendants().OfType<SessionChoiceSurface>().Single();
        var selectedTheme=DesignSystem.Themes.Single(theme=>theme.Id==DesignSystem.CurrentId);
        if(!selectedSurface.Selected || selectedSurface.Background is not Avalonia.Media.ISolidColorBrush brush || brush.Color!=Avalonia.Media.Color.Parse(selectedTheme.Selected))throw new Exception($"Selected result surface: selected={selectedSurface.Selected}, actual={selectedSurface.Background}, expected={selectedTheme.Selected}");
        switcher.CaptureRenderedFrame()!.Save(Path.Combine(root,"session-switcher.png")); DesignSystem.Apply("light"); RedesignChecks.Frames(); switcher.CaptureRenderedFrame()!.Save(Path.Combine(root,"session-switcher-light.png")); DesignSystem.Apply("dark"); RedesignChecks.Frames(); var target=restored.OpenSessions.Single(p=>restored.Workspace.Location(p.WorkspaceId)==SessionDock.Floating);
        switcher.KeyPress(Key.Enter,RawInputModifiers.None,PhysicalKey.Enter,"\r"); RedesignChecks.Frames(); if(restored.Workspace.Focused!=target.WorkspaceId)throw new Exception("Session switcher did not activate floating card");
        var choose=restored.SwitchSessionAsync(); RedesignChecks.Frames(); switcher=restored.OwnedWindows.Single(w=>w.Title=="Otwarte sesje"); switcher.GetLogicalDescendants().OfType<TextBox>().Single().Text="no-result"; RedesignChecks.Frames(); if(switcher.GetLogicalDescendants().OfType<Button>().Single(b=>b.Tag as string=="Przejdź").IsEnabled)throw new Exception("Empty search enabled activation"); switcher.Close(); RedesignChecks.Wait(choose);
        RedesignChecks.Wait(repo.SetSettingAsync("rememberWorkspace","False")); Close(restored); if(repo.SettingAsync("lastWorkspace").GetAwaiter().GetResult()!="")throw new Exception("Disabled restore retained stored cards");
        var disabled=Create(); disabled.Show(); RedesignChecks.Frames(20); if(disabled.OpenSessions.Count!=0 || store.Reads!=0)throw new Exception("Disabled restore reopened cards"); disabled.Close(); RedesignChecks.Frames();
        RedesignChecks.Wait(repo.SetSettingAsync("rememberWorkspace","True")); RedesignChecks.Wait(repo.SetSettingAsync("lastWorkspace","{invalid")); var bad=Create(); bad.Show(); RedesignChecks.Frames(20); if(bad.OpenSessions.Count!=0 || bad.OwnedWindows.Count!=0 || !bad.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("Nie udało się przywrócić kart")==true))throw new Exception("Corrupt restoration blocked or disappeared silently"); bad.Close(); RedesignChecks.Frames();
        Console.WriteLine("PASS workspace reference-only capture, duplicate cards, quick-connect exclusion, latest profile/deleted-profile handling, floating geometry, restore with zero credential reads/connections, panel retention, Ctrl+Shift+L search/Enter/empty results and opt-out/corrupt snapshot handling");
    }
    internal static void Network(MainWindow main,ProfileRepository repo,string root)
    {
        var panes=main.OpenSessions.ToArray(); var first=panes[0]; var terminal=first.Children.OfType<TerminalControl>().Single(); var sessionId=terminal.SessionId;
        if(!first.GetLogicalDescendants().OfType<Button>().Any(b=>b.Tag as string=="Połączono" && !b.IsEnabled))throw new Exception("Live SSH reconnect button not protected");
        var restart=main.ReconnectAsync(first); RedesignChecks.Frames(); var dialog=main.OwnedWindows.Single(w=>w.Title=="Uruchomić ponownie sesję?"); dialog.GetLogicalDescendants().OfType<Button>().Single(b=>b.Tag as string=="Anuluj").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Wait(restart);
        if(!first.IsConnected || terminal.SessionId!=sessionId)throw new Exception("Cancelling restart disconnected SSH");
        main.SetWorkspaceMode(WorkspaceMode.Columns); RedesignChecks.Frames();
        foreach(var pane in panes)
        {
            var toolbar=pane.GetVisualDescendants().OfType<WrapPanel>().Single(p=>p.Name=="SessionToolbar"); var options=toolbar.Children.OfType<Button>().Single(b=>b.Tag as string=="Opcje");
            if(options.Bounds.Right>toolbar.Bounds.Width+.5 || options.Bounds.Bottom>toolbar.Bounds.Height+.5)throw new Exception("Narrow toolbar hides Options");
        }
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"live-responsive-sessions.png"));
        RedesignChecks.Wait(repo.SaveAsync(first.Profile)); RedesignChecks.Wait(main.SaveSessionSnapshotAsync()); var saved=WorkspaceSnapshot.Parse(repo.SettingAsync("lastWorkspace").GetAwaiter().GetResult()!); if(saved.Sessions.Count!=1)throw new Exception("Live snapshot included unsaved profile");
        var resumed=new MainWindow(new MainViewModel(repo),repo,new ResumeStore(),new SshConnectionFactory(repo),new ExternalRdpClient()); resumed.Show(); RedesignChecks.Frames(25); if(resumed.OpenSessions.Count!=1)throw new Exception("Live profile did not restore its card");
        var pending=resumed.OpenSessions.Single(); if(pending.IsConnected || pending.HasAttemptedConnection)throw new Exception("Restored live profile connected automatically");
        RedesignChecks.Wait(resumed.StartExistingSessionAsync(pending)); RedesignChecks.Frames(15); if(!pending.IsConnected)throw new Exception("Manual restored-card connection failed"); resumed.CaptureRenderedFrame()!.Save(Path.Combine(root,"manual-restored-ssh.png")); Close(resumed);
        main.SetWorkspaceMode(WorkspaceMode.Single); main.FocusSession(panes[1]); Console.WriteLine("PASS protected live reconnect/cancel, responsive narrow toolbar and manual connection of restored real SSH profile");
    }
    private static void Close(MainWindow main)
    {
        main.Close(); RedesignChecks.Frames(); if(main.OpenSessions.Count>0) { var dialog=main.OwnedWindows.Single(w=>w.GetLogicalDescendants().OfType<Button>().Any(b=>b.Tag as string=="Potwierdź")); dialog.GetLogicalDescendants().OfType<Button>().Single(b=>b.Tag as string=="Potwierdź").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); } RedesignChecks.Frames(20);
    }
    private sealed class CountingStore : ICredentialStore
    {
        internal int Reads;
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Reads++; throw new Exception("Restoration must not read credentials"); }
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>throw new NotSupportedException();
    }
    private sealed class ResumeStore : ICredentialStore
    {
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>Task.FromResult<string?>("serverdesk-test-passphrase");
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default)=>Task.CompletedTask;
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>Task.CompletedTask;
    }
}
