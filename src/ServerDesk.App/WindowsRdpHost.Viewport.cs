using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed partial class WindowsRdpHost
{
    private bool smartSizing=true;
    private (int Width,int Height,double Scale)? lastViewport;
    internal (int Width,int Height)? LastViewportSize { get; private set; }
    internal int? LastViewportError { get; private set; }

    internal RdpDisplaySettings? SynchronizeViewport(bool force=false)
    {
        if(!OperatingSystem.IsWindows() || disposed || control==null || window==0 || scalingRoot==null || !IsEffectivelyVisible || Bounds.Width<=0 || Bounds.Height<=0 ||
            scalingRoot is Window { WindowState:WindowState.Minimized })return null;
        TryUpdateNativeControlPosition();
        if(!GetClientRect(window,out var rectangle) || rectangle.Right<=0 || rectangle.Bottom<=0)return null;
        var scale=scalingRoot?.RenderScaling??1;
        var settings=RdpDisplaySettings.FromViewport(rectangle.Right/scale,rectangle.Bottom/scale,scale);
        var next=(rectangle.Right,rectangle.Bottom,scale);
        if(force || lastViewport!=next)
        {
            try
            {
                // ATL's host HWND and the ActiveX in-place object have separate
                // bounds. Refresh the latter in actual client pixels after DPI
                // and layout settle; server display negotiation is independent.
                var clip=rectangle;
                Marshal.ThrowExceptionForHR(((IOleInPlaceViewport)control).SetObjectRects(ref rectangle,ref clip));
                var advanced=Get("AdvancedSettings9");
                try
                {
                    // Reapply fitting after a monitor change, including when the
                    // server rejects dynamic resolution or keeps its old bitmap.
                    if(smartSizing)Put(advanced,"SmartSizing",false);
                    Put(advanced,"SmartSizing",smartSizing);
                }
                finally { Marshal.ReleaseComObject(advanced); }
                lastViewport=next; LastViewportSize=(next.Right,next.Bottom); LastViewportError=null;
                RefreshDisplay();
            }
            catch(Exception ex) when(ex is COMException or TargetInvocationException or InvalidCastException)
            {
                var cause=ex.InnerException??ex;
                if(LastViewportError!=cause.HResult)ApplicationDiagnostics.Report("Rdp.Viewport",cause,Serilog.Events.LogEventLevel.Warning);
                LastViewportError=cause.HResult;
            }
        }
        return settings;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ViewportRect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")]
    [return:MarshalAs(UnmanagedType.Bool)] private static extern bool GetClientRect(nint window,out ViewportRect rectangle);

    // IOleInPlaceObject, including the inherited IOleWindow slots. This cast uses
    // the existing RCW; do not ReleaseComObject it separately from the RDP control.
    [ComImport,Guid("00000113-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IOleInPlaceViewport
    {
        [PreserveSig] int GetWindow(out nint handle);
        [PreserveSig] int ContextSensitiveHelp([MarshalAs(UnmanagedType.Bool)] bool enter);
        [PreserveSig] int InPlaceDeactivate();
        [PreserveSig] int UIDeactivate();
        [PreserveSig] int SetObjectRects([In] ref ViewportRect position,[In] ref ViewportRect clip);
        [PreserveSig] int ReactivateAndUndo();
    }
}
