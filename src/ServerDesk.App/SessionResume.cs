using Avalonia;
using Avalonia.Controls;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.Text.Json;
using Avalonia.Threading;

namespace ServerDesk.App;
public sealed partial class MainWindow
{
    private readonly SemaphoreSlim snapshotGate=new(1);
    private CancellationTokenSource? snapshotDelay;
    private bool workspaceReady,workspaceSaveFailed;
    private string workspaceNotice=L.T("Obszar pracy");
    private readonly HashSet<Guid> restoredCards=[];
    private int skippedRestoredProfiles;
    internal void QueueWorkspaceSave()
    {
        if(!workspaceReady || closing)return;
        CancelPendingSnapshot(); var delay=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); snapshotDelay=delay;
        _=SaveAfterDelayAsync(delay);
    }
    private void CancelPendingSnapshot()
    {
        var pending=snapshotDelay; snapshotDelay=null; pending?.Cancel();
    }
    private async Task SaveAfterDelayAsync(CancellationTokenSource delay)
    {
        try
        {
            await Task.Delay(600,delay.Token).ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(async ()=>
            {
                if(closing || delay.IsCancellationRequested)return;
                await SaveSessionSnapshotAsync();
                if(workspaceSaveFailed) { workspaceSaveFailed=false; SetWorkspaceNotice(workspaceNotice); }
            });
        }
        catch(OperationCanceledException) when(delay.IsCancellationRequested) { }
        catch(Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(()=>
            {
                if(closing)return;
                workspaceSaveFailed=true; ReportBackgroundError(L.T("Nie udało się zapisać układu kart. Ponowimy zapis po kolejnej zmianie."),ex);
                workspaceTitle.Text=vm.Status; ToolTip.SetTip(workspaceTitle,vm.Status);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(()=> { if(ReferenceEquals(snapshotDelay,delay))snapshotDelay=null; delay.Dispose(); });
        }
    }
    internal async Task SaveSessionSnapshotAsync()
    {
        await snapshotGate.WaitAsync();
        try
        {
            if(closing)return;
            // Capture only after earlier writers have finished, without yielding
            // while reading UI state. Later changes queue a newer snapshot.
            var ordered=dockState.All.Select(id=>sessions.FirstOrDefault(p=>p.WorkspaceId==id)).OfType<SessionPane>().ToArray();
            var entries=ordered.Select(p=>new SavedSession(p.WorkspaceId,p.Profile.Id,dockState.Location(p.WorkspaceId),p.SelectedPanel,floatingDocks.TryGetValue(p.WorkspaceId,out var floating)?floating.NormalGeometry:null)).ToList();
            Guid? Keep(Guid? id)=>entries.Any(e=>e.Key==id)?id:null;
            var snapshot=new WorkspaceSnapshot(1,dockState.Mode,dockState.Ratio,entries,Keep(dockState.SelectedPrimary),Keep(dockState.SelectedSecondary),Keep(dockState.Focused),Keep(dockState.SelectedTertiary),Keep(dockState.SelectedQuaternary),dockState.RowRatio);
            await Task.Run(()=>repository.SaveWorkspaceSnapshotAsync(snapshot));
        }
        finally { snapshotGate.Release(); }
    }
    private async Task RestoreSessionSnapshotAsync()
    {
        if(await repository.SettingAsync("rememberWorkspace")=="False")return;
        var json=await repository.SettingAsync("lastWorkspace"); if(string.IsNullOrWhiteSpace(json))return;
        WorkspaceSnapshot saved;
        try { saved=WorkspaceSnapshot.Parse(json); }
        catch(Exception ex) when(ex is JsonException or InvalidDataException)
        { ReportBackgroundError(L.T("Nie udało się przywrócić kart. Utwórz połączenie z listy serwerów."),ex); SetWorkspaceNotice(vm.Status); return; }
        var profiles=vm.AllServers.ToDictionary(p=>p.Id); var available=saved.AvailableProfiles(profiles.Keys); var mapping=new Dictionary<Guid,SessionPane>();
        dockState.SetMode(available.Mode); dockState.SetRatio(available.Ratio); dockState.SetRowRatio(available.RowRatio); LayoutDockWorkspace();
        foreach(var entry in available.Sessions)
        {
            var pane=new SessionPane(this,profiles[entry.ProfileId],ssh,rdp,repository); pane.PrepareRestored(entry.Panel); RegisterPane(pane,SessionDock.Primary); mapping[entry.Key]=pane;
            if(entry.Dock!=SessionDock.Primary)DockSession(pane,entry.Dock);
            if(entry.Window!=null && floatingDocks.TryGetValue(pane.WorkspaceId,out var floating))ApplyFloatingGeometry(floating,entry.Window);
        }
        if(available.SelectedPrimary is {} primary && mapping.TryGetValue(primary,out var p))FocusSession(p);
        if(available.SelectedSecondary is {} secondary && mapping.TryGetValue(secondary,out var s))FocusSession(s);
        if(available.SelectedTertiary is {} tertiary && mapping.TryGetValue(tertiary,out var t))FocusSession(t);
        if(available.SelectedQuaternary is {} quaternary && mapping.TryGetValue(quaternary,out var q))FocusSession(q);
        if(available.Focused is {} focused && mapping.TryGetValue(focused,out var f))FocusSession(f);
        foreach(var pane in mapping.Values)restoredCards.Add(pane.WorkspaceId);
        skippedRestoredProfiles=saved.Sessions.Count-available.Sessions.Count; UpdateResumeNotice();
        LayoutDockWorkspace();
    }
    private void UpdateResumeNotice()
    {
        if(restoredCards.Count==0 && skippedRestoredProfiles==0)return;
        var pending=sessions.Count(p=>restoredCards.Contains(p.WorkspaceId) && !p.HasAttemptedConnection);
        SetWorkspaceNotice(pending>0?L.F($"Przywrócono karty: {restoredCards.Count} · wybierz Połącz ({pending})")+(skippedRestoredProfiles>0?L.F($" · pominięto usunięte profile: {skippedRestoredProfiles}"):""):skippedRestoredProfiles>0?L.F($"Pominięto usunięte profile: {skippedRestoredProfiles}"):L.T("Obszar pracy"));
    }
    private void SetWorkspaceNotice(string message) { workspaceNotice=message; if(!workspaceSaveFailed) { workspaceTitle.Text=message; ToolTip.SetTip(workspaceTitle,message); } }
    private IReadOnlyList<ScreenArea> ScreenAreas()=>Screens.All.OrderByDescending(s=>s.IsPrimary).Select(s=>new ScreenArea(s.WorkingArea.X,s.WorkingArea.Y,s.WorkingArea.Width,s.WorkingArea.Height,s.Scaling)).ToArray();
    private void ApplyFloatingGeometry(FloatingDock floating,WindowGeometry original)
    {
        var window=floating.Window; var frame=FrameAllowance(window,false); var fit=original.Fit(ScreenAreas(),frameWidth:frame.Width,frameHeight:frame.Height);
        window.MinWidth=Math.Min(520,fit.Width); window.MinHeight=Math.Min(380,fit.Height); window.Width=fit.Width; window.Height=fit.Height; window.Position=new PixelPoint(fit.X,fit.Y); floating.NormalGeometry=fit;
    }
    private Task<string?> SessionSecretAsync(ServerProfile profile)=>SessionSecretCoreAsync(profile,false);
    private async Task<string?> SessionSecretCoreAsync(ServerProfile profile,bool forcePrompt)
    {
        if(profile.Authentication==Authentication.VaultSshKey)throw new InvalidOperationException(L.T("Klucz SSH z sejfu nie jest hasłem. Wybierz wpis SSH Key."));
        if(profile.Protocol==Protocol.Serial || profile.Protocol==Protocol.VNC && profile.Vnc.Authentication==VncAuthentication.None)return "";
        if(profile.Protocol==Protocol.RDP && !OperatingSystem.IsLinux() && (!OperatingSystem.IsWindows() || profile.Rdp.DisplayMode!=RdpDisplayMode.Embedded))return "";
        if(profile.Protocol==Protocol.RDP && OperatingSystem.IsLinux() && string.IsNullOrWhiteSpace(profile.Username))throw new ArgumentException(L.T("Wpisz login w profilu serwera, aby połączyć RDP w zakładce."));
        string secret; string? notice=null;
        if(profile.Bitwarden is {} reference)
        {
            if(IsPluginEnabled(PluginCatalog.Bitwarden))
            {
                using var choice=await ChooseBitwardenAsync(this,profile,reference);
                if(choice==null)return null;
                if(!choice.Manual)return choice.Password;
            }
            notice=L.T("Profil ma przypisany wpis sejfu. Hasło podane ręcznie zostanie użyte jednorazowo.");
        }
        try { secret=profile.Bitwarden!=null || forcePrompt?"":await credentials.GetAsync(CredentialReference.ForProfile(profile),lifetime.Token)??""; }
        catch(CredentialReadException ex)
        {
            secret=""; notice=ex.Message; vm.Status=notice;
            Serilog.Log.Warning("Saved credential unavailable: {Type}",ex.InnerException?.GetType().Name??ex.GetType().Name);
        }
        if(secret.Length>0)return secret;
        var input=await Dialogs.Credentials(this,profile.Protocol==Protocol.SSH && profile.Authentication==Authentication.PrivateKey,profile.Protocol==Protocol.RDP,profile.Protocol==Protocol.VNC,notice,profile); if(input==null)return null;
        if(profile.Protocol==Protocol.RDP && OperatingSystem.IsLinux())LinuxRdpProcessSession.ValidatePassword(input.Secret);
        if(profile.Bitwarden==null && input.Remember && input.Secret.Length>0)
        {
            try { await credentials.SaveAsync(CredentialReference.ForProfile(profile),input.Secret,lifetime.Token); }
            catch(Exception ex) when(ex is InvalidOperationException or IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
            {
                // Persistence failure is separate from authentication. Ask explicitly
                // before using the supplied secret without another save attempt.
                Serilog.Log.Warning("Credential save was not confirmed: {Type}",ex.GetType().Name);
                if(!await Dialogs.ContinueWithoutSavingCredentials(this,lifetime.Token))return null;
            }
        }
        return input.Secret;
    }
    internal async Task StartExistingSessionAsync(SessionPane pane)
    {
        if(!sessions.Contains(pane) || pane.IsBusy || pane.HasActiveClient || closing || closePending)return;
        if(pane.HasAttemptedConnection) { await ReconnectAsync(pane); return; }
        using var busy=pane.WaitForCredentials();
        var vaultKeyMode=pane.Profile.Authentication==Authentication.VaultSshKey;
        using var choice=vaultKeyMode?await VaultSshKeyAsync(pane.Profile):null; using var key=choice?.TakeSshKey();
        if(vaultKeyMode && key==null)return;
        var secret=vaultKeyMode?"":await SessionSecretAsync(pane.Profile); if(secret==null || !sessions.Contains(pane) || closing || closePending)return;
        await pane.ConnectAsync(secret,lifetime.Token,key); if(!pane.HasActiveClient) { vm.Status=pane.ConnectionStatus; return; }
        await pane.ShowRestoredPanelAsync();
    }
}
