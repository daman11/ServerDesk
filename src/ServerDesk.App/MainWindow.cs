using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.Input;
using Iciclecreek.Terminal;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Serilog;
using System.Diagnostics;

namespace ServerDesk.App;

public sealed partial class MainWindow : Window
{
    private readonly MainViewModel vm;
    private readonly ProfileRepository repository;
    private readonly ICredentialStore credentials;
    private readonly SshConnectionFactory ssh;
    private readonly IRdpClient rdp;
    private readonly ListBox servers=new();
    private readonly TabControl tabs=new();
    private readonly Grid sessionHost=new();
    private readonly List<SessionPane> sessions=[];
    private readonly HashSet<Task> closingSessions=[];
    private readonly CancellationTokenSource lifetime=new();
    private readonly Grid layout=new() { ColumnDefinitions=new ColumnDefinitions($"{DesignTokens.ActivityWidth},272,{DesignTokens.Small},*"),RowDefinitions=new RowDefinitions($"Auto,*,{DesignTokens.StatusHeight}") };
    private bool closing,closePending,readyToClose;
    private Task? initialization;
    internal static readonly SemaphoreSlim TransferSlots=new(2);
    private static readonly DataFormat<ServerProfile> ServerDrag=DataFormat.CreateInProcessFormat<ServerProfile>("ServerDesk.Server");
    private static readonly DataFormat<WorkspacePane> TabDrag=DataFormat.CreateInProcessFormat<WorkspacePane>("ServerDesk.Session");

    public MainWindow(MainViewModel vm,ProfileRepository repository,ICredentialStore credentials,SshConnectionFactory ssh,IRdpClient rdp)
    {
        this.vm=vm; this.repository=repository; this.credentials=credentials; this.ssh=ssh; this.rdp=rdp;
        Title="ServerDesk"; Width=1320; Height=840; MinWidth=900; MinHeight=600; CanResize=true; DataContext=vm;
        ExtendClientAreaToDecorationsHint=true;
        ExtendClientAreaTitleBarHeightHint=DesignTokens.HeaderHeight;
        WindowDecorations=WindowDecorations.BorderOnly;
        // Floating children normally cancel their close to redock. Confirm shutdown
        // at the owner first, then close every child with the shared closing flag.
        ClosingBehavior=WindowClosingBehavior.OwnerWindowOnly;
        ssh.ConfirmHostKey=(challenge,ct)=> Dispatcher.UIThread.InvokeAsync(()=>Dialogs.Confirm(this,L.T("Weryfikacja klucza SSH"),L.F($"Pierwsze połączenie: {challenge.Host}:{challenge.Port}\n{challenge.Algorithm}\n{challenge.Fingerprint}\n\nPorównaj fingerprint z zaufanym źródłem. Zapisać klucz i kontynuować?"),ct));
        BuildWorkspace();
        if(OperatingSystem.IsLinux())BuildResizeEdges();
        ConfigureWindowPlacement();
        Activated+=(_,_)=> { if(ActiveSession is {} active && dockState.Location(active.WorkspaceId)==SessionDock.Floating && sessions.FirstOrDefault(p=>WorkspaceState.DockedGroups.Select(dockState.Selected).Contains(p.WorkspaceId)) is {} docked)FocusSession(docked); };
        Opened+=async (_,_)=> { initialization=Safe(InitializeAsync); await initialization; };
        Closing+=async (_,e)=>
        {
            if(closing) { e.Cancel=!readyToClose; return; } e.Cancel=true;
            if(closePending)return; closePending=true;
            try
            {
                if(initialization!=null)await initialization;
                var language=restartLanguage??L.Language;
                if((sessions.Count>0 || networkPanes.Any(p=>p.IsRunning)) && !await Dialogs.Confirm(this,L.T(restartLanguage==null?"Zamknąć ServerDesk?":"Uruchomić ponownie ServerDesk?",language),L.T("Sesje i transfery zostaną rozłączone, a trwająca diagnostyka sieci zatrzymana.",language),language:language)) return;
                if(restartLanguage!=null)
                {
                    // Start before teardown so a launch failure leaves live sessions intact.
                    // The child waits for this process to exit before accessing application data.
                    try { StartReplacement(); }
                    catch(Exception ex)
                    {
                        Log.Error("Nie udało się ponownie uruchomić aplikacji: {Type}",ex.GetType().Name);
                        await Dialogs.Message(this,L.T("ServerDesk — błąd",language),L.T("Nie udało się uruchomić nowej instancji. Aplikacja pozostaje otwarta. Możesz zamknąć ją i uruchomić ręcznie.",language)); return;
                    }
                }
                await profileSortSave; CancelPendingSnapshot(); if(workspaceReady)await Safe(SaveSessionSnapshotAsync);
                await Safe(()=>commandPanel?.Shutdown??Task.CompletedTask); closing=true; statusTimer.Stop(); lifetime.Cancel(); commandPanel?.Dispose(); foreach(var session in sessions)session.StatusChanged-=PaneStatusChanged;
                var closingTools=networkPanes.ToArray(); foreach(var tool in closingTools)tool.Dispose(); await Task.WhenAll(closingTools.Select(tool=>tool.Shutdown)); networkPanes.Clear();
                await Safe(()=>Task.WhenAll(sessions.Select(session=>session.CloseAsync()).Concat(closingSessions)));
                foreach(var floating in floatingDocks.Values.ToArray())floating.Window.Close(); floatingDocks.Clear();
                await Safe(async()=> { await SaveWorkspaceAsync(); await repository.SaveMainWindowPlacementAsync(CaptureMainPlacement()); await repository.SetSettingAsync("sidebar",sidebarWidth.ToString(System.Globalization.CultureInfo.InvariantCulture)); await repository.SetSettingAsync("collapsedFolders",System.Text.Json.JsonSerializer.Serialize(collapsed)); await repository.SetSettingAsync("sidebarHidden",(!sidebar.IsVisible).ToString()); }); readyToClose=true; Close();
            }
            finally { closePending=false; if(!closing)restartLanguage=null; }
        };
    }
    private Button Action(string label,Func<Task> action) => Dialogs.Button(label,()=>_ = Safe(action));
    private MenuItem Menu(string label,Func<Task> action,string? glyph=null,string? shortcut=null) { var menu=DeskIcons.ActionMenu(label,glyph,shortcut); menu.Click+=(_,_)=>_ = Safe(action); return menu; }
    internal async Task Safe(Func<Task> action)
    {
        try { await action(); }
        catch(OperationCanceledException) { vm.Status=L.T("Operacja anulowana"); }
        catch(RdpHostUnavailableException ex) { Log.Error("Nie udało się osadzić RDP: {Type}",ex.InnerException?.GetType().Name); vm.Status=ex.Message; }
        catch(Exception ex) { Log.Error("Błąd operacji typu {Type}",ex.GetType().Name); vm.Status=ex is ArgumentException or InvalidDataException or FileNotFoundException or InvalidOperationException or NotSupportedException ? ex.Message : L.F($"Operacja nie powiodła się ({ex.GetType().Name}). Sprawdź adres, uprawnienia i połączenie."); await Dialogs.Message(this,L.T("ServerDesk — błąd"),vm.Status); }
    }
    private void PaneStatusChanged() { if(!closing) { UpdateStatus(); UpdateResumeNotice(); } }
    internal void ReportBackgroundError(string message,Exception ex) { Log.Error("Błąd ustawień tła typu {Type}; {Stack}",ex.GetType().Name,ex.StackTrace); vm.Status=message; }
    private async Task InitializeAsync()
    {
        if(!await InitializeDatabaseForStartupAsync())return;
        await RestoreMainPlacementAsync();
        PreviewPreferences(await repository.PreferencesAsync(lifetime.Token));
        UpdateLanguageRestartNotice(preferences.Language);
        if(await repository.SettingAsync("collapsedFolders") is {} foldersJson) { try { foreach(var f in System.Text.Json.JsonSerializer.Deserialize<string[]>(foldersJson)??[])collapsed.Add(f); } catch(System.Text.Json.JsonException) { } }
        await vm.LoadAsync(); await RestoreProfileOrderingAsync(); DesignSystem.Apply(await repository.SettingAsync("theme")??"dark");
        if(double.TryParse(await repository.SettingAsync("sidebar"),System.Globalization.CultureInfo.InvariantCulture,out var sidebar)) layout.ColumnDefinitions[1].Width=new GridLength(sidebarWidth=Math.Clamp(sidebar,220,500));
        if(await repository.SettingAsync("sidebarHidden")=="True")ToggleSidebar();
        if(preferences.SidebarOnStartup==SidebarStartup.Show && !this.sidebar.IsVisible || preferences.SidebarOnStartup==SidebarStartup.Hide && this.sidebar.IsVisible)ToggleSidebar();
        if(double.TryParse(await repository.SettingAsync("splitRatio"),System.Globalization.CultureInfo.InvariantCulture,out var ratio))dockState.SetRatio(ratio);
        if(double.TryParse(await repository.SettingAsync("rowSplitRatio"),System.Globalization.CultureInfo.InvariantCulture,out var rowRatio))dockState.SetRowRatio(rowRatio);
        if(Enum.TryParse<WorkspaceMode>(await repository.SettingAsync("workspaceMode"),out var mode) && Enum.IsDefined(mode))dockState.SetMode(mode); LayoutDockWorkspace();
        await RestoreSessionSnapshotAsync();
        if(preferences.WorkspaceOnStartup!=WorkspaceStartup.Remember)SetWorkspaceMode((WorkspaceMode)((int)preferences.WorkspaceOnStartup-1));
        workspaceReady=true;
    }
    private async Task EditAsync(ServerProfile? profile)
    {
        var edited=await Dialogs.Edit(this,profile,vm.Folders); if(edited==null)return;
        await SaveEditedProfileAsync(profile,edited);
    }
    internal async Task SaveEditedProfileAsync(ServerProfile? original,ServerProfile edited)
    {
        edited.Validate();
        var changed=original!=null && !CredentialReference.CanReuse(original,edited);
        if(changed)
        {
            // Fail before saving a new destination if its old secret cannot be removed.
            await credentials.DeleteAsync(original!.Id,lifetime.Token);
            await credentials.DeleteAsync(CredentialReference.Vnc(original.Id),lifetime.Token);
        }
        if(original!=null && (changed || !ConnectionHistorySnapshot.Capture(original).Matches(edited)))edited=edited with { LastConnected=null };
        await repository.SaveAsync(edited);
        foreach(var session in sessions.Where(s=>s.Profile.Id==edited.Id))session.UpdatePresentation(edited);
        await vm.LoadAsync(); vm.SelectedServer=edited;
        if(changed)vm.Status=L.T("Zmieniono cel lub sposób logowania. Przy następnym połączeniu podaj hasło ponownie.");
    }
    private async Task FolderAsync() { var name=await Dialogs.Prompt(this,L.T("Folder, np. Produkcja/Linux")); if(name==null)return; await repository.AddFolderAsync(name); await vm.LoadAsync(); }
    internal async Task EditFolderIconAsync(string path)
    {
        var icon=await Dialogs.FolderIcon(this,path,vm.FolderIcons.GetValueOrDefault(path,"folder"));
        if(icon==null)return;
        await repository.SetFolderIconAsync(path,icon,lifetime.Token); await vm.LoadAsync(); QueueTree();
    }
    private bool RealFolder()=>vm.SelectedFolder is not ("Wszystkie" or "★ Ulubione" or "◷ Ostatnie" or "" or null);
    private async Task RenameFolderAsync() { if(!RealFolder())return; var target=await Dialogs.Prompt(this,L.T("Nowa ścieżka folderu"),vm.SelectedFolder); if(target==null)return; await repository.RenameFolderAsync(vm.SelectedFolder,target); vm.SelectedFolder=target; await vm.LoadAsync(); }
    private async Task DeleteFolderAsync() { if(!RealFolder())return; if(!await Dialogs.Confirm(this,L.T("Usunąć pusty folder i podfoldery?"),vm.SelectedFolder))return; await repository.DeleteFolderAsync(vm.SelectedFolder); vm.SelectedFolder="Wszystkie"; await vm.LoadAsync(); }
    private async Task SettingsAsync() { await Dialogs.Settings(this,repository,()=>Task.WhenAll(sessions.Select(p=>p.ApplyAppearanceAsync()))); QueueWorkspaceSave(); }
    private async Task DuplicateAsync() { if(vm.SelectedServer is {} p) await EditAsync(p with { Id=Guid.NewGuid(),Name=p.Name+" — kopia",LastConnected=null }); }
    private async Task FavoriteAsync() { if(vm.SelectedServer is {} p) { await repository.SaveAsync(p with { Favorite=!p.Favorite }); await vm.LoadAsync(); } }
    private async Task MoveAsync() { if(vm.SelectedServer is {} p) { var folder=await Dialogs.Prompt(this,L.T("Folder docelowy"),p.Folder); if(folder!=null) { await repository.MoveToFolderAsync(p.Id,folder); await vm.LoadAsync(); } } }
    private async Task DeleteAsync()
    {
        if(vm.SelectedServer is not {} p || !await Dialogs.Confirm(this,L.T("Usunąć serwer?"),p.Name))return;
        try { await credentials.DeleteAsync(p.Id); await credentials.DeleteAsync(CredentialReference.Vnc(p.Id)); }
        catch(InvalidOperationException)
        { if(!await Dialogs.Confirm(this,L.T("Magazyn poświadczeń jest niedostępny"),L.T("Nie można potwierdzić usunięcia hasła z magazynu systemowego. Usunąć sam profil serwera? Poświadczenie może pozostać w magazynie — usuń je później, gdy będzie dostępny.")))return; }
        await repository.DeleteAsync(p.Id); await vm.LoadAsync();
    }
    private async Task ForgetSecretAsync() { if(vm.SelectedServer is {} p) { await credentials.DeleteAsync(p.Id); await credentials.DeleteAsync(CredentialReference.Vnc(p.Id)); vm.Status=L.T("Usunięto zapisane poświadczenie"); } }
    private async Task QuickAsync() { var uri=await Dialogs.Prompt(this,L.T("Szybkie połączenie"),"ssh://admin@localhost:22"); if(uri!=null) await ConnectAsync(QuickConnect.Parse(uri)); }
    private async Task ExportAsync()
    {
        using var export=new MemoryStream(); await repository.ExportAsync(export,lifetime.Token); export.Position=0;
        var file=await StorageProvider.SaveFilePickerAsync(new() { Title=L.T("Eksport konfiguracji bez sekretów"),SuggestedFileName="serverdesk.json" }); if(file==null)return;
        await using var stream=await file.OpenWriteAsync(); stream.SetLength(0); await export.CopyToAsync(stream,lifetime.Token); vm.Status=L.T("Wyeksportowano konfigurację bez haseł");
    }
    private async Task ImportAsync()
    {
        var files=await StorageProvider.OpenFilePickerAsync(new() { Title=L.T("Import konfiguracji JSON"),AllowMultiple=false }); if(files.Count==0)return;
        await using var stream=await files[0].OpenReadAsync(); await repository.ImportAsync(stream,lifetime.Token); await vm.LoadAsync(); vm.Status=L.T("Zaimportowano serwery jako nowe wpisy");
    }
    internal async Task ConnectAsync(ServerProfile? profile)
    {
        if(profile==null || closing || closePending)return; var secret=await SessionSecretAsync(profile); if(secret==null || closing || closePending)return;
        var pane=new SessionPane(this,profile,ssh,rdp,repository);
        RegisterPane(pane);
        try { await pane.ConnectAsync(secret,lifetime.Token); vm.Status=pane.HasActiveClient?$"{profile.Protocol} · {profile.Endpoint} · {profile.Name}":pane.ConnectionStatus; }
        catch(OperationCanceledException) { throw; }
        catch { pane.MarkFailed(); throw; }
    }
    internal async Task CloseSessionAsync(SessionPane pane)
    {
        if(!sessions.Contains(pane))return;
        if(!await Dialogs.Confirm(this,L.T("Zamknąć sesję?"),L.F($"Rozłączyć {pane.Profile.Name} i anulować transfery?")))return;
        if(!closing && sessions.Contains(pane)) { RemovePane(pane); await pane.CloseAsync(); }
    }
    private async Task TrackSessionCloseAsync(SessionPane pane)
    {
        var work=pane.CloseAsync(); closingSessions.Add(work);
        try { await work; }
        catch(Exception ex) { ReportBackgroundError(L.T("Nie udało się zakończyć sesji."),ex); }
        finally { closingSessions.Remove(work); }
    }
    internal async Task ReconnectAsync(SessionPane pane)
    {
        if(pane.IsBusy || closing || closePending || !sessions.Contains(pane))return;
        if((pane.HasActiveClient || pane.ActiveTransfers>0) && !await Dialogs.Confirm(this,L.T("Uruchomić ponownie sesję?"),L.T("Bieżące połączenie, uruchomione polecenia i transfery zostaną przerwane.")))return;
        if(closing || closePending || !sessions.Contains(pane))return;
        await pane.ReconnectAsync(); if(!pane.HasActiveClient) { vm.Status=pane.ConnectionStatus; return; } vm.Status=(pane.Profile.Protocol==Protocol.RDP?L.T("Ponownie uruchomiono klienta RDP · "):L.T("Ponownie połączono · "))+pane.Profile.Endpoint;
        await pane.ShowRestoredPanelAsync();
    }
    internal async Task ReauthenticateAsync(SessionPane pane)
    {
        if(pane.IsBusy || closing || closePending || !sessions.Contains(pane))return;
        if(pane.Profile.Protocol==Protocol.Serial)return;
        var secret=pane.Profile.Protocol==Protocol.RDP && OperatingSystem.IsLinux()?await SessionSecretCoreAsync(pane.Profile,true):await Dialogs.Prompt(this,pane.Profile.Protocol==Protocol.VNC?L.T("Hasło VNC"):pane.Profile.Protocol==Protocol.RDP?L.T("Hasło RDP"):pane.Profile.Authentication==Authentication.PrivateKey?L.T("Passphrase klucza (puste dla klucza bez hasła)"):L.T("Hasło SSH"),secret:true);
        if(secret==null || closing || closePending || !sessions.Contains(pane))return;
        if((pane.HasActiveClient || pane.ActiveTransfers>0) && !await Dialogs.Confirm(this,L.T("Zmienić poświadczenia i połączyć ponownie?"),L.T("Bieżące połączenie, uruchomione polecenia i transfery zostaną przerwane.")))return;
        if(closing || closePending || !sessions.Contains(pane))return;
        await pane.ReconnectAsync(secret); if(!pane.HasActiveClient)return;
        await pane.ShowRestoredPanelAsync();
    }
}
