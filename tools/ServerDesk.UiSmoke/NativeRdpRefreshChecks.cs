using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Threading;
using ServerDesk.App;

internal static class NativeRdpRefreshChecks
{
    internal static void Run(WindowsRdpHost host)
    {
        var handle=host.WindowHandle;
        if(handle==0)throw new Exception("Refresh fixture has no native RDP window");
        ValidateRect(handle,0);
        if(!host.RefreshDisplay() || !GetUpdateRect(handle,0,false))throw new Exception("RDP refresh did not invalidate the native HWND");
        if(host.WindowHandle!=handle || host.IsRunning || host.IsLoggedIn)throw new Exception("Repaint changed connection state or recreated the RDP control");
        host.IsVisible=false; RedesignChecks.Frames(2);
        if(host.RefreshDisplay())throw new Exception("Hidden RDP control was repainted");
        host.IsVisible=true; RedesignChecks.Frames(2);
        if(!host.RefreshDisplay() || host.WindowHandle!=handle)throw new Exception("Revealed RDP control could not refresh its retained HWND");

        // Drive the actual registered callbacks, without connecting/logging in.
        // This checks timer ownership only, not a live RDP or monitor-off case.
        var bindings=(List<(int Id,Delegate Handler)>)typeof(WindowsRdpHost).GetField("handlers",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
        var timer=(DispatcherTimer)typeof(WindowsRdpHost).GetField("repaintTimer",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(host)!;
        ((Action)bindings.Single(b=>b.Id==3).Handler)();
        if(!timer.IsEnabled)throw new Exception("Logged-in RDP did not schedule display refresh");
        ((Action<int>)bindings.Single(b=>b.Id==4).Handler)(1);
        if(timer.IsEnabled || host.IsLoggedIn)throw new Exception("Disconnected RDP retained display refresh work");
        using var uncreated=new WindowsRdpHost();
        if(uncreated.RefreshDisplay())throw new Exception("Refresh targeted a missing HWND");
        uncreated.Dispose(); if(uncreated.RefreshDisplay())throw new Exception("Disposed RDP accepted display refresh");
        Console.WriteLine("PASS native RDP repaint invalidates only its retained HWND, skips hidden/missing/disposed hosts, preserves connection state and starts/stops timer through simulated client callbacks; no server or credentials");
    }
    [DllImport("user32.dll")] private static extern bool ValidateRect(nint window,nint rectangle);
    [DllImport("user32.dll")] private static extern bool GetUpdateRect(nint window,nint rectangle,[MarshalAs(UnmanagedType.Bool)] bool erase);
}
