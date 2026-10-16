using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class RdpPaneChecks
{
    private static Button Button(Window window,string name)=>window.GetVisualDescendants().OfType<Button>().Single(b=>AutomationProperties.GetName(b)==name);
    private static void Click(Button button)=>button.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
    internal static void Run(MainWindow main,ProfileRepository repo,string root)
    {
        var profile=new ServerProfile { Name="Windows administration",Protocol=Protocol.RDP,Host="rdp-fixture.invalid",Port=3389,Username="LAB\\Administrator" };
        var adapter=new OwnedProcessAdapter(); using var pane=new SessionPane(main,profile,new SshConnectionFactory(repo),adapter,repo);
        var window=new Window { Width=1100,Height=760,Content=pane }; window.Show(); RedesignChecks.Frames();
        if(window.GetVisualDescendants().OfType<Button>().Any(b=>b.Tag as string=="Uruchom RDP"))throw new Exception("RDP has duplicate launch control");
        if(Button(window,"Pokaż okno").IsEnabled || Button(window,"Rozłącz RDP").IsEnabled)throw new Exception("Idle RDP actions incorrectly enabled");
        window.CaptureRenderedFrame()!.Save(Path.Combine(root,"rdp-ready.png"));
        RedesignChecks.Wait(pane.ConnectAsync("",CancellationToken.None)); RedesignChecks.Frames();
        if(repo.HistoryAsync(profileId:profile.Id).GetAwaiter().GetResult().Count!=0)throw new Exception("External RDP process incorrectly entered successful history");
        if(!pane.HasActiveClient || !Button(window,"Pokaż okno").IsEnabled || !Button(window,"Rozłącz RDP").IsEnabled || pane.VisualState!=SessionVisualState.ExternalClient || adapter.Starts!=1)throw new Exception("RDP start state incorrect");
        if(pane.IsConnected || pane.LastRdpEndpoint?.SupportsNla!=true)throw new Exception("RDP process claimed SSH/desktop authentication or lost availability check");
        window.CaptureRenderedFrame()!.Save(Path.Combine(root,"rdp-client-running.png"));
        Click(Button(window,"Rozłącz RDP")); RedesignChecks.Frames(); var dialog=main.OwnedWindows.Single(); Click(Button(dialog,"Anuluj")); RedesignChecks.Frames();
        if(!pane.HasActiveClient)throw new Exception("Cancel disconnected owned RDP process");
        Click(Button(window,"Rozłącz RDP")); RedesignChecks.Frames(); dialog=main.OwnedWindows.Single(); Click(Button(dialog,"Potwierdź"));
        var until=DateTime.UtcNow.AddSeconds(6); while((pane.HasActiveClient || pane.IsBusy) && DateTime.UtcNow<until)RedesignChecks.Frames(1); RedesignChecks.Frames();
        if(pane.HasActiveClient || Button(window,"Rozłącz RDP").IsEnabled || pane.VisualState!=SessionVisualState.Closed)throw new Exception("RDP stop did not clear active state");
        RedesignChecks.Wait(pane.ReconnectAsync()); if(adapter.Starts!=2 || !pane.HasActiveClient)throw new Exception("RDP reconnect did not restart owned process");
        var previous=DesignSystem.Preferences;
        foreach(var width in new[]{520,1100})foreach(var font in new[]{13,16})
        {
            DesignSystem.ApplyPreferences(previous with { InterfaceFontSize=font }); window.Width=width; RedesignChecks.Frames();
            foreach(var button in window.GetVisualDescendants().OfType<Button>().Where(b=>b.IsEffectivelyVisible))
            { var point=button.TranslatePoint(default,window)!.Value; if(point.X<0 || point.X+button.Bounds.Width>window.ClientSize.Width+1)throw new Exception("RDP toolbar button escapes viewport"); }
            window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"rdp-{width}-font{font}.png"));
        }
        DesignSystem.ApplyPreferences(previous); pane.Dispose(); window.Close();
        using var failed=new SessionPane(main,profile,new SshConnectionFactory(repo),new FailingEndpointAdapter(),repo); window=new Window { Width=950,Height=650,Content=failed }; window.Show();
        try { RedesignChecks.Wait(failed.ConnectAsync("",CancellationToken.None)); throw new Exception("Unavailable RDP endpoint was launched"); } catch(TimeoutException) { }
        RedesignChecks.Frames(); if(failed.IsBusy || failed.VisualState!=SessionVisualState.Failed)throw new Exception("Failed RDP check left busy state");
        window.CaptureRenderedFrame()!.Save(Path.Combine(root,"rdp-unreachable.png")); failed.Dispose(); window.Close();
        var shutdown=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(new AppPaths()),new SshConnectionFactory(repo),new ExternalRdpClient()); shutdown.Show(); RedesignChecks.Frames(20);
        var owned=new SessionPane(shutdown,profile,new SshConnectionFactory(repo),new OwnedProcessAdapter(),repo); shutdown.RegisterPane(owned); RedesignChecks.Wait(owned.ConnectAsync("",CancellationToken.None));
        var child=(RdpProcessSession)typeof(SessionPane).GetField("rdpProcess",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(owned)!;
        shutdown.Close(); RedesignChecks.Frames(); Click(Button(shutdown.OwnedWindows.Single(),"Potwierdź")); RedesignChecks.Frames(2); shutdown.Close(); RedesignChecks.Frames(2);
        if(!child.Completion.IsCompleted && !shutdown.IsVisible)throw new Exception("Second close exited while owned RDP process was still running");
        until=DateTime.UtcNow.AddSeconds(8); while(shutdown.IsVisible && DateTime.UtcNow<until)RedesignChecks.Frames(1);
        if(shutdown.IsVisible || !child.Completion.IsCompleted)throw new Exception("Main shutdown did not wait for owned RDP process");
        Console.WriteLine("PASS RDP preflight/start/stop-confirmation-cancel/stop/reconnect, distinct external-client state, no duplicate launch button, unavailable endpoint recovery, 520/1100px font13/16 layout (owned test process, no desktop authentication)");
        Console.WriteLine("PASS application shutdown waits for owned RDP child and repeated close cannot bypass cleanup");
        var edit=Dialogs.Edit(main,profile,[]); RedesignChecks.Frames(); var editor=main.OwnedWindows.Single();
        PolishChecks.Page(editor,3); var display=editor.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="RdpDisplayMode"); display.SelectedIndex=1; display.BringIntoView(); RedesignChecks.Frames();
        var keyboard=editor.GetVisualDescendants().OfType<ComboBox>().Single(c=>c.Name=="RdpKeyboardMode"); keyboard.SelectedIndex=(int)RdpKeyboardMode.Remote;
        var reconnect=editor.GetVisualDescendants().OfType<CheckBox>().Single(c=>c.Content as string=="Ponawiaj po utracie sieci (do 5 prób, Windows)"); reconnect.IsChecked=true;
        var full=editor.GetVisualDescendants().OfType<CheckBox>().Single(c=>c.Content as string=="Pełny ekran");
        if(OperatingSystem.IsWindows() && full.IsEnabled)throw new Exception("External fullscreen option remains enabled for embedded desktop");
        editor.CaptureRenderedFrame()!.Save(Path.Combine(root,"rdp-embedded-settings.png")); Click(Button(editor,"Zapisz")); RedesignChecks.Frames();
        var edited=edit.GetAwaiter().GetResult(); if(edited?.Rdp.DisplayMode!=RdpDisplayMode.Embedded)throw new Exception("Profile editor lost embedded RDP selection");
        RedesignChecks.Wait(repo.SaveAsync(edited)); var listing=repo.ListAsync(); RedesignChecks.Wait(listing); var saved=listing.GetAwaiter().GetResult().Single(p=>p.Id==edited.Id);
        if(saved.Rdp.DisplayMode!=RdpDisplayMode.Embedded)throw new Exception("Repository lost editor RDP display selection");
        if(saved.Rdp.KeyboardMode!=RdpKeyboardMode.Remote || !saved.Rdp.AutoReconnect)throw new Exception("Editor/repository lost RDP keyboard/reconnect settings");
        Console.WriteLine("PASS embedded RDP selection in profile editor, fullscreen availability and persistence");
        if(OperatingSystem.IsWindows())EmbeddedFailure(main,repo,root,profile);
    }
    private static void EmbeddedFailure(MainWindow main,ProfileRepository repo,string root,ServerProfile profile)
    {
        var adapter=new OwnedProcessAdapter();
        using var pane=new SessionPane(main,profile with { Rdp=profile.Rdp with { DisplayMode=RdpDisplayMode.Embedded } },new SshConnectionFactory(repo),adapter,repo);
        var window=new Window { Width=1000,Height=760,Content=pane }; window.Show(); RedesignChecks.Frames();
        foreach(var width in new[]{240,520,1100})
        {
            window.Width=width; RedesignChecks.Frames();
            var zone=window.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="RdpFileDrop");
            if(zone.IsEnabled)throw new Exception("RDP file drop enabled before authenticated connection");
            var point=zone.TranslatePoint(default,window)!.Value;
            if(point.X<0 || point.X+zone.Bounds.Width>window.ClientSize.Width+1)throw new Exception("RDP file drop escapes narrow viewport");
            window.CaptureRenderedFrame()!.Save(Path.Combine(root,$"rdp-drop-{width}.png"));
        }
        window.Width=1000; RedesignChecks.Frames();
        // Headless has no native host: exercise the actual unavailable-host path.
        RedesignChecks.Wait(main.Safe(()=>pane.ConnectAsync("",CancellationToken.None))); RedesignChecks.Frames();
        var fallback=Button(window,"Otwórz w osobnym oknie");
        if(!fallback.IsVisible || !fallback.IsEnabled || pane.IsBusy || pane.HasActiveClient || pane.VisualState!=SessionVisualState.Failed)throw new Exception("Native host failure did not expose a usable recovery action");
        if(main.OwnedWindows.Count!=0)throw new Exception("Native host failure duplicated inline error with modal dialog");
        if(window.GetVisualDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("Unable to create child window")==true))throw new Exception("Native host error exposed an untranslated message");
        window.CaptureRenderedFrame()!.Save(Path.Combine(root,"rdp-host-recovery.png"));
        Click(fallback); var until=DateTime.UtcNow.AddSeconds(6); while(!pane.HasActiveClient && DateTime.UtcNow<until)RedesignChecks.Frames(1); RedesignChecks.Frames();
        if(!pane.HasActiveClient || pane.UseEmbeddedRdp || adapter.Starts!=1 || fallback.IsVisible || pane.Profile.Rdp.DisplayMode!=RdpDisplayMode.Embedded)throw new Exception("External recovery failed or changed saved profile mode");
        if(!Button(window,"Pokaż okno").IsEnabled || pane.VisualState!=SessionVisualState.ExternalClient)throw new Exception("External recovery toolbar/state remained in embedded mode");
        RedesignChecks.Wait(pane.CloseAsync()); window.Close();
        Console.WriteLine("PASS native host failure: Polish inline error, no duplicate modal, external recovery in same card, saved embedded mode retained");
    }
    internal static void Endpoint(MainWindow main,ProfileRepository repo,string root,string host)
    {
        var profile=new ServerProfile { Name="RDP endpoint",Protocol=Protocol.RDP,Host=host,Port=3389,Username="Damian" };
        using var pane=new SessionPane(main,profile,new SshConnectionFactory(repo),new ExternalRdpClient(),repo);
        var window=new Window { Width=1000,Height=780,Content=pane }; window.Show(); RedesignChecks.Frames(); Click(Button(window,"Sprawdź serwer"));
        var until=DateTime.UtcNow.AddSeconds(12); while(pane.IsBusy && DateTime.UtcNow<until)RedesignChecks.Frames(1); RedesignChecks.Frames();
        if(pane.LastRdpEndpoint is not { SupportsNla:true,CertificateFingerprint:not null } || pane.HasActiveClient)throw new Exception("Real RDP diagnostic did not inspect certificate without launching client");
        window.CaptureRenderedFrame()!.Save(Path.Combine(root,"rdp-endpoint.png")); pane.Dispose(); window.Close();
        Console.WriteLine("PASS real RDP/NLA endpoint and TLS certificate rendered, no credentials transmitted");
    }
    private sealed class OwnedProcessAdapter : IRdpClient
    {
        public int Starts;
        public Task<RdpEndpointInfo?> CheckAsync(ServerProfile profile,CancellationToken ct)=>Task.FromResult<RdpEndpointInfo?>(new(TimeSpan.FromMilliseconds(15),true,"NLA / CredSSP (fixture)"));
        public RdpLaunch Prepare(ServerProfile profile,RdpOptions options) { Starts++; return new(Environment.GetEnvironmentVariable("ComSpec")!,["/d","/c","ping -n 30 127.0.0.1 >nul"],false); }
    }
    private sealed class FailingEndpointAdapter : IRdpClient
    {
        public Task<RdpEndpointInfo?> CheckAsync(ServerProfile profile,CancellationToken ct)=>Task.FromException<RdpEndpointInfo?>(new TimeoutException("Serwer RDP nie odpowiada — sprawdź sieć/VPN."));
        public RdpLaunch Prepare(ServerProfile profile,RdpOptions options)=>throw new Exception("Endpoint failure should prevent launch");
    }
}
