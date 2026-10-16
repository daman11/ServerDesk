using Avalonia.LogicalTree;
using Serilog;
using Avalonia.VisualTree;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Iciclecreek.Terminal;
using Avalonia.Controls;
using System.Text;

var root=Path.GetFullPath(args.Length>0?args[0]:"artifacts/ui-smoke");
Directory.CreateDirectory(root); Log.Logger=new LoggerConfiguration().WriteTo.File(Path.Combine(root,"ui-diagnostics.log")).CreateLogger();
var dataRoot=Path.Combine(root,"data-"+Guid.NewGuid().ToString("N"));
Environment.SetEnvironmentVariable("SERVERDESK_DATA_HOME",dataRoot);
if(args.Contains("--native-rdp"))
{
    if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
    Exception? error=null;
    var ui=new Thread(()=> { try { AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().SetupWithoutStarting(); NativeRdpChecks.Run(root,args); } catch(Exception ex) { error=ex; } });
    ui.SetApartmentState(ApartmentState.STA); ui.Start(); ui.Join(); if(error!=null)throw error; return;
}
if(args.Contains("--published-startup")) { PublishedStartupChecks.Run(root,args[Array.IndexOf(args,"--published-startup")+1]).GetAwaiter().GetResult(); return; }
if(args.Contains("--published-restart")) { RestartProcessChecks.Run(root,args[Array.IndexOf(args,"--published-restart")+1]).GetAwaiter().GetResult(); return; }
if(args.Contains("--native-restart-helper")) { AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().SetupWithoutStarting(); RestartProcessChecks.Helper(root,args[Array.IndexOf(args,"--native-restart-helper")+1]); return; }
if(args.Contains("--native-startup")) { AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().SetupWithoutStarting(); NativeStartupChecks.Run(root); return; }
if(args.Contains("--native-titlebar")) { AppBuilder.Configure<App>().UsePlatformDetect().WithInterFont().SetupWithoutStarting(); NativeChromeChecks.Run(root); return; }
AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing=false }).UseSkia().WithInterFont().SetupWithoutStarting();
if(args.Contains("--brand-assets")) { BrandAssets.Generate(root); return; }
if(args.Contains("--ssh-acceptance")) { SshAcceptanceChecks.Run(root,args[1]); return; }
if(args.Contains("--large-browser")) { LargeBrowserChecks.Run(root,args.Contains("--measure-only")); return; }
if(args.Contains("--platform-ux")) { PlatformUxChecks.Run(root); return; }
if(args.Contains("--credential-save")) { CredentialSaveChecks.Run(root); return; }
if(args.Contains("--credential-save-network")) { CredentialSaveChecks.Network(root,args[1],args[2]); return; }
if(args.Contains("--large-file-views")) { LargeFileViewChecks.Run(root); return; }
if(args.Contains("--sftp-navigation")) { SftpNavigationChecks.Run(root); return; }
if(args.Contains("--extended-settings")) { ExtendedSettingsChecks.Run(root); return; }
if(args.Contains("--four-panels")) { FourPanelChecks.Run(root); return; }
if(args.Contains("--commands")) { CommandPanelChecks.Run(root); return; }
if(args.Contains("--presentation-consistency")) { PresentationConsistencyChecks.Run(root); return; }
if(args.Contains("--commands-network")) { CommandPanelChecks.Network(root,args[1],args[2]); return; }
if(args.Contains("--four-panels-network")) { FourPanelChecks.Network(root,args[1],args[2]); return; }
if(args.Contains("--extended-settings-network")) { ExtendedSettingsChecks.Network(root,args[1],args[2]); return; }
if(args.Contains("--sftp-navigation-network")) { SftpNavigationChecks.Network(root,args[1],args[2]); return; }
if(args.Contains("--database-backups")) { DatabaseBackupChecks.Run(root); return; }
if(args.Contains("--trusted-hosts")) { TrustedHostsChecks.Run(root); return; }
if(args.Contains("--trusted-hosts-network")) { TrustedHostsChecks.Network(root,args[1],args[2]); return; }
if(args.Contains("--terminal-search")) { TerminalSearchChecks.Run(root); return; }
if(args.Contains("--terminal-search-network")) { TerminalSearchChecks.Network(root,args[1],args[2]); return; }
if(args.Contains("--ssh-responsiveness")) { SshResponsivenessChecks.Run(root,args[1],args[2]); return; }
if(args.Contains("--monitoring-acceptance")) { MonitoringAcceptanceChecks.Run(root,args[1],args[2]); return; }
if(args.Contains("--session-lifecycle")) { SessionLifecycleChecks.Run(root); if(args.Length>=4)SessionLifetimeNetworkChecks.Run(root,args[1],args[2]); return; }
if(args.Contains("--session-endurance")) { SessionLifetimeNetworkChecks.Endurance(root,args[1],args[2],int.Parse(args[Array.IndexOf(args,"--session-endurance")+1])); return; }
if(args.Contains("--session-workflow")) { SessionWorkflowChecks.Run(root); return; }
if(args.Contains("--transfer-overview")) { TransferOverviewChecks.Layout(root); return; }
if(args.Contains("--network-tools")) { NetworkToolsChecks.Run(root); return; }
if(args.Contains("--app-polish")) { AppPolishChecks.Run(root); return; }
if(args.Contains("--sorting")) { SortingChecks.Run(root); return; }
if(args.Contains("--connection-browser")) { ConnectionBrowserChecks.Run(root); return; }
if(args.Contains("--design-refresh")) { DesignRefreshChecks.Run(root); return; }
if(args.Contains("--sorting-backend")) { NetworkBackendChecks.Sorting(); return; }
if(args.Contains("--transfer-backend")) { NetworkBackendChecks.Transfers(); return; }
if(args.Contains("--network-backend")) { NetworkBackendChecks.Run(); return; }
if(args.Contains("--network-snmp")) { var index=Array.IndexOf(args,"--network-snmp"); NetworkSnmpChecks.Run(args[index+1],int.Parse(args[index+2])); return; }
if(args.Contains("--workspace-crash-writer")) { WorkspaceRecoveryChecks.WriteThenCrash(root); return; }
if(args.Contains("--workspace-crash-recovery")) { WorkspaceRecoveryChecks.RestoreAfterCrash(root); return; }
var paths=new AppPaths(dataRoot); var repository=new ProfileRepository(paths);
repository.InitializeAsync().GetAwaiter().GetResult();
foreach(var sample in new[]{new ServerProfile { IconId="globe",Name="web-prod-01",Host="10.24.8.11",Username="deploy",Folder="Production/Linux",Favorite=true },new ServerProfile { IconId="database",Name="Database primary",Host="10.24.8.20",Username="postgres",Folder="Production/Linux" },new ServerProfile { Name="Windows management",Host="10.24.9.10",Port=3389,Protocol=Protocol.RDP,Folder="Production/Windows",Favorite=true },new ServerProfile { IconId="container",Name="staging-api",Host="192.168.20.5",Username="admin",Folder="Staging",LastConnected=DateTimeOffset.UtcNow }})repository.SaveAsync(sample).GetAwaiter().GetResult();
var vm=new MainViewModel(repository);
var window=new MainWindow(vm,repository,new CredentialStore(paths),new SshConnectionFactory(repository),new ExternalRdpClient());
window.Show();
for(var i=0;i<20;i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(25); }
window.CaptureRenderedFrame()!.Save(Path.Combine(root,"main-dark.png"));
if(args.Contains("--icons")) { IconChecks.Run(window,repository,root); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--security")) { SecurityChecks.Run(repository); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--stability")) { StabilityChecks.Run(repository,root); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--session-chrome")) { SessionChromeChecks.Preview(window,repository,root); return; }
if(args.Contains("--workspace-details")) { WorkspaceDetailsChecks.Preview(window,repository,root); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--settings-layout")) { SettingsLayoutChecks.Run(window,repository,root); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--docking-regression")) { PersonalizationChecks.Run(window,root); SessionWorkflowChecks.Run(root); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--sftp-workflow")) { SftpWorkflowChecks.Run(window,repository,root,args[1],args[2]); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--sftp-resilience")) { SftpResilienceChecks.Run(window,repository,root,args[1],args[2]); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--transfer-queue-recovery")) { TransferQueueRecoveryChecks.Run(window,repository,root,args[1],args[2]); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--transfer-errors")) { TransferErrorChecks.Run(window,repository,root,args[1],args[2]); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--sftp-interruption")) { SftpInterruptionChecks.Run(window,repository,root,args[1],args[2]); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--sftp-browser-recovery")) { SftpBrowserRecoveryChecks.Run(window,repository,root,args[1],args[2]); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--sftp-links")) { SftpLinkChecks.Run(window,repository,root,args[1],args[2]); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--sftp-focus")) { SftpWorkflowChecks.FocusControls(window,repository); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--sftp-batch")) { SftpBatchChecks.Layout(window,repository,root); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--file-deletion")) { FileDeletionChecks.Run(window,repository,root); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--file-deletion-network")) { var index=Array.IndexOf(args,"--file-deletion-network"); FileDeletionChecks.Network(window,repository,root,args[index+1],args[index+2]); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--sftp-batch-network")) { var index=Array.IndexOf(args,"--sftp-batch-network"); SftpBatchChecks.Network(window,repository,root,args[index+1],args[index+2]); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--transfer-overview-network")) { var index=Array.IndexOf(args,"--transfer-overview-network"); TransferOverviewChecks.Network(window,repository,root,args[index+1],args[index+2]); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--rdp-pane")) { RdpPaneChecks.Run(window,repository,root); if(args.Length>2)RdpPaneChecks.Endpoint(window,repository,root,args[1]); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--serial-pane")) { SerialPaneChecks.Run(window,repository,root,args[Array.IndexOf(args,"--serial-pane")+1]); return; }
if(args.Contains("--protocols")) { ProtocolChecks.Run(window,repository,root); return; }
if(args.Contains("--connection-control")) { ConnectionControlChecks.Run(window,repository,root); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--localization")) { LocalizationChecks.Run(window,repository,root); return; }
if(args.Contains("--restart-design")) { RestartNoticeChecks.Run(window,repository,root); return; }
if(args.Contains("--visual-surfaces")) { VisualSurfaceChecks.Run(window,repository,root); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--polish")) { PolishChecks.Run(window,repository,root); window.Close(); RedesignChecks.Frames(); return; }
if(args.Contains("--live-settings"))
{
    var profile=new ServerProfile { Name="Live settings test",Host=args[1],Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(args[2]) };
    var factory=new SshConnectionFactory(repository) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
    using var pane=new SessionPane(window,profile,factory,new ExternalRdpClient(),repository); window.RegisterPane(pane);
    RedesignChecks.Wait(pane.ConnectAsync("serverdesk-test-passphrase",CancellationToken.None)); SettingsLayoutChecks.Network(window,repository,root);
    window.Close(); RedesignChecks.Frames(); window.OwnedWindows.Single().GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Potwierdź").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); RedesignChecks.Frames(); return;
}
if(args.Contains("--design-preview")) { DesignPolishChecks.Run(window,repository,root); window.Close(); RedesignChecks.Frames(); return; }
DesignSystem.Apply("light");
for(var i=0;i<10;i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(25); }
window.CaptureRenderedFrame()!.Save(Path.Combine(root,"main-light.png"));
foreach(var theme in DesignSystem.Themes) { DesignSystem.Apply(theme.Id); for(var i=0;i<10;i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(25); } window.CaptureRenderedFrame()!.Save(Path.Combine(root,"theme-"+theme.Id+".png")); }
DesignSystem.Apply("dark"); var settings=Dialogs.Settings(window,repository,()=>Task.CompletedTask); for(var i=0;i<20;i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(25); } var settingsWindow=window.OwnedWindows.Single(); settingsWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"appearance-settings.png")); settingsWindow.Close(); for(var i=0;i<10;i++)Dispatcher.UIThread.RunJobs();
Console.WriteLine("MainWindow rendered in five themes; appearance settings rendered.");
IconChecks.Run(window,repository,root);
RedesignChecks.Run(window,repository,root);
DesignPolishChecks.Run(window,repository,root);
HardeningChecks.Run(window,repository,root);
AuditChecks.Run(window,repository);
PersonalizationChecks.Run(window,root);
SessionWorkflowChecks.Run(root);
if(OperatingSystem.IsWindows())
{
    var profile=new ServerProfile { Name="Process lifecycle test",Protocol=Protocol.RDP,Host="127.0.0.1",Port=49999,Username="TEST\\user",Rdp=new() { Width=1920,Height=1080,Clipboard=false } };
    using var pane=new SessionPane(window,profile,new SshConnectionFactory(repository),new TestProcessRdpAdapter(),repository);
    var processWindow=new Window { Width=1000,Height=600,Content=pane }; processWindow.Show();
    var task=pane.ConnectAsync("",CancellationToken.None); var deadline=DateTime.UtcNow.AddSeconds(8);
    while(!task.IsCompleted && DateTime.UtcNow<deadline) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); } task.GetAwaiter().GetResult();
    for(var i=0;i<50;i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
    if(!pane.SessionTitle.StartsWith("○"))throw new Exception("Finished external process still appears active in RDP tab");
    processWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"rdp-process-ended.png")); pane.Dispose(); processWindow.Close();
    var edit=Dialogs.Edit(window,profile,[]);
    for(var i=0;i<20;i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
    var editor=window.OwnedWindows.Single(); editor.CaptureRenderedFrame()!.Save(Path.Combine(root,"rdp-settings.png")); editor.Close();
    for(var i=0;i<10;i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(20); }
    if(!edit.IsCompleted || edit.GetAwaiter().GetResult()!=null)throw new Exception("Cancelling RDP settings did not close the dialog cleanly");
    Console.WriteLine("PASS RDP pane tracks process termination (test adapter, no RDP connection)");
}
if(args.Length>=3)
{
    T Pump<T>(Task<T> task) { var watch=System.Diagnostics.Stopwatch.StartNew(); while(!task.IsCompleted) { if(watch.Elapsed>TimeSpan.FromSeconds(40))throw new TimeoutException("UI operation timed out"); Dispatcher.UIThread.RunJobs(); Thread.Sleep(10); } return task.GetAwaiter().GetResult(); }
    void Wait(Task task) => Pump(task.ContinueWith(t=> { t.GetAwaiter().GetResult(); return true; }));
    void Frames(int count) { for(var i=0;i<count;i++) { Dispatcher.UIThread.RunJobs(); Thread.Sleep(25); } }
    var profile=new ServerProfile { Name="UI terminal integration",Host=args[1],Port=22222,Username=SshTestIdentity.Username,Authentication=Authentication.PrivateKey,PrivateKeyPath=Path.GetFullPath(args[2]) };
    var factory=new SshConnectionFactory(repository) { ConfirmHostKey=(_,_)=>Task.FromResult(true) };
    using var connection=new SshTerminalSession(Pump(factory.SshAsync(profile,"serverdesk-test-passphrase",CancellationToken.None)));
    var terminal=new TerminalControl { Process="",FontSize=15,Background=Avalonia.Media.Brushes.Black,Foreground=Avalonia.Media.Brushes.White };
    var terminalWindow=new Window { Title="Live SSH terminal test",Width=1000,Height=650,Content=terminal }; terminalWindow.Show(); Frames(10); terminal.AttachConnection(connection); connection.Resize(terminal.Terminal.Cols,terminal.Terminal.Rows); Frames(20);
    connection.WriterStream.Write(Encoding.UTF8.GetBytes("printf '\\033[32mREAL SSH / UTF-8: żółć\\033[0m\\n'; stty size\r")); connection.WriterStream.Flush(); Frames(40);
    terminalWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"terminal-ssh.png"));
    connection.WriterStream.Write(Encoding.UTF8.GetBytes("nano\r")); connection.WriterStream.Flush(); Frames(50);
    terminalWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"terminal-nano.png"));
    connection.WriterStream.WriteByte(24); connection.WriterStream.Flush(); Frames(10);
    terminal.DetachConnection(); terminal.Dispose(); terminalWindow.Close(); Console.WriteLine("Live SSH terminal and nano rendered.");
    Application.Current!.RequestedThemeVariant=Avalonia.Styling.ThemeVariant.Dark;
    using var pane=new SessionPane(window,profile,factory,new ExternalRdpClient(),repository) { IsActive=true };
    var sessionWindow=new Window { Width=1200,Height=800,Content=pane }; sessionWindow.Show(); Frames(10);
    var connecting=pane.ConnectAsync("serverdesk-test-passphrase",CancellationToken.None); if(!connecting.IsCompleted && !pane.IsBusy)throw new Exception("Connection loading indicator is absent"); Wait(connecting); if(pane.IsBusy)throw new Exception("Connection loading indicator did not clear"); Frames(10);
    if(pane.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.IsVisible && t.Text?.Contains("Nie udało się wczytać ustawień wyglądu")==true))throw new Exception("Normal pane failed to initialize appearance");
    var paneTerminal=pane.Children.OfType<TerminalControl>().Single();
    Wait(paneTerminal.SendInputAsync("exit\r",CancellationToken.None));
    var until=DateTime.UtcNow.AddSeconds(5); while(pane.IsConnected && DateTime.UtcNow<until)Frames(1); Frames(10);
    if(pane.IsConnected || !pane.SessionTitle.StartsWith("○"))throw new Exception("Remote shell exit was not reflected in pane state");
    sessionWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"session-disconnected.png"));
    Wait(pane.ReconnectAsync()); Frames(10); if(!pane.IsConnected || !pane.SessionTitle.StartsWith("●"))throw new Exception("Reconnect in same pane failed");
    sessionWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"session-reconnected.png"));
    try { Wait(pane.ReconnectAsync("wrong-test-passphrase")); throw new Exception("Invalid credential unexpectedly worked"); } catch(Renci.SshNet.Common.SshException) { }
    if(pane.IsConnected)throw new Exception("Failed reconnect kept connected state");
    Wait(pane.ReconnectAsync("serverdesk-test-passphrase")); if(!pane.IsConnected)throw new Exception("Credential correction did not recover pane");
    Wait(pane.ShowAsync(2)); Frames(240); sessionWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"server-info.png")); sessionWindow.Width=650; sessionWindow.Height=600; Frames(10); sessionWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"server-info-narrow.png")); sessionWindow.Width=1200; sessionWindow.Height=800; Frames(5);
    Wait(pane.ShowAsync(1)); Frames(10); sessionWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"sftp.png"));
    Console.WriteLine("Session pane rows after SFTP: "+string.Join(", ",pane.RowDefinitions.Select(x=>x.ActualHeight))); sessionWindow.Width=650; sessionWindow.Height=600; Frames(10); sessionWindow.CaptureRenderedFrame()!.Save(Path.Combine(root,"sftp-narrow.png")); sessionWindow.Width=1200; sessionWindow.Height=800; Frames(5);
    Wait(pane.ReconnectAsync()); Wait(pane.ShowAsync(2)); Frames(30); Wait(pane.ShowAsync(1)); Frames(10); Wait(pane.ShowAsync(0));
    Wait(paneTerminal.SendInputAsync("sleep 30\r",CancellationToken.None)); Frames(5); pane.Dispose(); Frames(10); sessionWindow.Close();
        Console.WriteLine("PASS same-pane reconnect, remote shell exit, failed authentication recovery, reconnect with monitoring/SFTP open and close during remote command");
    if(OperatingSystem.IsWindows())
    {
        var store=new CredentialStore(paths); Wait(store.SaveAsync(profile.Id,"serverdesk-test-passphrase")); Wait(window.ConnectAsync(profile));
        var second=profile with { Id=Guid.NewGuid(),Name="Long server name — second independent SSH session" }; Wait(store.SaveAsync(second.Id,"serverdesk-test-passphrase")); Wait(window.ConnectAsync(second)); Frames(10);
        var tabs=window.GetVisualDescendants().OfType<TabControl>().Single(t=>Avalonia.Automation.AutomationProperties.GetName(t)=="Sesje panelu 1"); if(tabs.ItemCount!=2)throw new Exception("Main workspace did not create independent tabs");
        Frames(80); var statusbar=((Grid)window.Content!).Children.OfType<Grid>().Single(g=>Grid.GetRow(g)==2); var statusText=statusbar.Children.OfType<TextBlock>().First().Text??""; if(!System.Text.RegularExpressions.Regex.IsMatch(statusText,@"\d{2}:\d{2}:\d{2}$")||statusText.EndsWith("00:00:00"))throw new Exception("Global status did not receive elapsed SSH time: "+statusText);
        window.CaptureRenderedFrame()!.Save(Path.Combine(root,"workspace-ssh-tabs.png"));
        var activeTerminal=window.GetVisualDescendants().OfType<TerminalControl>().Last(); activeTerminal.Focus();
        window.KeyPress(Key.W,RawInputModifiers.Control,PhysicalKey.W,"w"); Frames(4); if(window.OwnedWindows.Count!=0 || tabs.ItemCount!=2)throw new Exception("Ctrl+W in terminal was intercepted");
        window.KeyPress(Key.Tab,RawInputModifiers.Control,PhysicalKey.Tab,"\t"); Frames(4); if(tabs.SelectedIndex!=0)throw new Exception("Ctrl+Tab failed");
        window.KeyPress(Key.Tab,RawInputModifiers.Control|RawInputModifiers.Shift,PhysicalKey.Tab,"\t"); Frames(4); if(tabs.SelectedIndex!=1)throw new Exception("Ctrl+Shift+Tab failed");
        PersonalizationChecks.Network(window,root);
        WorkspaceDetailsChecks.Network(window,repository,root);
        SettingsLayoutChecks.Network(window,repository,root);
        SessionWorkflowChecks.Network(window,repository,root);
        window.KeyPress(Key.W,RawInputModifiers.Control|RawInputModifiers.Shift,PhysicalKey.W,"W"); Frames(5);
        var confirm=window.OwnedWindows.Single(); confirm.GetVisualDescendants().OfType<Button>().Single(b=>(b.Tag as string??b.Content as string)=="Potwierdź").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Frames(10); if(tabs.ItemCount!=1)throw new Exception("Ctrl+Shift+W did not close selected tab");
        Wait(store.DeleteAsync(profile.Id)); Wait(store.DeleteAsync(second.Id)); window.Close(); Frames(5);
        confirm=window.OwnedWindows.Single(); confirm.GetVisualDescendants().OfType<Button>().Single(b=>(b.Tag as string??b.Content as string)=="Potwierdź").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Frames(10);
        Console.WriteLine("PASS real main-window SSH tabs, keyboard cycling, terminal Ctrl+W passthrough and explicit Ctrl+Shift+W close");
    }
}
window.Close();
sealed class TestProcessRdpAdapter : IRdpClient
{
    public RdpLaunch Prepare(ServerProfile profile,RdpOptions options)
    {
        if(options.Width!=1920 || options.Height!=1080 || options.Clipboard)throw new Exception("Persisted RDP options were not applied to pane");
        return new(Environment.GetEnvironmentVariable("ComSpec")!,["/d","/c","exit 7"],false);
    }
}
