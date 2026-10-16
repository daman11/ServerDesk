using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ServerDesk.App;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.Runtime.InteropServices;
using System.Text.Json;

internal static class NativeStartupChecks
{
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left,Top,Right,Bottom; }
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(nint hwnd,int attribute,out Rect rect,int size);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint hwnd,out Rect rect);
    private static Rect VisibleBounds(Window w)
    {
        var h=w.TryGetPlatformHandle()!.Handle;
        if(DwmGetWindowAttribute(h,9,out var r,Marshal.SizeOf<Rect>())!=0 && !GetWindowRect(h,out r))throw new Exception("Cannot read own native window bounds");
        return r;
    }
    private static void Inside(Window w)
    {
        var b=VisibleBounds(w); var screen=w.Screens.All.FirstOrDefault(s=>b.Left>=s.WorkingArea.X-1 && b.Top>=s.WorkingArea.Y-1 && b.Right<=s.WorkingArea.Right+1 && b.Bottom<=s.WorkingArea.Bottom+1);
        if(screen==null)throw new Exception($"Visible window off screen: [{b.Left},{b.Top},{b.Right},{b.Bottom}], work areas: {string.Join(";",w.Screens.All.Select(s=>s.WorkingArea))}, client {w.ClientSize}, scale {w.DesktopScaling}");
    }
    internal static void Run(string root)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        var paths=new AppPaths(Path.Combine(root,"startup-data-"+Guid.NewGuid().ToString("N"))); var repo=new ProfileRepository(paths); repo.InitializeAsync().GetAwaiter().GetResult();
        MainWindow Create()=>new(new MainViewModel(repo),repo,new CredentialStore(paths),new SshConnectionFactory(repo),new ExternalRdpClient());
        var main=Create(); var area=main.Screens.Primary!.WorkingArea;
        foreach(var pair in new[]{("width","1320"),("height","840"),("x",(area.Right-260).ToString()),("y",(area.Bottom-160).ToString())})repo.SetSettingAsync(pair.Item1,pair.Item2).GetAwaiter().GetResult();
        using var done=new CancellationTokenSource(TimeSpan.FromSeconds(45)); Exception? error=null; var step=0; var busy=false; var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(650) }; var steps=new List<string>(); WindowGeometry? normal=null;
        timer.Tick+=async (_,_)=>
        {
            if(busy)return; busy=true;
            try
            {
                Console.WriteLine($"step={step}; state={main.WindowState}; position={main.Position}; client={main.ClientSize}; frame={main.FrameSize}; saved={repo.SettingAsync("mainWindowPlacement").GetAwaiter().GetResult()}");
                if(step==0)
                {
                    Inside(main); steps.Add("Legacy position near bottom/right corrected inside working area");
                    main.Position=new PixelPoint(area.X+70,area.Y+80); main.Width=950; main.Height=640;
                }
                else if(step==1) { Inside(main); normal=new(main.Position.X,main.Position.Y,main.ClientSize.Width,main.ClientSize.Height); main.WindowState=WindowState.Maximized; }
                else if(step==2) { main.Close(); }
                else if(step==3) { if(main.IsVisible)throw new Exception("Main window did not close"); main=Create(); main.Show(); }
                else if(step==4) { if(main.WindowState!=WindowState.Maximized)throw new Exception("Maximized state did not restore"); main.WindowState=WindowState.Normal; }
                else if(step==5)
                {
                    Inside(main); if(Math.Abs(main.ClientSize.Width-normal!.Width)>2 || Math.Abs(main.ClientSize.Height-normal.Height)>2)throw new Exception($"Maximized geometry replaced normal restore bounds: expected {normal}, actual {main.ClientSize}");
                    steps.Add("Maximized restart and normal restore bounds preserved"); main.WindowState=WindowState.Minimized;
                }
                else if(step==6) { main.Close(); }
                else if(step==7) { if(main.IsVisible)throw new Exception("Minimized window did not close"); main=Create(); main.Show(); }
                else if(step==8)
                {
                    if(main.WindowState!=WindowState.Normal)throw new Exception("Application reopened minimized"); Inside(main); steps.Add("Closing minimized never reopens off screen or minimized");
                    var p=new ServerProfile { Name="Floating visibility",Host="fixture.invalid",Username="test" }; var pane=new SessionPane(main,p,new SshConnectionFactory(repo),new ExternalRdpClient(),repo); main.RegisterPane(pane); main.DockSession(pane,SessionDock.Floating);
                }
                else if(step==9)
                {
                    Inside(main.OwnedWindows.Single()); var pane=main.OpenSessions.Single(); main.RemovePane(pane); steps.Add("New floating session fits working area");
                    main.Close();
                }
                else if(step==10)
                {
                    if(main.IsVisible)throw new Exception("Final window did not close"); timer.Stop(); done.Cancel();
                }
                step++;
            }
            catch(Exception ex) { error=ex; timer.Stop(); foreach(var pane in main.OpenSessions.ToArray())main.RemovePane(pane); main.Close(); done.Cancel(); }
            finally { busy=false; }
        };
        main.Show(); timer.Start(); Dispatcher.UIThread.MainLoop(done.Token);
        File.WriteAllText(Path.Combine(root,"native-startup.json"),JsonSerializer.Serialize(new { Success=error==null && step>=10,Steps=steps,Error=error?.ToString(),WorkArea=area.ToString() },new JsonSerializerOptions { WriteIndented=true }));
        if(error!=null)throw error; if(step<10)throw new TimeoutException("Native startup check timed out"); Console.WriteLine("PASS native startup placement, maximize/restore/minimize/restart and floating bounds");
    }
}
