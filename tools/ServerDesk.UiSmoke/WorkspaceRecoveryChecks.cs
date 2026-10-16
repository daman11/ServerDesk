using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class WorkspaceRecoveryChecks
{
    private static ProfileRepository Repo(string root)=>new(new AppPaths(Path.Combine(root,"crash-data")));
    private static MainWindow Create(ProfileRepository repo)=>new(new MainViewModel(repo),repo,new NoReadStore(),new SshConnectionFactory(repo),new ExternalRdpClient());
    private static WorkspaceSnapshot? Saved(ProfileRepository repo)
    { var value=repo.SettingAsync("lastWorkspace").GetAwaiter().GetResult(); return string.IsNullOrEmpty(value)?null:WorkspaceSnapshot.Parse(value); }
    private static void Until(Func<bool> condition)
    { var until=DateTime.UtcNow.AddSeconds(12); while(!condition() && DateTime.UtcNow<until) { AvaloniaHeadlessPlatform.ForceRenderTimerTick(); RedesignChecks.Frames(1); } if(!condition())throw new Exception("Automatic workspace save did not complete"); }
    internal static void WriteThenCrash(string root)
    {
        Avalonia.Threading.AvaloniaSynchronizationContext.InstallIfNeeded();
        var repo=Repo(root); RedesignChecks.Wait(repo.InitializeAsync());
        var profile=new ServerProfile { Name="Crash recovery",Host="crash-test.invalid",Username="admin" }; RedesignChecks.Wait(repo.SaveAsync(profile));
        var main=Create(repo); main.Show(); RedesignChecks.Frames(20);
        var first=new SessionPane(main,profile,new SshConnectionFactory(repo),new ExternalRdpClient(),repo); main.RegisterPane(first);
        var second=new SessionPane(main,profile,new SshConnectionFactory(repo),new ExternalRdpClient(),repo); main.RegisterPane(second);
        main.SetWorkspaceMode(WorkspaceMode.Rows); main.Workspace.SetRatio(.67); first.PrepareRestored(1); main.DockSession(second,SessionDock.Floating);
        var window=main.OwnedWindows.Single(); window.Width=810; window.Height=570; window.Position=new PixelPoint(40,60); main.FocusSession(first);
        Until(()=>Saved(repo) is { Sessions.Count:2,Mode:WorkspaceMode.Rows,Ratio:.67 } snapshot && snapshot.Sessions.Any(s=>s.Panel==1) && snapshot.Sessions.Any(s=>s.Dock==SessionDock.Floating && s.Window?.Width==810));
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"before-abrupt-exit.png"));
        Console.WriteLine("PASS automatic save while window is open; exiting process without Closing/Dispose");
        Environment.Exit(0);
    }
    internal static void RestoreAfterCrash(string root)
    {
        Avalonia.Threading.AvaloniaSynchronizationContext.InstallIfNeeded();
        var repo=Repo(root); var saved=Saved(repo)??throw new Exception("No workspace survived abrupt process exit");
        var main=Create(repo); main.Show(); RedesignChecks.Frames(30);
        if(main.OpenSessions.Count!=2 || main.OpenSessions.Any(p=>p.HasAttemptedConnection || p.IsConnected) || main.Workspace.Mode!=WorkspaceMode.Rows || Math.Abs(main.Workspace.Ratio-.67)>.001 || main.OwnedWindows.Count!=1 || main.OpenSessions.Count(p=>p.SelectedPanel==1)!=1)throw new Exception("Crash recovery lost layout or connected automatically");
        main.CaptureRenderedFrame()!.Save(Path.Combine(root,"after-abrupt-exit.png"));
        // Repeated close requests share one confirmation. Cancelling preserves cards/windows.
        main.Close(); main.Close(); main.Close(); RedesignChecks.Frames();
        var dialog=main.OwnedWindows.Single(w=>w.Title=="Zamknąć ServerDesk?");
        dialog.GetLogicalDescendants().OfType<Button>().Single(b=>b.Tag as string=="Anuluj").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames();
        if(!main.IsVisible || main.OpenSessions.Count!=2 || main.OwnedWindows.Count!=1)throw new Exception("Cancel shutdown closed a card or floating window");
        // Rapid changes settle on the latest state, not an earlier queued writer.
        var panes=main.OpenSessions.ToArray(); main.DockSession(panes[1],SessionDock.Primary); main.DockSession(panes[0],SessionDock.Secondary); main.SetWorkspaceMode(WorkspaceMode.Columns); main.SetWorkspaceMode(WorkspaceMode.Rows); main.Workspace.SetRatio(.58); main.FocusSession(panes[1]);
        Until(()=>Saved(repo) is { Mode:WorkspaceMode.Rows,Ratio:.58 } snapshot && snapshot.Sessions.All(s=>s.Dock!=SessionDock.Floating) && snapshot.Focused==panes[1].WorkspaceId);
        var close=main.CloseSessionAsync(panes[0]); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single(); Confirm(dialog); RedesignChecks.Wait(close);
        Until(()=>Saved(repo)?.Sessions.Count==1);
        close=main.CloseSessionAsync(main.OpenSessions.Single()); RedesignChecks.Frames(); Confirm(main.OwnedWindows.Single()); RedesignChecks.Wait(close); Until(()=>Saved(repo)?.Sessions.Count==0);
        var store=new PausedStore(); var waiting=new MainWindow(new MainViewModel(repo),repo,store,new SshConnectionFactory(repo),new ExternalRdpClient()); waiting.Show(); RedesignChecks.Frames(20);
        var profile=repo.ListAsync().GetAwaiter().GetResult().First(); var pending=new SessionPane(waiting,profile,new SshConnectionFactory(repo),new ExternalRdpClient(),repo); waiting.RegisterPane(pending);
        var attempt=waiting.StartExistingSessionAsync(pending); RedesignChecks.Wait(waiting.StartExistingSessionAsync(pending));
        if(store.Reads!=1 || !pending.IsBusy)throw new Exception("Repeated click requested credentials twice");
        close=waiting.CloseSessionAsync(pending); RedesignChecks.Frames(); Confirm(waiting.OwnedWindows.Single()); RedesignChecks.Wait(close); store.Result.SetResult("unused-test-secret"); RedesignChecks.Wait(attempt);
        if(pending.HasAttemptedConnection || pending.IsBusy)throw new Exception("Removed card started a delayed connection or retained busy state");
        waiting.Close(); Until(()=>!waiting.IsVisible);
        main.Close(); Until(()=>!main.IsVisible);
        var empty=Create(repo); empty.Show(); RedesignChecks.Frames(25); if(empty.OpenSessions.Count!=0)throw new Exception("Closed cards were resurrected");
        empty.Close(); Until(()=>!empty.IsVisible);
        Console.WriteLine("PASS abrupt process recovery with zero credential reads, duplicate profile cards, SFTP selection, floating geometry, shutdown reentrancy/cancel, latest rapid layout changes, removed/empty workspace persistence and delayed credentials without duplicate reads or a connection on a removed card");
    }
    private static void Confirm(Window dialog)=>dialog.GetLogicalDescendants().OfType<Button>().Single(b=>b.Tag as string=="Potwierdź").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private sealed class NoReadStore : ICredentialStore
    {
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default)=>throw new Exception("Recovery read credentials");
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>throw new NotSupportedException();
    }
    private sealed class PausedStore : ICredentialStore
    {
        internal int Reads;
        internal readonly TaskCompletionSource<string?> Result=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string?> GetAsync(Guid id,CancellationToken ct=default) { Reads++; return Result.Task; }
        public Task SaveAsync(Guid id,string secret,CancellationToken ct=default)=>throw new NotSupportedException();
        public Task DeleteAsync(Guid id,CancellationToken ct=default)=>throw new NotSupportedException();
    }
}
