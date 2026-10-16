using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Polyline = Avalonia.Controls.Shapes.Polyline;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using Iciclecreek.Terminal;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.Diagnostics;

namespace ServerDesk.App;

internal sealed partial class SessionPane : WorkspacePane
{
    public ServerProfile Profile { get; }
    internal override string DisplayName=>PresentationProfile.Name;
    internal event Action? StatusChanged;
    private readonly MainWindow owner;
    private readonly SshConnectionFactory factory;
    private readonly IRdpClient rdp;
    private readonly ProfileRepository repository;
    private readonly CancellationTokenSource lifetime=new();
    private CancellationTokenSource connectionLifetime=new();
    private readonly SemaphoreSlim connectionGate=new(1);
    private readonly SemaphoreSlim refreshGate=new(1);
    private long connectionStarted;
    private bool connecting;
    private bool cancelRequested;
    private readonly Button connectButton;
    private readonly Button cancelConnectionButton;
    private Button? optionsButton;
    private readonly ScrollViewer restoredNotice=new() { Name="RestoredSessionNotice",IsVisible=false,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
    internal bool HasAttemptedConnection { get; private set; }
    internal bool HasActiveClient=>IsConnected || embeddedRdp is { IsRunning:true } || rdpProcess is { IsRunning:true } || Profile.Protocol==Protocol.RDP && terminal.IsLive;
    private int? restoredPanel;
    private Control? rdpNotice;
    public static readonly DirectProperty<SessionPane,string> SessionTitleProperty=AvaloniaProperty.RegisterDirect<SessionPane,string>(nameof(SessionTitle),p=>p.SessionTitle);
    private string sessionTitle="";
    public string SessionTitle => sessionTitle;
    public bool IsConnected => session is { Completion.IsCompleted:false } || serialSession is { Completion.IsCompleted:false } || vncSession is { IsConnected:true };
    private readonly TerminalControl terminal=new() { Process="",FontSize=14,BufferSize=10000,Options=new XTerm.Options.TerminalOptions { ClipboardReadEnabled=false,ClipboardWriteEnabled=false } };
    private readonly TextBlock state=Dialogs.Text(L.T("Łączenie…"),12);
    private readonly Grid files=new() { ColumnDefinitions=new ColumnDefinitions("*,4,*"),RowDefinitions=new RowDefinitions("Auto,Auto,*"),ClipToBounds=true };
    private readonly ScrollViewer info=new() { IsVisible=false };
    private readonly ListBox localList=new() { Focusable=true },remoteList=new() { Focusable=true };
    private readonly TextBox localPath=new() { Text=Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) },remotePath=new() { Text="/" };
    private readonly TextBlock transfer=Dialogs.Text(L.T("Brak transferów"),12);
    private readonly StackPanel jobList=new() { Spacing=DesignTokens.Tight };
    private readonly CheckBox hidden=new() { Content=L.T("Ukryte"),IsChecked=false };
    private readonly ComboBox fileTarget=new() { ItemsSource=new[] { L.T("Lokalny"), L.T("Zdalny") },SelectedIndex=1,Width=105 };
    private readonly CheckBox fullscreen=new() { Content=L.T("Pełny ekran") },clipboard=new() { Content=L.T("Schowek RDP"),IsChecked=true };
    private readonly NumericUpDown interval=new() { Minimum=2,Maximum=300,Value=5,Text="5",FormatString="0",Width=100 };
    private List<FileEntry> localEntries=[],remoteEntries=[];
    private SshTerminalSession? session;
    private SftpSession? sftp;
    private LinuxMonitoring? monitor;
    private RdpProcessSession? rdpProcess;
    private string secret="";
    private bool infoActive;
    private int disposed;
    private readonly List<CancellationTokenSource> transfers=[];
    private readonly ProgressBar cpuBar=new() { Maximum=100,Height=8 },ramBar=new() { Maximum=100,Height=8 };
    private readonly TextBlock cpu=Dialogs.Text(L.T("CPU: oczekiwanie na drugą próbkę")),ram=Dialogs.Text(""),system=Dialogs.Text(""),disks=Dialogs.Text(""),network=Dialogs.Text("");
    private readonly Polyline cpuChart=new() { StrokeThickness=2 };
    private readonly Queue<double> cpuHistory=[];
    private readonly TextBlock topology=Dialogs.Text(""),networkSummary=Dialogs.Text("");
    private readonly StackPanel diskRows=new() { Spacing=DesignTokens.Section },networkRows=new() { Spacing=DesignTokens.Section };
    private sealed record FileDrag(SessionPane Session,bool Remote,IReadOnlyList<FileEntry> Entries);
    private static readonly DataFormat<FileDrag> FileFormat=DataFormat.CreateInProcessFormat<FileDrag>("ServerDesk.File");
    public SessionPane(MainWindow owner,ServerProfile profile,SshConnectionFactory factory,IRdpClient rdp,ProfileRepository repository)
    {
        this.owner=owner; Profile=profile; UpdatePresentation(profile); this.factory=factory; this.rdp=rdp; this.repository=repository;
        ConfigureRemoteTerminalInput();
        ClipToBounds=true;
        RowDefinitions=new RowDefinitions("Auto,Auto,*,Auto,Auto");
        var actions=new WrapPanel { Name="SessionToolbar",Orientation=Orientation.Horizontal,Margin=new Thickness(10,5) };
        var toolbarSurface=new Border { Name="SessionToolbarSurface",Child=actions,BorderThickness=new Thickness(0,0,0,1) };
        DesignSystem.Resource(toolbarSurface,Border.BackgroundProperty,"BackgroundSecondary"); DesignSystem.Resource(toolbarSurface,Border.BorderBrushProperty,"BorderDefault"); Children.Add(toolbarSurface);
        SetStatus(L.T("Gotowy do połączenia"),"○",SessionVisualState.Idle);
        durationLoop=TrackDurationAsync();
        connectButton=Button("Połącz",()=>owner.StartExistingSessionAsync(this)); connectButton.IsDefault=false; connectButton.Classes.Add("primary"); connectButton.Classes.Add("chrome"); actions.Children.Add(connectButton);
        cancelConnectionButton=Dialogs.Button("Anuluj łączenie",CancelConnection); cancelConnectionButton.Name="CancelConnection"; cancelConnectionButton.Classes.Add("chrome"); cancelConnectionButton.IsVisible=false; actions.Children.Add(cancelConnectionButton);

        if(profile.Protocol==Protocol.SSH)
        {

            actions.Children.Add(PanelButton("Terminal",0)); actions.Children.Add(PanelButton("SFTP",1)); actions.Children.Add(PanelButton("Server Info",2));
            actions.Children.Add(Button("Komendy",owner.OpenCommandsPanelAsync));
            var options=new Button { Content=DeskIcons.Create("more"),Tag="Opcje",Width=30,Classes={"chrome","icon"} }; optionsButton=options; ToolTip.SetTip(options,L.T("Opcje sesji")); Avalonia.Automation.AutomationProperties.SetName(options,L.T("Opcje sesji"));
            options.ContextMenu=new ContextMenu { ItemsSource=new[]{OperationMenu("Uruchom ponownie sesję",()=>owner.ReconnectAsync(this)),OperationMenu("Poświadczenia",()=>owner.ReauthenticateAsync(this)),OperationMenu("Kopiuj zaznaczenie",()=>terminal.CopyAsync()),OperationMenu("Wklej",()=>terminal.PasteAsync()),OperationMenu("Większa czcionka",()=> { terminal.FontSize=Math.Min(32,terminal.FontSize+1); return Task.CompletedTask; }),OperationMenu("Mniejsza czcionka",()=> { terminal.FontSize=Math.Max(9,terminal.FontSize-1); return Task.CompletedTask; })} };
            options.Click+=(_,_)=>options.ContextMenu.Open(options); actions.Children.Add(options);
        }
        else if(profile.Protocol is Protocol.VNC or Protocol.Serial)BuildAdditionalControls(actions);
        else
        {
            BuildRdpControls(actions);
        }
        BuildTerminalSearch(actions);
        Grid.SetRow(terminal,2); Children.Add(terminal); files.IsVisible=false; Grid.SetRow(files,2); Children.Add(files); Grid.SetRow(info,2); Children.Add(info);
        if(additionalNotice!=null) { Children.Remove(additionalNotice); Children.Add(additionalNotice); }
        if(profile.Protocol==Protocol.VNC)terminal.IsVisible=false;
        BuildRestoredNotice(); Grid.SetRow(restoredNotice,2); Children.Add(restoredNotice);
        Grid.SetRow(state,3); Children.Add(state);
        BuildTransferQueue();
        BuildOperationStatus(); BuildFiles(); BuildInfo(); DesignSystem.Resource(cpuChart,Polyline.StrokeProperty,"AccentPrimary");
        Loaded+=(_,_)=>_ = ApplyAppearanceAsync(); terminal.Loaded+=(_,_)=>_ = ApplyAppearanceAsync();
        terminal.ProcessExited+=(_,e)=> { if(Profile.Protocol==Protocol.RDP && disposed==0 && !terminal.IsLive && !stoppingRdp) { terminal.IsVisible=false; RdpEnded(e.ExitCode,true); } };
        foreach(var child in actions.Children)child.Margin=new Thickness(0,2,DesignTokens.Small,2); UpdateConnectionActions();
    }
    private void SetStatus(string text,string glyph,SessionVisualState? visual=null)
    { VisualState=visual??(glyph switch { "●"=>SessionVisualState.Connected,"◌"=>SessionVisualState.Connecting,"↗"=>SessionVisualState.ExternalClient,"○"=>SessionVisualState.Closed,_=>SessionVisualState.Idle }); state.Text=text; ConnectionStatus=text; SetAndRaise(SessionTitleProperty,ref sessionTitle,$"{glyph} {Profile.Name}"); UpdateConnectionActions(); StatusChanged?.Invoke(); }
    private Button Button(string label,Func<Task> action)=>Dialogs.Button(label,()=>_ = owner.Safe(action));
    public async Task ConnectAsync(string secret,CancellationToken outer)
    {
        using var waiting=CancellationTokenSource.CreateLinkedTokenSource(outer,lifetime.Token);
        await connectionGate.WaitAsync(waiting.Token);
        try { await ConnectCoreAsync(secret,outer); }
        catch(Exception ex) when(cancelRequested || outer.IsCancellationRequested || lifetime.IsCancellationRequested)
        {
            await ResetConnectionsAsync();
            if(disposed==0)SetStatus(L.T("Anulowano łączenie · ")+Profile.Endpoint,"○",SessionVisualState.Idle);
            if(outer.IsCancellationRequested || lifetime.IsCancellationRequested)throw new OperationCanceledException(L.T("Anulowano łączenie."),ex,outer.IsCancellationRequested?outer:lifetime.Token);
        }
        catch(Exception ex) { await ResetConnectionsAsync(); if(Profile.Protocol==Protocol.RDP)rdpError=ex.Message; if(disposed==0)MarkFailed(); throw; }
        finally { connecting=false; RenderOperationStatus(); connectionGate.Release(); }
        if(Profile.Protocol==Protocol.SSH&&IsConnected)_=WarmCommandDiscoveryAsync();
        if(IsConnected || IsRdpLoggedIn)await RecordSuccessfulConnectionAsync();
    }
    private void CancelConnection()
    {
        if(cancelRequested || IsConnected || IsRdpLoggedIn || !connecting && embeddedRdp is not { IsRunning:true,IsReconnecting:false })return;
        cancelRequested=true; connectionLifetime.Cancel();
        if(!connecting) { ResetConnections(); SetStatus(L.T("Anulowano łączenie · ")+Profile.Endpoint,"○",SessionVisualState.Idle); }
        RenderOperationStatus();
    }
    private async Task ConnectCoreAsync(string secret,CancellationToken outer)
    {
        if(disposed!=0)throw new ObjectDisposedException(nameof(SessionPane));
        cancelRequested=false; connectionAttempt++;
        HasAttemptedConnection=true; restoredNotice.IsVisible=false;
        if(additionalNotice!=null)additionalNotice.IsVisible=true;
        await ResetConnectionsAsync();
        lifetime.Token.ThrowIfCancellationRequested(); outer.ThrowIfCancellationRequested();
        connectionLifetime.Dispose(); connectionLifetime=CancellationTokenSource.CreateLinkedTokenSource(outer,lifetime.Token);
        connecting=true; using var busy=BeginBusy(L.T("Łączenie z ")+Profile.Endpoint+"…");
        SetStatus(L.T("Łączenie · ")+Profile.Endpoint,"◌");
        this.secret=secret;
        var ct=connectionLifetime.Token;
        var preferences=await repository.PreferencesAsync(ct);
        if(int.TryParse(await repository.SettingAsync("terminalFont",ct),out var font)) terminal.FontSize=Math.Clamp(font,9,32);
        if(int.TryParse(await repository.SettingAsync("monitorInterval",ct),out var seconds)) { interval.Value=Math.Clamp(seconds,2,300); interval.Text=interval.Value.Value.ToString("0"); }
        if(Profile.Protocol==Protocol.RDP)
        {
            terminal.IsVisible=false; rdpError=""; rdpHostUnavailable=false;
            if(rdpNotice!=null)rdpNotice.IsVisible=true;
            await LaunchRdpAsync(); return;
        }
        if(Profile.Protocol is Protocol.VNC or Protocol.Serial) { await ConnectAdditionalAsync(ct); return; }
        terminal.IsVisible=true; files.IsVisible=info.IsVisible=false; infoActive=false; SelectPanel(0);
        var client=await factory.SshAsync(Profile,secret,ct);
        try { ct.ThrowIfCancellationRequested(); terminal.ApplyTemplate(); terminalSearch?.Reset(); terminal.Terminal.Reset(); ApplyTerminalConnectionPreferences(preferences); TerminalAppearance.Apply(terminal,await repository.SettingAsync("terminalPalette",ct)??TerminalAppearance.Names[0],(int)terminal.FontSize,preferences.TerminalFontFamily); session=await SshTerminalSession.CreateAsync(client,ct); terminal.AttachConnection(session); commandConnectionId=Guid.NewGuid(); session.Resize(terminal.Terminal.Cols,terminal.Terminal.Rows); terminal.Focus(); connectionStarted=Stopwatch.GetTimestamp(); SetStatus(L.F($"● SSH · {Profile.Username}@{Profile.Host}:{Profile.Port} · połączono"),"●"); _=ObserveEndAsync(session); if(owner.Preferences.ShowMetrics && owner.Preferences.ShowStatusBar)_=StartMonitoringAsync(ct); }
        catch { client.Dispose(); throw; }
    }
    public Task ReconnectAsync(string? replacementSecret=null) => ConnectAsync(replacementSecret??secret,lifetime.Token);
    private async Task ObserveEndAsync(SshTerminalSession observed)
    {
        var result=await observed.Completion;
        await Dispatcher.UIThread.InvokeAsync(()=>
        {
            if(disposed!=0 || !ReferenceEquals(session,observed))return;
            // An ended terminal must also stop its monitoring and file connections.
            ResetConnections();
            SetStatus(result.Reason==TerminalEndReason.TransportError?L.F($"Utracono połączenie · {Profile.Host} ({result.ErrorType}). Użyj Połącz ponownie."):L.F($"Zakończono sesję · {Profile.Host}. Użyj Połącz ponownie."),"○",result.Reason==TerminalEndReason.TransportError?SessionVisualState.Failed:SessionVisualState.Closed);
        });
    }
    private void ResetConnections()
    {
        commandConnectionId=Guid.Empty; commandEnvironment=null; commandDiscovery=null;
        CancelFileViews();
        connectionLifetime.Cancel();
        var oldSsh=session; var oldSftp=sftp; var oldMonitor=monitor; var oldMonitorLoop=monitorLoop;
        var oldRdp=rdpProcess; var oldVnc=vncSession; var oldSerial=serialSession;
        session=null; sftp=null; monitor=null; monitorLoop=Task.CompletedTask; ResetFileNavigation();
        // Kill an owned PTY before detaching it; Kill requires a realized terminal template.
        if(Profile.Protocol==Protocol.RDP && terminal.IsLive)terminal.Kill();
        var detached=terminal.DetachConnection();
        ResetAdditional();
        if(Profile.Protocol==Protocol.RDP)detached?.Dispose();
        cpuHistory.Clear();
        ClearMetrics();
        if(Profile.Protocol==Protocol.RDP) { rdpProcess?.Dispose(); rdpProcess=null; ResetEmbeddedRdp(); }
        // SSH.NET channel shutdown can wait on network timeouts. Keep it off the UI,
        // but retain the tasks so reconnect and application exit can drain them.
        var stopping=new List<Task> { oldMonitorLoop };
        if(oldSsh!=null) { stopping.Add(Task.Run(oldSsh.Dispose)); stopping.Add(oldSsh.Completion); }
        if(oldSftp!=null)stopping.Add(oldSftp.DisposeAsync().AsTask());
        if(oldMonitor!=null)stopping.Add(Task.Run(oldMonitor.Dispose));
        if(oldRdp!=null)stopping.Add(oldRdp.StopAsync());
        if(oldVnc!=null)stopping.Add(oldVnc.Shutdown);
        if(oldSerial!=null)stopping.Add(oldSerial.Completion);
        if(!transportShutdown.IsCompletedSuccessfully)stopping.Add(transportShutdown);
        transportShutdown=Task.WhenAll(stopping);
    }
    private async Task ResetConnectionsAsync()
    {
        ResetConnections();
        await Task.WhenAll(transferTasks.Concat(fileViewTasks).Append(transportShutdown));
    }
    private async Task LaunchRdpAsync()
    {
        var ct=connectionLifetime.Token; ct.ThrowIfCancellationRequested();
        if(rdpProcess is { IsRunning:true } || terminal.IsLive) { state.Text=L.T("Klient RDP jest już uruchomiony w osobnym oknie."); return; }
        try
        {
            using var busy=BeginBusy(L.T("Sprawdzanie dostępności RDP i NLA…"));
            var endpoint=await rdp.CheckAsync(Profile,ct); if(endpoint!=null)RenderRdpEndpoint(endpoint);
        }
        catch(Exception ex) { LastRdpEndpoint=null; rdpCertificate.Text=""; rdpError=ex.Message; rdpDiagnostic.Text=ex.Message; DesignSystem.Resource(rdpDiagnostic,TextBlock.ForegroundProperty,"Danger"); throw; }
        ct.ThrowIfCancellationRequested();
        if(UseEmbeddedRdp) { await LaunchEmbeddedRdpAsync(ct); return; }
        var launch=rdp.Prepare(Profile,Profile.Rdp with { Fullscreen=fullscreen.IsChecked==true,Clipboard=clipboard.IsChecked==true });
        if(rdpNotice!=null)rdpNotice.IsVisible=!launch.InteractiveConsole;
        if(launch.InteractiveConsole)
        { terminal.IsVisible=true; await terminal.LaunchProcess(null,launch.Executable,launch.Arguments); }
        else
        {
            rdpProcess=RdpProcessSession.Start(launch);
        }
        ct.ThrowIfCancellationRequested(); SetStatus(L.F($"RDP · {Profile.Host}:{Profile.Port} · klient uruchomiony w osobnym oknie"), "↗");
        if(rdpProcess is {} started)_=ObserveRdpExitAsync(started);
    }
    private async Task ObserveRdpExitAsync(RdpProcessSession observed)
    {
        try
        {
            var code=await observed.Completion;
            await Dispatcher.UIThread.InvokeAsync(()=> { if(disposed==0 && ReferenceEquals(rdpProcess,observed))RdpEnded(code,false); });
        }
        catch(Exception) { await Dispatcher.UIThread.InvokeAsync(()=> { if(disposed==0 && ReferenceEquals(rdpProcess,observed))SetStatus(L.T("Klient RDP zakończony — nie udało się posprzątać pliku konfiguracji"),"○"); }); }
        finally { observed.Dispose(); }
    }
    public void MarkFailed() => SetStatus(Profile.Protocol switch
    {
        Protocol.Serial=>L.T("Nie otwarto portu · sprawdź urządzenie i parametry, następnie Połącz ponownie"),
        Protocol.VNC=>L.T("Nie połączono VNC · sprawdź adres i uwierzytelnianie, następnie Połącz ponownie"),
        Protocol.RDP=>rdpHostUnavailable?L.T("Błąd osadzenia RDP · użyj Połącz ponownie lub Otwórz w osobnym oknie"):L.T("Nie uruchomiono klienta RDP — sprawdź dostępność klienta i ustawienia, następnie Połącz ponownie"),
        _=>L.T("Połączenie nie powiodło się — użyj Połącz ponownie lub Opcje → Poświadczenia")
    },"○",SessionVisualState.Failed);
    internal async Task ShowAsync(int panel)
    {
        if(connecting)throw new InvalidOperationException(L.T("Poczekaj na zakończenie łączenia."));
        if(Profile.Protocol!=Protocol.SSH)throw new InvalidOperationException(L.T("SFTP i monitoring są dostępne w sesji SSH."));
        if(!IsConnected)throw new InvalidOperationException(L.T("Najpierw połącz sesję SSH."));
        var ct=connectionLifetime.Token;
        using var busy=BeginBusy(panel==1?L.T("Wczytywanie SFTP…"):panel==2?L.T("Uruchamianie monitoringu…"):L.T("Przełączanie terminala…"));
        await connectionGate.WaitAsync(ct);
        try {
        ct.ThrowIfCancellationRequested(); terminal.IsVisible=panel==0; files.IsVisible=panel==1; info.IsVisible=panel==2; infoActive=panel==2; SelectPanel(panel);
        if(panel==1)
        {
            if(sftp==null)
            {
                var defaults=await repository.PreferencesAsync(ct);
                var startup=fileDefaultsApplied?null:await FileBrowserStartup.ResolveAsync(defaults,Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),ct);
                var client=await factory.SftpAsync(Profile,secret,ct); if(ct.IsCancellationRequested) { client.Dispose(); return; }
                sftp=new SftpSession(client); remoteHome=sftp.WorkingDirectory; remotePath.Text=remoteHome;
                if(startup!=null)ApplyFileDefaults(defaults,startup);
            }
            await RefreshAsync();
        }
        if(panel==2 && monitor==null)
        {
            await EnsureMonitoringAsync(ct);
        }
        } finally { connectionGate.Release(); }
    }
    private void BuildFiles()
    {
        var bar=new WrapPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(8,6) }; Grid.SetColumnSpan(bar,3); files.Children.Add(bar);
        bar.Children.Add(Button("Odśwież",RefreshAsync)); bar.Children.Add(hidden); bar.Children.Add(fileTarget); hidden.IsCheckedChanged+=(_,_)=>FilterFiles();
        uploadSelection=Button("Wyślij",UploadAsync); downloadSelection=Button("Pobierz",DownloadAsync);
        bar.Children.Add(uploadSelection); bar.Children.Add(downloadSelection); var operations=new Button { Content=DeskIcons.Labelled("more",L.T("Operacje")),Tag="Operacje" }; operations.ContextMenu=new ContextMenu { ItemsSource=new[]{OperationMenu("Nowy katalog",MkdirAsync),SingleFileMenu(null,"Zmień nazwę",RenameAsync),SingleFileMenu(null,"Przenieś do katalogu",MoveFileAsync),LinkFileMenu(null),SingleFileMenu(true,"Utwórz zdalne dowiązanie…",CreateRemoteLinkAsync),SingleFileMenu(true,"Uprawnienia chmod (zdalny)",ChmodAsync),SingleFileMenu(null,"Szczegóły",FileDetailsAsync),DeleteSelectionMenu(null)} }; operations.Click+=(_,_)=> { UpdateFileSelection(); operations.ContextMenu.Open(operations); }; bar.Children.Add(operations);
        foreach(var control in bar.Children)control.Margin=new Thickness(0,2,6,2);

        localPath.Name="SftpLocalPath"; remotePath.Name="SftpRemotePath";
        foreach(var remote in new[]{false,true})
        {
            var path=remote?remotePath:localPath;
            ToolTip.SetTip(path,L.T("Enter: otwórz katalog · Escape: przywróć otwarty katalog."));
            path.KeyDown+=(_,e)=>
            {
                if(e.Key==Avalonia.Input.Key.Enter) { e.Handled=true; _=owner.Safe(()=>NavigateFilesAsync(remote,path.Text??"")); }
                else if(e.Key==Avalonia.Input.Key.Escape && (remote?loadedRemotePath:loadedLocalPath).Length>0)
                {
                    e.Handled=true; if(remote)remoteNavigationTarget=null; else localNavigationTarget=null;
                    path.Text=remote?loadedRemotePath:loadedLocalPath; (remote?remoteList:localList).Focus();
                }
            };
            path.PropertyChanged+=(_,e)=> { if(e.Property==TextBox.TextProperty)UpdateFileNavigation(); };
        }
        ConfigureFileTables(); BuildFileFilters(); BuildFileSelection();
        Grid.SetRow(localList,4); files.Children.Add(localList); Grid.SetRow(remoteList,4); Grid.SetColumn(remoteList,2); files.Children.Add(remoteList);
        localList.DoubleTapped+=(_,_)=>_ = owner.Safe(()=>OpenDirectoryAsync(false));
        remoteList.DoubleTapped+=(_,_)=>_ = owner.Safe(()=>OpenDirectoryAsync(true));
        DragSupport.Source(localList,FileFormat,()=>CaptureFileDrag(false));
        DragSupport.Source(remoteList,FileFormat,()=>CaptureFileDrag(true));
        foreach(var list in new[] { localList,remoteList })
        {
            DragDrop.SetAllowDrop(list,true);
            DragDrop.AddDragOverHandler(list,(_,e)=> { var dragged=e.DataTransfer.TryGetValue(FileFormat); e.DragEffects=IsConnected && fileRefreshes==0 && FilePathConfirmed(list==remoteList) && (dragged!=null && dragged.Session==this && dragged.Entries.Count>0 && !dragged.Entries.Any(f=>f.IsSymbolicLink) && dragged.Remote!=(list==remoteList) || list==remoteList && e.DataTransfer.Contains(DataFormat.File))?DragDropEffects.Copy:DragDropEffects.None; e.Handled=true; });
            DragDrop.AddDropHandler(list,(_,e)=>
            {
                if(!IsConnected) { e.Handled=true; return; }
                var dragged=e.DataTransfer.TryGetValue(FileFormat);
                if(dragged!=null && dragged.Session==this && dragged.Entries.Count>0 && !dragged.Entries.Any(f=>f.IsSymbolicLink))
                {
                    if(list==remoteList && !dragged.Remote) _ = owner.Safe(()=>UploadFilesAsync(dragged.Entries));
                    if(list==localList && dragged.Remote) _ = owner.Safe(()=>DownloadFilesAsync(dragged.Entries));
                }
                else if(list==remoteList)
                {
                    var entries=new List<FileEntry>();
                    foreach(var storage in e.DataTransfer.TryGetFiles()??[])
                    {
                        var path=storage.TryGetLocalPath(); if(path==null)continue; var file=new FileInfo(path);
                        entries.Add(new(Path.GetFileName(path),path,Directory.Exists(path),file.LinkTarget!=null,0,DateTime.Now,""));
                    }
                    if(entries.Count>0)_ = owner.Safe(()=>UploadFilesAsync(entries));
                }
                e.Handled=true;
            });
        }
    }
    private async Task RefreshAsync()
    {
        var source=sftp; if(source==null) return;
        var ct=connectionLifetime.Token; var pathInput=localPath.Text??""; var remoteInput=remotePath.Text??"/";
        var localTarget=localNavigationTarget; var remoteTarget=remoteNavigationTarget;
        var entered=false; fileRefreshes++; UpdateFileNavigation();
        using var busy=BeginBusy(L.T("Wczytywanie listy plików…"));
        try {
        await refreshGate.WaitAsync(ct); entered=true;
        ct.ThrowIfCancellationRequested();
        if(!ReferenceEquals(sftp,source) || remotePath.Text!=remoteInput || localPath.Text!=pathInput)return;
        var path=pathInput;
        var remote=remoteInput;
        if(string.IsNullOrWhiteSpace(path)||string.IsNullOrWhiteSpace(remote))throw new ArgumentException(L.T("Podaj ścieżkę lokalnego i zdalnego katalogu."));
        path=Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        remote=await source.ResolveDirectoryAsync(remote,ct);
        var localResult=await Task.Run(()=>
        {
            var result=new List<FileEntry>();
            foreach(var x in new DirectoryInfo(path).EnumerateFileSystemInfos())
            { ct.ThrowIfCancellationRequested(); result.Add(new(x.Name,x.FullName,x is DirectoryInfo,x.LinkTarget!=null,x is FileInfo f && x.LinkTarget==null?f.Length:0,x.LastWriteTime,"")); }
            return result;
        },ct);
        if(Directory.GetParent(path) is {} parent) localResult.Insert(0,new("..",parent.FullName,true,false,0,DateTime.MinValue,""));
        var remoteResult=await source.ListAsync(remote,ct); ct.ThrowIfCancellationRequested();
        if(!ReferenceEquals(sftp,source) || remotePath.Text!=remoteInput || localPath.Text!=pathInput)return;
        localPath.Text=path; remotePath.Text=remote;
        if(remote!="/")remoteResult.Insert(0,new("..",RemotePath.Parent(remote),true,false,0,DateTime.MinValue,""));
        localEntries=localResult; remoteEntries=remoteResult;
        // A background view for a new directory must not leave actionable rows
        // from the previous directory under the newly displayed path.
        if(loadedLocalPath!=path)localList.ItemsSource=null;
        if(loadedRemotePath!=remote)remoteList.ItemsSource=null;
        if(loadedLocalPath!=path)localFilter.Text=""; if(loadedRemotePath!=remote)remoteFilter.Text="";
        loadedLocalPath=path; loadedRemotePath=remote;
        localNavigation.Commit(path,localTarget); remoteNavigation.Commit(remote,remoteTarget); localNavigationTarget=remoteNavigationTarget=null;
        await FilterFilesAsync();
        } catch {
            if(ReferenceEquals(sftp,source))
            {
                if(localPath.Text==pathInput && loadedLocalPath.Length>0) { localPath.Text=loadedLocalPath; localNavigationTarget=null; }
                if(remotePath.Text==remoteInput && loadedRemotePath.Length>0) { remotePath.Text=loadedRemotePath; remoteNavigationTarget=null; }
                if(FilePathConfirmed(false) && FilePathConfirmed(true))FilterFiles();
            }
            throw;
        } finally { if(entered)refreshGate.Release(); fileRefreshes--; UpdateFileNavigation(); }
    }
    private bool RemoteSelected => fileTarget.SelectedIndex==1;
    private async Task MkdirAsync()
    {
        var remote=RemoteSelected; var directory=FileDirectory(remote);
        var name=await Dialogs.Prompt(owner,L.T("Nazwa nowego katalogu")); if(name==null || sftp==null)return;
        if(remote) await sftp.CreateDirectoryAsync(RemotePath.Join(directory,name),lifetime.Token);
        else { ValidateLocalName(name); Directory.CreateDirectory(Path.Combine(directory,name)); } await RefreshAsync();
    }
    private async Task RenameAsync()
    {
        var remote=RemoteSelected; if(SingleFile(remote) is not {} f || sftp==null)return;
        var name=await Dialogs.Prompt(owner,L.T("Nowa nazwa"),f.Name); if(name==null)return;
        if(remote) await sftp.RenameAsync(f.FullPath,RemotePath.Join(RemotePath.Parent(f.FullPath),name),lifetime.Token);
        else { ValidateLocalName(name); var target=Path.Combine(Path.GetDirectoryName(f.FullPath)!,name); if(f.IsDirectory) Directory.Move(f.FullPath,target); else File.Move(f.FullPath,target); } await RefreshAsync();
    }
    private async Task ChmodAsync()
    {
        if(SingleFile(true) is not {} f || sftp==null)return;
        var mode=await Dialogs.Prompt(owner,L.T("Uprawnienia ósemkowe, np. 644"),f.Permissions); if(mode==null)return;
        if(mode.Length is < 3 or > 4 || mode.Any(c=>c<'0'||c>'7')) throw new ArgumentException(L.T("Niepoprawne uprawnienia."));
        await sftp.ChmodAsync(f.FullPath,Convert.ToInt16(mode,8),lifetime.Token); await RefreshAsync();
    }
    private static void ValidateLocalName(string name)=>LocalFileName.Validate(name);
    private Task UploadAsync()=>UploadFilesAsync(SelectedFiles(false));
    private Task UploadEntryAsync(FileEntry entry)=>UploadFilesAsync([entry]);
    private Task DownloadAsync()=>DownloadFilesAsync(SelectedFiles(true));
    private Task DownloadEntryAsync(FileEntry entry)=>DownloadFilesAsync([entry]);
    private async Task<TransferOutcome> UploadTree(SftpSession connection,string local,string remote,IProgress<FileTransferUpdate> report,CancellationToken ct,TransferTraversal traversal)
    {
        traversal.Visit(ct);
        var source=await Task.Run(()=>LocalTransferSource.Read(local),ct);
        if(source.IsSymbolicLink)return new(Skipped:1);
        if(source.IsDirectory)
        {
            using var directory=traversal.EnterDirectory(source.FullPath);
            await connection.EnsureTransferDirectoryAsync(remote,ct);
            var outcome=new TransferOutcome(Directories:1);
            var children=await Task.Run(()=>
            {
                var items=new DirectoryInfo(local).EnumerateFileSystemInfos().Take(TransferTraversal.MaximumEntries+1).ToArray();
                if(items.Length>TransferTraversal.MaximumEntries)throw new IOException(L.T("Transfer przekracza limit 20 000 elementów lub 64 poziomów. Prześlij mniejsze grupy."));
                return items;
            },ct);
            foreach(var child in children)outcome+=await UploadTree(connection,child.FullName,RemotePath.Join(remote,child.Name),report,ct,traversal);
            return outcome;
        }
        else
        {
            var exists=await connection.ExistsAsync(remote,ct); if(exists && !await Dialogs.Confirm(owner,L.T("Nadpisać zdalny plik?"),remote,ct))return new(Skipped:1);
            await connection.UploadAsync(local,remote,exists,new ForwardProgress<TransferProgress>(p=>report.Report(new(Path.GetFileName(local),p))),ct);
            return new(Files:1);
        }
    }
    private async Task<TransferOutcome> DownloadTree(SftpSession connection,FileEntry entry,string local,IProgress<FileTransferUpdate> report,CancellationToken ct,TransferTraversal traversal)
    {
        traversal.Visit(ct); if(entry.IsSymbolicLink)return new(Skipped:1);
        entry=await connection.ReadTransferSourceAsync(entry,false,ct);
        if(entry.IsDirectory)
        {
            using var directory=traversal.EnterDirectory(entry.FullPath);
            if(new DirectoryInfo(local).LinkTarget!=null)throw new IOException(L.T("Katalog docelowy jest dowiązaniem. Wybierz zwykły katalog."));
            Directory.CreateDirectory(local);
            var outcome=new TransferOutcome(Directories:1);
            foreach(var child in await connection.ListTransferChildrenAsync(entry.FullPath,ct))outcome+=await DownloadTree(connection,child,LocalFileName.InDirectory(local,child.Name),report,ct,traversal);
            return outcome;
        }
        else
        {
            var exists=File.Exists(local); if(exists && !await Dialogs.Confirm(owner,L.T("Nadpisać lokalny plik?"),local,ct))return new(Skipped:1);
            await connection.DownloadAsync(entry.FullPath,local,exists,new ForwardProgress<TransferProgress>(p=>report.Report(new(entry.Name,p))),ct);
            return new(Files:1);
        }
    }
    private Task CancelTransfersAsync() { foreach(var ct in transfers.ToArray())ct.Cancel(); return Task.CompletedTask; }
    private void BuildInfo()
    {
        BuildInfoLayout();
    }
    private void RenderDetails(ServerSnapshot snapshot)
    {
        static string Bytes(long bytes)=>$"{bytes/1073741824d:N1} GiB";
        topology.Text=snapshot.Topology is {} t?L.F($"CPU: {t.Sockets} gniazd · {t.Cores} rdzeni · {t.Threads} wątków"):L.T("Topologia CPU: dane niedostępne");
        diskRows.Children.Clear();
        foreach(var disk in snapshot.DiskDetails)
        {
            var label=Dialogs.Text($"{disk.MountPoint} · {disk.FileSystem} · {disk.Device}");
            var stats=Dialogs.Text(L.F($"{disk.Percent:N0}% zajęte · {Bytes(disk.Used)} używane / {Bytes(disk.Total)} łącznie · {Bytes(disk.Available)} dostępne"),12);
            var bar=new ProgressBar { Minimum=0,Maximum=100,Value=disk.Percent,Height=7 };
            if(disk.Percent>=85) { var token=disk.Percent>=95?"Danger":"Warning"; DesignSystem.Resource(label,TextBlock.ForegroundProperty,token); DesignSystem.Resource(bar,ProgressBar.ForegroundProperty,token); }
            diskRows.Children.Add(new StackPanel { Spacing=DesignTokens.Small,Children={label,stats,bar} });
        }
        disks.IsVisible=snapshot.DiskDetails.Count==0; disks.Text=snapshot.Disks.Length>0?snapshot.Disks:L.T("Dane dysków niedostępne");
        networkRows.Children.Clear();
        foreach(var nic in snapshot.NetworkDetails.Interfaces)
        {
            var name=Dialogs.Text($"{nic.Name} · {nic.State} · MAC {nic.Mac}");
            var addresses=Dialogs.Text($"IPv4: {(nic.IPv4.Length>0?nic.IPv4:"—")}\nIPv6: {(nic.IPv6.Length>0?nic.IPv6:"—")}",12);
            networkRows.Children.Add(new StackPanel { Spacing=3,Children={name,addresses} });
        }
        networkSummary.Text=L.F($"Brama: {(snapshot.NetworkDetails.Gateways.Length>0?snapshot.NetworkDetails.Gateways:L.T("dane niedostępne"))}\nDNS: {(snapshot.NetworkDetails.Dns.Length>0?snapshot.NetworkDetails.Dns:L.T("dane niedostępne"))}");
        network.IsVisible=snapshot.NetworkDetails.Interfaces.Count==0; network.Text=snapshot.Network.Length>0?snapshot.Network:L.T("Dane interfejsów niedostępne");
    }
    private async Task MonitorAsync(CancellationToken ct)
    {
        var waitingForFirstSample=true;
        try
        {
            while(!ct.IsCancellationRequested)
            {
                if(IsActive && IsConnected && (infoActive || owner.Preferences.ShowMetrics && owner.Preferences.ShowStatusBar))
                {
                    LinuxMonitoring? source=null;
                    try
                    {
                        if(monitor==null)
                        {
                            await connectionGate.WaitAsync(ct);
                            try { await EnsureMonitoringAsync(ct); }
                            finally { connectionGate.Release(); }
                        }
                        source=monitor; if(source==null)return;
                        using var busy=waitingForFirstSample && infoActive?BeginBusy(L.T("Wczytywanie pierwszych danych serwera…")):null;
                        var snapshot=await source.SampleAsync(ct); ct.ThrowIfCancellationRequested();
                        if(!ReferenceEquals(monitor,source) || disposed!=0 || !IsConnected)return;
                        RenderMonitoringSnapshot(snapshot); waitingForFirstSample=false;
                    }
                    catch(Exception ex)
                    {
                        if(ct.IsCancellationRequested)return;
                        if(source!=null && ReferenceEquals(monitor,source)) { monitor=null; await Task.Run(source.Dispose); }
                        ClearMetrics(); MetricsError=L.F($"Parametry niedostępne ({ex.GetType().Name}). Ponowimy próbę automatycznie."); metricsNotice.Text=MetricsError; metricsNotice.IsVisible=true; StatusChanged?.Invoke();
                        waitingForFirstSample=true; await Task.Delay(TimeSpan.FromSeconds(30),ct);
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds((double)(interval.Value??5)),ct);
            }
        }
        catch(OperationCanceledException) { }
    }
    public override void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        lifetime.Cancel(); secret=""; ResetConnections(); terminalSearch?.Dispose(); terminal.Dispose();
        _=CompleteCloseAsync();
    }
    internal Task CloseAsync() { Dispose(); return closed.Task; }
}
