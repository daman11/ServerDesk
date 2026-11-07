using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ServerDesk.App;
using ServerDesk.Core;

internal static class NativeRdpViewportChecks
{
    private static object Control(WindowsRdpHost host)=>typeof(WindowsRdpHost).GetField("control",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
    private static object Get(object target,string name)=>target.GetType().InvokeMember(name,BindingFlags.GetProperty,null,target,null)!;
    private static void Put(object target,string name,object value)=>target.GetType().InvokeMember(name,BindingFlags.SetProperty,null,target,[value]);
    private static bool SmartSizing(WindowsRdpHost host)
    { if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException(); var advanced=Get(Control(host),"AdvancedSettings9"); try { return Convert.ToBoolean(Get(advanced,"SmartSizing")); } finally { Marshal.ReleaseComObject(advanced); } }
    private static void SetSmartSizing(WindowsRdpHost host,bool value)
    { if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException(); var advanced=Get(Control(host),"AdvancedSettings9"); try { Put(advanced,"SmartSizing",value); } finally { Marshal.ReleaseComObject(advanced); } }
    private static void Verify(bool condition,string message) { if(!condition)throw new Exception(message); }
    private static void Dpi(Window root,int dpi)
    {
        var handle=root.TryGetPlatformHandle()!.Handle; Verify(GetWindowRect(handle,out var rect),"Missing own test window");
        // Only this disposable HWND receives a synthetic DPI message. No display
        // settings, registry, other windows, network or credentials are changed.
        SendMessageW(handle,0x02E0,new nint((dpi<<16)|dpi),ref rect);
    }
    internal static void Run(string root)
    {
        using var host=new WindowsRdpHost(); var window=new Window { Title="ServerDesk — RDP viewport test",Width=1100,Height=720,Content=host };
        Window? second=null; nint retained=0; var phase=0; var monitorPhase=0; var checks=new List<object>(); Exception? failure=null;
        var physical=window.Screens.All.OrderByDescending(s=>(long)s.WorkingArea.Width*s.WorkingArea.Height).ToArray();
        using var done=new CancellationTokenSource(TimeSpan.FromSeconds(35)); var timer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(450) };
        void Check(string name,Window owner)
        {
            var settings=host.SynchronizeViewport();
            Verify(settings!=null && host.LastViewportError==null,"Viewport synchronization failed: "+host.LastViewportError);
            Verify(host.WindowHandle==retained && !host.IsRunning && !host.IsLoggedIn,"Viewport work changed transport or recreated the control");
            Verify(GetClientRect(retained,out var client),"Cannot read own RDP host bounds");
            Verify(Math.Abs(client.Right-host.Bounds.Width*owner.RenderScaling)<=1 && Math.Abs(client.Bottom-host.Bounds.Height*owner.RenderScaling)<=1,"Native host kept the old physical viewport size");
            var ole=(IOleViewport)Control(host); Marshal.ThrowExceptionForHR(ole.GetWindow(out var inner));
            Verify(GetWindowRect(inner,out var content),"Cannot read ActiveX object bounds");
            Verify(GetWindowRect(retained,out var outer),"Cannot read native host bounds");
            Verify(content.Left>=outer.Left && content.Top>=outer.Top && content.Right<=outer.Right && content.Bottom<=outer.Bottom,"RDP in-place object extends outside the host");
            Verify(host.LastViewportSize==(client.Right,client.Bottom) && SmartSizing(host),"Pixel extent or local fitting is stale");
            var expected=RdpDisplaySettings.FromViewport(client.Right/owner.RenderScaling,client.Bottom/owner.RenderScaling,owner.RenderScaling);
            Verify(settings==expected,"Remote display request uses stale DPI/viewport units");
            Verify(Convert.ToInt32(Get(Control(host),"Connected"))==0,"Fixture attempted a real RDP connection");
            checks.Add(new { Name=name,Position=owner.Position.ToString(),Scale=owner.RenderScaling,Host=new[]{client.Right,client.Bottom},Object=new[]{content.Right-content.Left,content.Bottom-content.Top},Display=settings });
        }
        timer.Tick+=(_,_)=>
        {
            try
            {
                if(!host.Ready.IsCompleted)return;
                host.Ready.GetAwaiter().GetResult();
                if(retained==0)retained=host.WindowHandle;
                if(physical.Length>=2 && monitorPhase<3)
                {
                    if(monitorPhase>0)Check(monitorPhase==1?"physical-larger-monitor":"physical-smaller-monitor",window);
                    if(monitorPhase==1)
                        typeof(WindowsRdpHost).GetProperty("LastDisplaySettings",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(host,new RdpDisplaySettings(1600,900,100,100));
                    if(monitorPhase<2)
                    {
                        var screen=monitorPhase==0?physical[0]:physical[^1];
                        window.Width=Math.Min(2400,screen.WorkingArea.Width/screen.Scaling-96); window.Height=Math.Min(900,screen.WorkingArea.Height/screen.Scaling-96);
                        window.Position=new PixelPoint(screen.WorkingArea.X+24,screen.WorkingArea.Y+24);
                    }
                    else Verify(host.LastDisplaySettings==null,"A physical monitor move did not invalidate stale display settings");
                    monitorPhase++; return;
                }
                switch(phase)
                {
                    case 0:
                        retained=host.WindowHandle; Check("initial",window);
                        // Model a stale oversized ActiveX object and a lost fit
                        // setting, then force the same invalidation used by DPI.
                        var large=new Rect { Right=3840,Bottom=2160 }; var clip=large;
                        Marshal.ThrowExceptionForHR(((IOleViewport)Control(host)).SetObjectRects(ref large,ref clip)); SetSmartSizing(host,false);
                        typeof(WindowsRdpHost).GetMethod("InvalidateViewport",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(host,null);
                        Check("stale-object-recovered",window); window.Width=720; window.Height=480; break;
                    case 1: Check("smaller-window",window); Dpi(window,144); break;
                    case 2: Verify(Math.Abs(window.RenderScaling-1.5)<.01,"Synthetic 150% DPI was not applied"); Check("synthetic-150",window); Dpi(window,192); break;
                    case 3: Verify(Math.Abs(window.RenderScaling-2)<.01,"Synthetic 200% DPI was not applied"); Check("synthetic-200",window); Dpi(window,96); break;
                    case 4: Check("synthetic-return-100",window); window.WindowState=WindowState.Minimized; break;
                    case 5: Verify(host.SynchronizeViewport()==null,"Minimized window changed remote display size"); window.WindowState=WindowState.Normal; break;
                    case 6: Check("restored",window); host.IsVisible=false; Verify(host.SynchronizeViewport()==null,"Hidden host synchronized"); host.IsVisible=true; break;
                    case 7: Check("shown",window); window.Content=null; break;
                    case 8:
                        Verify(host.SynchronizeViewport()==null,"Detached host synchronized");
                        second=new Window { Title="ServerDesk — RDP detached viewport test",Width=620,Height=420,Content=host }; second.Show(); break;
                    case 9:
                        Check("reattached-smaller-window",second!);
                        var marker=new RdpDisplaySettings(800,600,100,100);
                        typeof(WindowsRdpHost).GetProperty("LastDisplaySettings",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(host,marker);
                        Dpi(window,144); Verify(host.LastDisplaySettings==marker,"Old parent still receives RDP DPI notifications after reparenting");
                        Dpi(second!,144); Verify(host.LastDisplaySettings==null,"New parent DPI change did not invalidate display deduplication");
                        Check("new-parent-dpi",second!);
                        typeof(WindowsRdpHost).GetField("smartSizing",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(host,false);
                        host.SynchronizeViewport(true); Verify(!SmartSizing(host),"Explicitly disabled fitting was overridden");
                        typeof(WindowsRdpHost).GetField("smartSizing",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(host,true);
                        host.SynchronizeViewport(true);
                        var handlers=(List<(int Id,Delegate Handler)>)typeof(WindowsRdpHost).GetField("handlers",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(host)!;
                        typeof(WindowsRdpHost).GetProperty("LastDisplaySettings",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(host,marker);
                        ((Action<int,int>)handlers.Single(h=>h.Id==12).Handler)(800,600);
                        Verify(host.LastDisplaySettings==marker,"Remote size acknowledgement would cause a display request feedback loop");
                        ((Action)handlers.Single(h=>h.Id==3).Handler)();
                        Verify(!host.ResizeDesktop(new(800,600,100,100),true) && host.LastDisplayError!=null,"Offline client unexpectedly accepted a server display update");
                        Verify(host.SynchronizeViewport(true)!=null && SmartSizing(host),"Server display failure disabled local fitting");
                        ((Action<int>)handlers.Single(h=>h.Id==4).Handler)(1);
                        Check("local-fit-after-rejected-server-update",second!);
                        checks.Add(new { Name="server-update-rejected",Error=host.LastDisplayError });
                        break;
                    case 10:
                        host.Dispose(); Verify(host.SynchronizeViewport()==null && !IsWindow(retained),"Disposed control retained viewport work/HWND");
                        timer.Stop(); second!.Close(); window.Close(); done.Cancel(); break;
                }
                phase++;
            }
            catch(Exception ex) { failure=ex; timer.Stop(); host.Dispose(); second?.Close(); window.Close(); done.Cancel(); }
        };
        window.Show(); timer.Start(); Dispatcher.UIThread.MainLoop(done.Token); timer.Stop();
        if(phase<11 && failure==null)failure=new TimeoutException("Native RDP viewport fixture timed out");
        var screens=window.Screens.All.Select(s=>new { Area=s.WorkingArea.ToString(),s.Scaling }).ToArray();
        host.Dispose(); second?.Close(); window.Close();
        File.WriteAllText(Path.Combine(root,"native-viewport.json"),JsonSerializer.Serialize(new { Passed=failure==null,Checks=checks,Screens=screens,PhysicalMonitorMove=monitorPhase==3,Error=failure?.ToString(),LiveRdp=false,SyntheticDpi=true },new JsonSerializerOptions { WriteIndented=true }));
        if(failure!=null)throw failure;
        Console.WriteLine("PASS native RDP viewport: retained HWND/object bounds, stale extent/SmartSizing recovery, shrink, synthetic 100/150/200/100 DPI, minimize/hide/detach, reparent, explicit opt-out, server-update rejection and disposal; no server or credentials");
    }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] private static extern bool GetClientRect(nint handle,out Rect rect);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint handle,out Rect rect);
    [DllImport("user32.dll")] private static extern bool IsWindow(nint handle);
    [DllImport("user32.dll")] private static extern nint SendMessageW(nint handle,uint message,nint wparam,ref Rect rectangle);
    [ComImport,Guid("00000113-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IOleViewport
    {
        [PreserveSig] int GetWindow(out nint handle);
        [PreserveSig] int ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool enter);
        [PreserveSig] int InPlaceDeactivate();
        [PreserveSig] int UIDeactivate();
        [PreserveSig] int SetObjectRects([In] ref Rect position,[In] ref Rect clip);
        [PreserveSig] int ReactivateAndUndo();
    }
}
