using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;
internal sealed partial class SessionPane
{
    private readonly TextBlock rdpSummary=DesignSystem.Label(L.T("Gotowy do uruchomienia pulpitu zdalnego."));
    private readonly TextBlock rdpDiagnostic=DesignSystem.Label(L.T("Sprawdzenie dostępności i NLA nastąpi przed uruchomieniem klienta."),true);
    private readonly TextBlock rdpCertificate=DesignSystem.Label("",true,12);
    private Button? rdpShow,rdpStop,rdpCheck;
    private Button? rdpExternal;
    private TextBlock? rdpHelp;
    private bool rdpHostUnavailable,rdpExternalOverride;
    private bool stoppingRdp;
    private string rdpError="";
    internal RdpEndpointInfo? LastRdpEndpoint { get; private set; }
    internal bool UseEmbeddedRdp=>OperatingSystem.IsWindows() && Profile.Rdp.DisplayMode==RdpDisplayMode.Embedded && !rdpExternalOverride;

    private void BuildRdpControls(WrapPanel actions)
    {
        terminal.IsVisible=false; fullscreen.IsChecked=Profile.Rdp.Fullscreen; clipboard.IsChecked=Profile.Rdp.Clipboard;
        fullscreen.IsVisible=!UseEmbeddedRdp; actions.Children.Add(fullscreen); actions.Children.Add(clipboard);
        rdpShow=Button("Pokaż okno",ShowRdpWindowAsync); rdpShow.Content=DeskIcons.Labelled("external-link",L.T("Pokaż okno"));
        rdpStop=Button("Rozłącz RDP",StopRdpAsync); rdpStop.Content=DeskIcons.Labelled("log-out",L.T("Rozłącz RDP"));
        rdpCheck=Button("Sprawdź serwer",CheckRdpAsync); rdpCheck.Content=DeskIcons.Labelled("network",L.T("Sprawdź serwer"));
        actions.Children.Add(rdpShow); actions.Children.Add(rdpStop); actions.Children.Add(rdpCheck);
        if(UseEmbeddedRdp)BuildRdpFileDrop(actions);
        if(UseEmbeddedRdp)rdpShow.Content=DeskIcons.Labelled("monitor",L.T("Pulpit"));
        var options=new Button { Content=DeskIcons.Create("more"),Tag="Opcje",Width=30,Classes={"chrome","icon"} }; optionsButton=options;
        ToolTip.SetTip(options,L.T("Opcje sesji RDP")); Avalonia.Automation.AutomationProperties.SetName(options,L.T("Opcje sesji RDP"));
        var menu=new List<MenuItem> { OperationMenu("Uruchom ponownie sesję",()=>owner.ReconnectAsync(this)) };
        if(UseEmbeddedRdp)menu.Add(OperationMenu("Poświadczenia",()=>UseEmbeddedRdp?owner.ReauthenticateAsync(this):Dialogs.Message(owner,L.T("Poświadczenia RDP"),L.T("Zmień dane logowania w oknie klienta Windows. Wybierz Pokaż okno, aby do niego przejść."))));
        options.ContextMenu=new ContextMenu { ItemsSource=menu }; options.Click+=(_,_)=>options.ContextMenu.Open(options); actions.Children.Add(options);
        var identity=RdpIdentity.Parse(Profile.Username,Profile.Domain);
        var details=DesignSystem.Label(L.F($"{Profile.Host}:{Profile.Port}\nUżytkownik: {(identity.WindowsUsername.Length>0?identity.WindowsUsername:L.T("wybierz w kliencie"))}\nRozdzielczość: {Profile.Rdp.Width} × {Profile.Rdp.Height}"),true);
        foreach(var text in new[]{rdpSummary,rdpDiagnostic,rdpCertificate,details}) { text.TextWrapping=TextWrapping.Wrap; text.TextTrimming=TextTrimming.CharacterEllipsis; }
        rdpHelp=DesignSystem.Label("",true,12); rdpHelp.TextWrapping=TextWrapping.Wrap;
        rdpExternal=Button("Otwórz w osobnym oknie",OpenExternalRdpAsync); rdpExternal.Content=DeskIcons.Labelled("external-link",L.T("Otwórz w osobnym oknie")); rdpExternal.HorizontalAlignment=HorizontalAlignment.Left; rdpExternal.Classes.Add("primary"); rdpExternal.IsVisible=false;
        var heading=DesignSystem.PageHeading("monitor",L.T("Pulpit zdalny"));
        rdpCertificate.IsVisible=false;
        var desktopCard=DesignSystem.Card(new StackPanel { Spacing=16,Children={heading,details,rdpSummary,rdpExternal,rdpDiagnostic,rdpCertificate,rdpHelp} });
        desktopCard.MaxWidth=760; desktopCard.Margin=new Thickness(24); desktopCard.VerticalAlignment=VerticalAlignment.Center;
        rdpNotice=new ScrollViewer { Content=desktopCard,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        Grid.SetRow(rdpNotice,2); Children.Add(rdpNotice);
    }
    private void UpdateRdpActions()
    {
        var active=HasActiveClient;
        if(rdpShow!=null)rdpShow.IsEnabled=active && !stoppingRdp;
        if(rdpStop!=null)rdpStop.IsEnabled=active && !connecting && !stoppingRdp;
        if(rdpCheck!=null)rdpCheck.IsEnabled=!IsBusy && !connecting && !stoppingRdp;
        fullscreen.IsEnabled=clipboard.IsEnabled=!active && !connecting && !stoppingRdp;
        fullscreen.IsVisible=!UseEmbeddedRdp;
        if(rdpFileDrop!=null) { rdpFileDrop.IsVisible=UseEmbeddedRdp; rdpFileDrop.IsEnabled=CanPrepareRdpFiles; }
        if(rdpExternal!=null) { rdpExternal.IsVisible=UseEmbeddedRdp && rdpHostUnavailable; rdpExternal.IsEnabled=!active && !connecting && !IsBusy && !stoppingRdp; }
        if(rdpShow!=null)
        {
            var label=UseEmbeddedRdp?L.T("Pulpit"):L.T("Pokaż okno"); rdpShow.Content=DeskIcons.Labelled(UseEmbeddedRdp?"monitor":"external-link",label);
            Avalonia.Automation.AutomationProperties.SetName(rdpShow,label); ToolTip.SetTip(rdpShow,label);
        }
        if(rdpHelp!=null)rdpHelp.Text=UseEmbeddedRdp?L.T("Pulpit otworzy się w tej zakładce. Możesz przenieść ją do drugiego panelu lub osobnego okna.\nCtrl+Alt+← / Ctrl+Alt+→ oddaje klawiaturę do ServerDesk; Ctrl+Alt+End otwiera zdalny ekran zabezpieczeń.\nPliki upuść na strefę schowka, a następnie wklej w folderze zdalnego Eksploratora.\nCertyfikat wymagający weryfikacji potwierdź w systemowym oknie Windows."):OperatingSystem.IsWindows()?L.T("Pulpit działa w osobnym oknie klienta Windows. Dane logowania i certyfikat potwierdź w jego oknie.\nStan uruchomionego klienta nie potwierdza zalogowania."):L.T("Login, hasło i certyfikat potwierdź w konsoli tej zakładki.\nPulpit działa w osobnym oknie FreeRDP. Stan klienta nie potwierdza zalogowania.");
        rdpSummary.Text=connecting?L.T("Uruchamianie klienta RDP…"):stoppingRdp?L.T("Zamykanie klienta RDP…"):active?(UseEmbeddedRdp?L.T("Logowanie RDP w zakładce. Sprawdź także systemowe okno certyfikatu."):L.T("Klient RDP działa. Logowanie i pulpit obsługuje jego okno.")):rdpError.Length>0?rdpError:HasAttemptedConnection?L.T("Klient RDP jest zamknięty. Wybierz Połącz ponownie."):L.T("Gotowy do uruchomienia pulpitu zdalnego.");
        DesignSystem.Resource(rdpSummary,TextBlock.ForegroundProperty,!active && rdpError.Length>0?"Danger":"ForegroundPrimary");
    }
    private async Task OpenExternalRdpAsync()
    {
        if(!UseEmbeddedRdp || !rdpHostUnavailable || HasActiveClient || IsBusy || connecting)return;
        rdpExternalOverride=true;
        await ReconnectAsync();
    }
    private void RenderRdpEndpoint(RdpEndpointInfo result)
    {
        LastRdpEndpoint=result; rdpDiagnostic.Text=L.F($"Serwer RDP dostępny · {result.RoundTrip.TotalMilliseconds:0} ms · {result.Security}");
        DesignSystem.Resource(rdpDiagnostic,TextBlock.ForegroundProperty,"Success");
        if(result.CertificateFingerprint!=null)
        {
            rdpCertificate.Text=L.F($"Certyfikat: {result.CertificateSubject}\nSHA-256: {result.CertificateFingerprint}\nWażny do: {result.CertificateExpires:yyyy-MM-dd}\n")+(result.CertificateErrors==System.Net.Security.SslPolicyErrors.None?L.T("Certyfikat zaufany przez system."):L.T("Certyfikat wymaga weryfikacji w kliencie. Nie wysłano danych logowania."));
            DesignSystem.Resource(rdpCertificate,TextBlock.ForegroundProperty,result.CertificateErrors==System.Net.Security.SslPolicyErrors.None?"ForegroundSecondary":"Warning");
        }
        else rdpCertificate.Text="";
        rdpCertificate.IsVisible=!string.IsNullOrWhiteSpace(rdpCertificate.Text);
        ToolTip.SetTip(rdpDiagnostic,rdpDiagnostic.Text);
    }
    private async Task CheckRdpAsync()
    {
        await connectionGate.WaitAsync(lifetime.Token);
        try
        {
            // A deliberate diagnostic replaces the restored placeholder, just as Connect does.
            // Keep the result visible without stacking the two cards or starting a session.
            if(restoredNotice.IsVisible) { restoredNotice.IsVisible=false; if(rdpNotice!=null)rdpNotice.IsVisible=true; }
            using var busy=BeginBusy(L.T("Sprawdzanie RDP i certyfikatu…"));
            var result=await new RdpEndpointProbe().CheckAsync(Profile,lifetime.Token,inspectCertificate:true); RenderRdpEndpoint(result);
        }
        catch(Exception ex) { LastRdpEndpoint=null; rdpCertificate.Text=""; rdpDiagnostic.Text=ex.Message; DesignSystem.Resource(rdpDiagnostic,TextBlock.ForegroundProperty,"Danger"); throw; }
        finally { connectionGate.Release(); }
    }
    private Task ShowRdpWindowAsync()
    {
        if(embeddedRdp is { IsRunning:true }) { embeddedRdp.FocusDesktop(); return Task.CompletedTask; }
        if(rdpProcess?.TryActivateWindow()==true)return Task.CompletedTask;
        if(terminal.IsLive) { terminal.Focus(); return Task.CompletedTask; }
        return Dialogs.Message(owner,L.T("Okno klienta RDP"),L.T("Okno może jeszcze się uruchamiać albo wyświetlać pytanie o logowanie/certyfikat. Wybierz je na pasku zadań Windows."));
    }
    private async Task StopRdpAsync()
    {
        if(!HasActiveClient || stoppingRdp)return;
        if(!await Dialogs.Confirm(owner,L.T("Rozłączyć RDP?"),L.T("Zamknąć klienta pulpitu zdalnego tej sesji? Niezapisane zmiany na serwerze mogą wymagać zapisania.")))return;
        await connectionGate.WaitAsync(lifetime.Token);
        try
        {
            stoppingRdp=true; using var busy=BeginBusy(L.T("Zamykanie klienta RDP…")); UpdateRdpActions();
            var process=rdpProcess; ResetConnections(); if(process!=null)await process.StopAsync();
            terminal.IsVisible=false; if(rdpNotice!=null)rdpNotice.IsVisible=true;
            SetStatus(L.T("RDP rozłączony · wybierz Połącz ponownie"),"○");
        }
        finally { stoppingRdp=false; UpdateConnectionActions(); connectionGate.Release(); }
    }
    private void RdpEnded(int code,bool console)
    {
        var result=RdpExitStatus.FromClient(code,console); rdpError=result.IsError?result.Message:"";
        if(rdpNotice!=null)rdpNotice.IsVisible=true;
        SetStatus(result.Message,"○",result.IsError?SessionVisualState.Failed:SessionVisualState.Closed);
    }
}
