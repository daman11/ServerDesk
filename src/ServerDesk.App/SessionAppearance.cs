using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ServerDesk.Core;

namespace ServerDesk.App;
internal sealed partial class SessionPane
{
    private StackPanel transferFooter=null!;
    private string connectionStatus=L.T("Gotowy");
    internal string ConnectionStatus { get=>(IsConnected || IsRdpLoggedIn) && connectionStarted!=0?ConnectedStatus():connectionStatus; private set=>connectionStatus=value; }
    internal int ActiveTransfers=>transfers.Count;
    internal string TransferStatus=>transfer.Text??L.T("Brak transferów");
    private readonly TextBlock localEmpty=DesignSystem.Label(L.T("Ten katalog jest pusty."),true);
    private readonly TextBlock remoteEmpty=DesignSystem.Label(L.T("Brak plików. Włącz „Ukryte”, aby pokazać wszystkie wpisy."),true);
    private readonly Dictionary<int,Button> panelButtons=[];
    internal int SelectedPanel { get; private set; }
    private Button PanelButton(string title,int panel)
    {
        var button=Button(title,()=>ShowAsync(panel)); panelButtons[panel]=button;
        button.Classes.Add("segment"); button.Classes.Add("chrome"); button.Classes.Set("active",panel==0); if(panel==2)button.Content=DeskIcons.Labelled("chart-no-axes-combined","Monitoring"); return button;
    }
    private void SelectPanel(int panel)
    { var changed=SelectedPanel!=panel; SelectedPanel=panel; foreach(var (id,button) in panelButtons) { button.Classes.Set("active",id==panel); button.FontWeight=id==panel?FontWeight.SemiBold:FontWeight.Normal; } if(changed)owner.QueueWorkspaceSave(); }
    internal void PrepareRestored(int panel)
    {
        restoredPanel=panel; SelectPanel(panel); terminal.IsVisible=files.IsVisible=info.IsVisible=false;
        if(rdpNotice!=null)rdpNotice.IsVisible=false;
        if(additionalNotice!=null)additionalNotice.IsVisible=false;
        restoredNotice.IsVisible=true; SetStatus(L.T("Karta przywrócona · wybierz Połącz"),"○",SessionVisualState.Idle); UpdateConnectionActions();
    }
    private void BuildRestoredNotice()
    {
        var name=DesignSystem.Label(Profile.Name,false,18); name.TextWrapping=TextWrapping.Wrap;
        var endpoint=DesignSystem.Label($"{Profile.Protocol} · {Profile.Endpoint}",true,12); endpoint.TextWrapping=TextWrapping.Wrap;
        var instruction=DesignSystem.Label(L.T("Wybierz Połącz, aby rozpocząć sesję.")); instruction.TextWrapping=TextWrapping.Wrap;
        var credentials=DesignSystem.Label(Profile.Protocol==Protocol.Serial?L.T("Port zostanie otwarty po wybraniu Połącz."):L.T("Poświadczenia pobierzemy z magazynu systemowego lub poprosimy o nie przy połączeniu."),true,12); credentials.TextWrapping=TextWrapping.Wrap;
        var card=DesignSystem.Card(new StackPanel { Spacing=16,Children={DesignSystem.PageHeading("history",L.T("Karta przywrócona")),new StackPanel { Spacing=4,Children={name,endpoint} },new StackPanel { Spacing=6,Children={instruction,credentials} }} });
        card.Name="RestoredSessionCard"; card.MaxWidth=560; card.Margin=new Thickness(24); card.VerticalAlignment=VerticalAlignment.Center;
        restoredNotice.Content=card; DesignSystem.Resource(restoredNotice,ScrollViewer.BackgroundProperty,"BackgroundPrimary");
    }
    internal async Task ShowRestoredPanelAsync()
    { var panel=restoredPanel; restoredPanel=null; if(panel is >0 && Profile.Protocol==Protocol.SSH)await ShowAsync(panel.Value); }
    internal override void FocusContent() { if(Profile.Protocol==Protocol.VNC && IsConnected)vncSurface?.Focus(); else if(terminal.IsVisible)terminal.Focus(); else if(files.IsVisible)remoteList.Focus(); else if(info.IsVisible)info.Focus(); else connectButton.Focus(); }
    private void UpdateConnectionActions()
    {
        if(connectButton==null)return;
        if(optionsButton!=null)optionsButton.IsEnabled=HasAttemptedConnection && !connecting;
        var live=HasActiveClient; var authenticated=IsConnected || IsRdpLoggedIn;
        var label=connecting?cancelRequested?L.T("Anulowanie…"):L.T("Łączenie…"):live?authenticated?L.T("Połączono"):UseEmbeddedRdp?embeddedRdp?.IsReconnecting==true?L.T("Ponawianie RDP…"):L.T("Logowanie RDP…"):L.T("Klient uruchomiony"):HasAttemptedConnection?L.T("Połącz ponownie"):L.T("Połącz");
        connectButton.Classes.Set("live",live); connectButton.Classes.Set("primary",!live);
        connectButton.Content=DeskIcons.Labelled(live?authenticated?"success":UseEmbeddedRdp?"circle-dashed":"external-link":HasAttemptedConnection?"refresh":"connect",label,16,live?authenticated?"Success":"Warning":null); connectButton.Tag=label; connectButton.IsEnabled=!live && !IsBusy && !connecting;
        Avalonia.Automation.AutomationProperties.SetName(connectButton,label); ToolTip.SetTip(connectButton,live?authenticated?L.T("Ponowne uruchomienie: Opcje → Uruchom ponownie sesję"):L.T("RDP jest uruchomiony. Logowanie może oczekiwać na potwierdzenie certyfikatu w Windows."):label);
        cancelConnectionButton.IsVisible=!authenticated && (connecting || embeddedRdp is { IsRunning:true,IsReconnecting:false }); cancelConnectionButton.IsEnabled=!cancelRequested;
        foreach(var button in panelButtons.Values)button.IsEnabled=!connecting && IsConnected;
        if(Profile.Protocol==Protocol.RDP)UpdateRdpActions();
        if(Profile.Protocol is Protocol.VNC or Protocol.Serial)UpdateAdditionalActions();
        if(Profile.Protocol==Protocol.SSH && uploadSelection!=null)UpdateFileSelection();
    }
    private readonly SemaphoreSlim appearanceGate=new(1);
    internal async Task ApplyAppearanceAsync()
    {
        if(disposed!=0)return;
        var acquired=false;
        try
        {
            await appearanceGate.WaitAsync(lifetime.Token); acquired=true;
            var palette=await repository.SettingAsync("terminalPalette",lifetime.Token);
            var font=await repository.SettingAsync("terminalFont",lifetime.Token);
            var proportions=await repository.SettingAsync("sftpLocalShare",lifetime.Token);
            var monitorInterval=await repository.SettingAsync("monitorInterval",lifetime.Token);
            var preferences=await repository.PreferencesAsync(lifetime.Token);
            if(disposed!=0)return;
            if(double.TryParse(proportions,System.Globalization.CultureInfo.InvariantCulture,out var share))
            { localShare=Math.Clamp(share,.2,.8); if(files.Bounds.Width>=700) { files.ColumnDefinitions[0].Width=new GridLength(localShare,GridUnitType.Star); files.ColumnDefinitions[2].Width=new GridLength(1-localShare,GridUnitType.Star); } AdaptFiles(); }
            TerminalAppearance.Apply(terminal,palette??TerminalAppearance.Names[0],int.TryParse(font,out var size)?size:14,preferences.TerminalFontFamily);
            ApplySessionPreferences(preferences);
            if(Profile.Protocol==Protocol.SSH && IsConnected && owner.Preferences.ShowMetrics && owner.Preferences.ShowStatusBar)_=StartMonitoringAsync(connectionLifetime.Token);
            if(int.TryParse(monitorInterval,out var seconds))interval.Value=Math.Clamp(seconds,2,300);
            appearanceWarning=""; RenderOperationStatus();
        }
        catch(OperationCanceledException) when(lifetime.IsCancellationRequested) { }
        catch(Exception ex)
        {
            if(disposed!=0)return;
            appearanceWarning=L.T("Nie udało się wczytać ustawień wyglądu. Spróbuj zapisać je ponownie.");
            owner.ReportBackgroundError(appearanceWarning,ex); RenderOperationStatus();
        }
        finally { if(acquired)appearanceGate.Release(); }
    }
    internal void ApplySessionPreferences(ApplicationPreferences value)=>state.IsVisible=value.ShowSessionStatus;
    private void ConfigureFileTables()
    {
        files.RowDefinitions=new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto");
        fileSplitter=new GridSplitter { ResizeDirection=GridResizeDirection.Columns,HorizontalAlignment=HorizontalAlignment.Stretch }; Grid.SetColumn(fileSplitter,1); Grid.SetRow(fileSplitter,1); Grid.SetRowSpan(fileSplitter,4); files.Children.Add(fileSplitter); fileSplitter.DragCompleted+=(_,_)=>_ = owner.Safe(async()=> { var total=files.ColumnDefinitions[0].ActualWidth+files.ColumnDefinitions[2].ActualWidth; if(total>0) { localShare=Math.Clamp(files.ColumnDefinitions[0].ActualWidth/total,.2,.8); await repository.SetSettingAsync("sftpLocalShare",localShare.ToString(System.Globalization.CultureInfo.InvariantCulture)); } });
        var upLocal=DeskIcons.Button("up","Lokalny katalog nadrzędny",()=>_ = owner.Safe(()=>NavigateParentAsync(false)));
        var upRemote=DeskIcons.Button("up","Zdalny katalog nadrzędny",()=>_ = owner.Safe(()=>NavigateParentAsync(true)));
        foreach(var pair in new[]{(localPath,upLocal,0),(remotePath,upRemote,2)})
        {
            var pathRow=new Grid { ColumnDefinitions=new ColumnDefinitions("Auto,*"),RowDefinitions=new RowDefinitions("Auto,Auto,Auto"),RowSpacing=6,Margin=new Thickness(6,6,6,4) };
            var heading=FilePanelHeading(pair.Item3==2); Grid.SetColumnSpan(heading,2); pathRow.Children.Add(heading); Grid.SetRow(pair.Item2,1); pathRow.Children.Add(pair.Item2);
            pathRow.GotFocus+=(_,_)=>fileTarget.SelectedIndex=pair.Item3==2?1:0;
            pathRow.AddHandler(Avalonia.Input.InputElement.PointerPressedEvent,(_,_)=>fileTarget.SelectedIndex=pair.Item3==2?1:0,Avalonia.Interactivity.RoutingStrategies.Tunnel);
            // Paths remain editable, serving as a compact navigation bar for deep directory hierarchies.
            Grid.SetColumn(pair.Item1,1); Grid.SetRow(pair.Item1,1); pathRow.Children.Add(pair.Item1);
            var crumbs=new StackPanel { Orientation=Orientation.Horizontal,Spacing=DesignTokens.Tight }; var crumbScroll=new ScrollViewer { Content=crumbs,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled }; Grid.SetColumnSpan(crumbScroll,2); Grid.SetRow(crumbScroll,2); pathRow.Children.Add(crumbScroll);
            void Breadcrumbs()
            {
                crumbs.Children.Clear(); var remote=pair.Item3==2; var path=pair.Item1.Text??"";
                foreach(var crumb in PathNavigation.Breadcrumbs(path,remote))
                {
                    if(crumbs.Children.Count>0)crumbs.Children.Add(DesignSystem.Label("/",true,DesignTokens.Micro));
                    var button=Dialogs.Button(crumb.Label,()=>_ = owner.Safe(async()=> { pair.Item1.Text=crumb.Path; await RefreshAsync(); }));
                    button.Classes.Add("icon"); button.MaxWidth=100; button.MinHeight=24; ToolTip.SetTip(button,crumb.Path); crumbs.Children.Add(button);
                }
            }
            pair.Item1.TextChanged+=(_,_)=>Breadcrumbs(); Breadcrumbs();
            Grid.SetRow(pathRow,1); Grid.SetColumn(pathRow,pair.Item3); files.Children.Add(pathRow);
        }
        BuildFileRows();
        foreach(var pair in new[]{(localEmpty,0),(remoteEmpty,2)}) { pair.Item1.HorizontalAlignment=HorizontalAlignment.Center; pair.Item1.VerticalAlignment=VerticalAlignment.Center; pair.Item1.MaxWidth=300; pair.Item1.TextWrapping=TextWrapping.Wrap; Grid.SetRow(pair.Item1,4); Grid.SetColumn(pair.Item1,pair.Item2); files.Children.Add(pair.Item1); }
        files.SizeChanged+=(_,_)=>AdaptFiles(); fileTarget.SelectionChanged+=(_,_)=> { AdaptFiles(); UpdateFileSelection(); };
    }
    private void AdaptFiles()
    {
        UpdateFilePanelHeadings();
        var narrow=files.Bounds.Width<700 && files.Bounds.Width>0; foreach(var child in files.Children.Where(c=>Grid.GetRow(c)>0 && Grid.GetColumn(c)!=1))child.IsVisible=!narrow || (RemoteSelected?Grid.GetColumn(child)==2:Grid.GetColumn(child)==0);
        localEmpty.IsVisible=!localList.Items.Cast<FileEntry>().Any(e=>e.Name!="..") && (!narrow || !RemoteSelected); remoteEmpty.IsVisible=!remoteList.Items.Cast<FileEntry>().Any(e=>e.Name!="..") && (!narrow || RemoteSelected);
        if(fileSplitter!=null)fileSplitter.IsVisible=!narrow;
        files.ColumnDefinitions[1].Width=new GridLength(narrow?0:4);
        if(narrow) { var remote=RemoteSelected; files.ColumnDefinitions[0].Width=remote?new GridLength(0):new GridLength(1,GridUnitType.Star); files.ColumnDefinitions[2].Width=remote?new GridLength(1,GridUnitType.Star):new GridLength(0); localList.IsVisible=!remote; remoteList.IsVisible=remote; }
        else { if(files.ColumnDefinitions[0].Width.Value==0 || files.ColumnDefinitions[2].Width.Value==0) { files.ColumnDefinitions[0].Width=new GridLength(localShare,GridUnitType.Star); files.ColumnDefinitions[2].Width=new GridLength(1-localShare,GridUnitType.Star); } localList.IsVisible=remoteList.IsVisible=true; }
    }
    private static string FormatBytes(long bytes)=>bytes>=1073741824?$"{bytes/1073741824d:0.0} GiB":bytes>=1048576?$"{bytes/1048576d:0.0} MiB":bytes>=1024?$"{bytes/1024d:0.0} KiB":$"{bytes} B";
    private void BuildInfoLayout()
    {
        var stack=new StackPanel { Margin=DesignTokens.PagePadding }; DesignSystem.Resource(stack,StackPanel.SpacingProperty,"SpacingLarge"); info.Content=stack;
        var header=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto,Auto"),ColumnSpacing=DesignTokens.Medium }; header.Children.Add(DesignSystem.PageHeading("chart-no-axes-combined",L.T("Monitoring")));
        var refresh=DesignSystem.Label(L.T("Odświeżanie [s]"),true,12); Grid.SetColumn(refresh,1); header.Children.Add(refresh); Grid.SetColumn(interval,2); header.Children.Add(interval); stack.Children.Add(header);
        metricsNotice.Name="MonitoringNotice"; metricsNotice.TextWrapping=TextWrapping.Wrap; metricsNotice.IsVisible=false; DesignSystem.Resource(metricsNotice,Avalonia.Controls.TextBlock.ForegroundProperty,"Warning"); stack.Children.Add(metricsNotice);
        Border Section(string glyph,string title,params Control[] content)
        { var body=new StackPanel(); DesignSystem.Resource(body,StackPanel.SpacingProperty,"SpacingMedium"); body.Children.Add(DeskIcons.Labelled(glyph,L.T(title),17,"AccentPrimary")); foreach(var control in content)body.Children.Add(control); return DesignSystem.Card(body); }
        stack.Children.Add(Section("server","System",system));
        var metrics=new Grid { ColumnDefinitions=new ColumnDefinitions("*,*"),ColumnSpacing=DesignTokens.Section };
        cpuChartHost=new Viewbox { Height=80,Stretch=Stretch.Fill,IsVisible=false,Child=new Canvas { Height=80,Width=480,ClipToBounds=true,Children={cpuChart} } };
        metrics.Children.Add(Section("settings","CPU",cpu,topology,cpuBar,cpuChartHost));
        var memory=Section("memory-stick","Pamięć RAM",ram,ramBar); Grid.SetColumn(memory,1); metrics.Children.Add(memory); stack.Children.Add(metrics);
        metrics.SizeChanged+=(_,_)=> { var narrow=metrics.Bounds.Width<780; metrics.ColumnDefinitions[1].Width=narrow?new GridLength(0):new GridLength(1,GridUnitType.Star); Grid.SetColumn(memory,narrow?0:1); Grid.SetRow(memory,narrow?1:0); if(metrics.RowDefinitions.Count==0)metrics.RowDefinitions=new RowDefinitions("Auto,Auto"); memory.Margin=narrow?new Thickness(0,12,0,0):new Thickness(0); };
        stack.Children.Add(Section("hard-drive","Dyski",diskRows,disks)); stack.Children.Add(Section("network","Sieć",networkSummary,networkRows,network));
    }
}
