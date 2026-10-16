using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Runtime.InteropServices;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

internal static class NativeRdpWorkspaceChecks
{
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    internal static void Run(string root,string[] args,string password)
    {
        var paths=new AppPaths(Path.Combine(root,"native-data-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths);
        repo.InitializeAsync().GetAwaiter().GetResult();
        var profile=new ServerProfile { Name="RDP embedded test",Host=args[1],Username=args[2],Protocol=Protocol.RDP,Port=3389,Rdp=new() { DisplayMode=RdpDisplayMode.Embedded,Width=1280,Height=720,Clipboard=false } };
        var main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient()) { Title="ServerDesk – native RDP workspace test" };
        SessionPane? pane=null; nint handle=0; var phase=0; var waits=0; var busy=false; Exception? failure=null;
        using var done=new CancellationTokenSource(); var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(2) };
        void SameSession()
        {
            if(pane!.EmbeddedRdpHandle!=handle || !pane.IsRdpLoggedIn || pane.VisualState!=SessionVisualState.Connected)throw new Exception("Docking/hiding/reparenting lost the authenticated RDP control");
        }
        timer.Tick+=async (_,_)=>
        {
            if(busy)return; busy=true;
            try
            {
                if(phase==0)
                {
                    pane=new SessionPane(main,profile,new SshConnectionFactory(repo),new ExternalRdpClient(),repo); main.RegisterPane(pane);
                    await pane.ConnectAsync(password,CancellationToken.None);
                }
                else if(phase==1 || phase==9)
                {
                    if(!pane!.IsRdpLoggedIn) { if(++waits>30)throw new TimeoutException("No native OnLoginComplete within 60s"); return; }
                    handle=pane.EmbeddedRdpHandle; Console.WriteLine("PASS native RDP authenticated in ServerDesk; "+pane.ConnectionStatus);
                    if(pane.IsConnected)throw new Exception("RDP incorrectly enables SSH monitoring/SFTP");
                    if(phase==1)main.DockSession(pane,SessionDock.Secondary);
                }
                else if(phase==2)
                {
                    SameSession(); Console.WriteLine("PASS RDP split view retains authenticated HWND");
                    main.DockSession(pane!,SessionDock.Floating);
                }
                else if(phase==3)
                {
                    SameSession(); Console.WriteLine("PASS RDP floating window retains authenticated HWND");
                    main.DockSession(pane!,SessionDock.Primary);
                }
                else if(phase==4)
                {
                    SameSession(); Console.WriteLine("PASS RDP dock back retains authenticated HWND");
                    var other=new SessionPane(main,profile with { Id=Guid.NewGuid(),Name="Inactive external RDP",Rdp=new() },new SshConnectionFactory(repo),new ExternalRdpClient(),repo); main.RegisterPane(other);
                }
                else if(phase==5)
                {
                    SameSession(); if(pane!.IsVisible)throw new Exception("Inactive RDP card remained visible");
                    main.DockSession(pane,SessionDock.Primary);
                }
                else if(phase==6)
                {
                    SameSession(); if(!pane!.IsVisible)throw new Exception("Reactivated RDP remained hidden");
                    main.Width=950; main.Height=700; Console.WriteLine("PASS inactive tab keeps RDP session; reactivation restores desktop");
                }
                else if(phase==7)
                {
                    SameSession(); var host=pane!.GetVisualDescendants().OfType<WindowsRdpHost>().Single();
                    if(host.Bounds.Width>pane!.Bounds.Width+1 || host.Bounds.Height>pane.Bounds.Height+1)throw new Exception("Native RDP escaped session bounds");
                    Console.WriteLine("PASS RDP resize and bounds; viewport="+host.Bounds.Size);
                }
                else if(phase==8)
                {
                    await pane!.ReconnectAsync(); if(IsWindow(handle))throw new Exception("Reconnect leaked previous native RDP control"); waits=0;
                }
                else if(phase>=10 && phase<16)SameSession();
                else if(phase==16)
                {
                    SameSession(); main.Close();
                    var confirm=main.OwnedWindows.Single(w=>w.Title=="Zamknąć ServerDesk?");
                    confirm.GetVisualDescendants().OfType<Button>().Single(b=>b.Tag as string=="Potwierdź").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                else
                {
                    if(main.IsVisible || IsWindow(handle))throw new Exception("Application shutdown leaked active RDP window");
                    Console.WriteLine("PASS native RDP reconnect and confirmed application shutdown release owned control"); timer.Stop(); done.Cancel();
                }
                phase++;
            }
            catch(Exception ex) { failure=ex; timer.Stop(); pane?.Dispose(); main.Close(); done.Cancel(); }
            finally { busy=false; }
        };
        main.Show(); timer.Start(); Dispatcher.UIThread.MainLoop(done.Token); password=""; if(failure!=null)throw failure;
    }
}
