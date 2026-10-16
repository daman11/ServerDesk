using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ServerDesk.App;

// Opt-in release check. It runs inside the actual executable's activation
// context, uses a separate data directory and never saves test credentials.
internal static class RdpExecutableCheck
{
    [DllImport("user32.dll")] private static extern bool IsWindow(nint window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(nint window);
    internal static int Run(string[] args)
    {
        if(!OperatingSystem.IsWindows() || args.Length is not (2 or 5))return 2;
        var report=Path.GetFullPath(args[1]); Directory.CreateDirectory(Path.GetDirectoryName(report)!);
        Environment.SetEnvironmentVariable("SERVERDESK_DATA_HOME",Path.Combine(Path.GetDirectoryName(report)!,"rdp-check-"+Guid.NewGuid().ToString("N")));
        var steps=new List<string>(); Exception? error=null;
        try
        {
            Program.BuildAvaloniaApp().SetupWithoutStarting();
            var paths=new AppPaths(); var repo=new ProfileRepository(paths); repo.InitializeAsync().GetAwaiter().GetResult();
            var main=new MainWindow(new MainViewModel(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient());
            var profile=new ServerProfile { Name="Release RDP check",Protocol=Protocol.RDP,Host=args.Length==5?args[2]:"127.0.0.1",Port=3389,Username=args.Length==5?args[3]:"probe",Rdp=new() { DisplayMode=RdpDisplayMode.Embedded,Clipboard=false,Width=1280,Height=720 } };
            profile.Validate();
            SessionPane? pane=null; WindowsRdpHost? host=null; nint handle=0; var phase=0; var busy=false;
            using var done=new CancellationTokenSource(); using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(100));
            var timer=new DispatcherTimer { Interval=TimeSpan.FromSeconds(1) };
            void SameControl()
            {
                if(host==null || host.WindowHandle!=handle || !IsWindow(handle) || args.Length==5 && !pane!.IsRdpLoggedIn)throw new InvalidOperationException("Release RDP control did not survive docking.");
            }
            void Finish()
            {
                host?.Dispose();
                if(pane!=null && main.OpenSessions.Contains(pane))main.RemovePane(pane);
                main.Close();
            }
            timer.Tick+=async (_,_)=>
            {
                if(busy)return; busy=true;
                try
                {
                    limit.Token.ThrowIfCancellationRequested();
                    if(phase==0)
                    {
                        pane=new SessionPane(main,profile,new SshConnectionFactory(repo),new ExternalRdpClient(),repo); main.RegisterPane(pane);
                        if(args.Length==5)
                        {
                            using var pipe=new NamedPipeClientStream(".",args[4],PipeDirection.In,PipeOptions.Asynchronous);
                            await pipe.ConnectAsync(limit.Token); using var input=new StreamReader(pipe);
                            var password=await input.ReadLineAsync(limit.Token)??throw new InvalidDataException("Empty credential pipe.");
                            await pane.ConnectAsync(password,limit.Token); password="";
                            host=pane.GetVisualDescendants().OfType<WindowsRdpHost>().Single();
                        }
                        else
                        {
                            host=new WindowsRdpHost(); Grid.SetRow(host,2); pane.Children.Add(host);
                            await host.Ready.WaitAsync(TimeSpan.FromSeconds(8),limit.Token);
                        }
                        handle=host.WindowHandle;
                        if(!IsWindow(handle))throw new InvalidOperationException("Release EXE did not create an RDP HWND.");
                        steps.Add("RDP control created inside published EXE; version="+host.Version);
                    }
                    else if(phase==1)
                    {
                        if(args.Length==5 && !pane!.IsRdpLoggedIn)return;
                        if(args.Length==5)steps.Add("OnLoginComplete confirmed in published EXE");
                        if(!IsWindowVisible(handle))throw new InvalidOperationException("Published RDP control is hidden.");
                        main.DockSession(pane!,SessionDock.Secondary);
                    }
                    else if(phase==2) { SameControl(); steps.Add("Split view retained HWND"+(args.Length==5?" and login":"")); main.DockSession(pane!,SessionDock.Quaternary); }
                    else if(phase==3) { SameControl(); if(!IsWindowVisible(handle))throw new InvalidOperationException("Four-panel RDP host is hidden."); steps.Add("Four-panel grid retained visible HWND"); main.DockSession(pane!,SessionDock.Tertiary); main.SetWorkspaceMode(WorkspaceMode.Three); }
                    else if(phase==4) { SameControl(); if(!IsWindowVisible(handle))throw new InvalidOperationException("Three-panel RDP host is hidden."); steps.Add("Three-panel layout retained visible HWND"); main.DockSession(pane!,SessionDock.Floating); }
                    else if(phase==5) { SameControl(); steps.Add("Floating window retained HWND"+(args.Length==5?" and login":"")); main.DockSession(pane!,SessionDock.Primary); }
                    else if(phase==6) { SameControl(); steps.Add("Dock back retained HWND"+(args.Length==5?" and login":"")); main.Width=950; main.Height=700; }
                    else if(phase==7)
                    {
                        SameControl(); steps.Add("Resize retained native control");
                        if(args.Length==5)await pane!.ReconnectAsync();
                    }
                    else if(phase==8)
                    {
                        if(args.Length==5)
                        {
                            if(!pane!.IsRdpLoggedIn)return;
                            if(IsWindow(handle))throw new InvalidOperationException("Reconnect leaked the previous RDP window.");
                            host=pane.GetVisualDescendants().OfType<WindowsRdpHost>().Single(); handle=host.WindowHandle;
                            steps.Add("Reconnect confirmed OnLoginComplete and released previous HWND");
                        }
                        Finish();
                    }
                    else
                    {
                        if(main.IsVisible)return;
                        if(IsWindow(handle))throw new InvalidOperationException("Release check leaked RDP HWND.");
                        steps.Add("Owned RDP HWND released and window closed"); timer.Stop(); done.Cancel();
                    }
                    phase++;
                }
                catch(Exception ex) { error=ex; timer.Stop(); Finish(); done.Cancel(); }
                finally { busy=false; }
            };
            try { main.Show(); timer.Start(); Dispatcher.UIThread.MainLoop(done.Token); }
            finally { timer.Stop(); Finish(); }
        }
        catch(Exception ex) { error=ex; }
        File.WriteAllText(report,JsonSerializer.Serialize(new { Success=error==null,Version=typeof(App).Assembly.GetName().Version?.ToString(),Executable=Environment.ProcessPath,Steps=steps,Error=error?.GetType().Name,Message=error?.Message },new JsonSerializerOptions { WriteIndented=true }));
        return error==null?0:1;
    }
}
