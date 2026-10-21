using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia;
using Avalonia.VisualTree;
using Avalonia.Threading;
using ServerDesk.Core;

namespace ServerDesk.App;

// The control and its event subscriptions belong to the session, not to a tab's
// current visual parent. A hidden parking window keeps them alive between tabs.
internal sealed partial class WindowsRdpHost : NativeControlHost, IDisposable
{
    private static readonly Guid EventsId=new("336D5562-EFA8-482E-8CB3-C5C0FC7A7DB6");
    private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly List<(int Id,Delegate Handler)> handlers=[];
    private nint window,parking;
    private object? control;
    private bool disposed;
    internal event Action? LoginComplete;
    internal event Action<int>? Disconnected;
    internal event Action? FocusReleased;
    internal event Action<int>? Reconnecting;
    internal event Action? Reconnected;
    internal event Action? ViewportChanged;
    private TopLevel? scalingRoot;
    private readonly DispatcherTimer repaintTimer=new() { Interval=TimeSpan.FromSeconds(5) };
    internal WindowsRdpHost() { repaintTimer.Tick+=(_,_)=> { if(IsLoggedIn)RefreshDisplay(); }; }
    private delegate void ReconnectHandler(int reason,int attempt,ref int continuation);
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e); scalingRoot=TopLevel.GetTopLevel(this);
        if(scalingRoot!=null)scalingRoot.ScalingChanged+=ScalingChanged;
        if(scalingRoot is Window root)root.Activated+=WindowActivated;
        if(IsLoggedIn)repaintTimer.Start();
        ViewportChanged?.Invoke();
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if(scalingRoot!=null)scalingRoot.ScalingChanged-=ScalingChanged;
        if(scalingRoot is Window root)root.Activated-=WindowActivated;
        repaintTimer.Stop();
        scalingRoot=null; base.OnDetachedFromVisualTree(e);
    }
    private void WindowActivated(object? sender,EventArgs e) { if(IsLoggedIn)RefreshDisplay(); }
    private void ScalingChanged(object? sender,EventArgs e) { TryUpdateNativeControlPosition(); ViewportChanged?.Invoke(); }
    internal bool IsRunning { get; private set; }
    private volatile bool isLoggedIn;
    internal bool IsLoggedIn { get=>isLoggedIn; private set=>isLoggedIn=value; }
    internal bool IsReconnecting { get; private set; }
    internal (int Width,int Height) RemoteDesktopSize { get; private set; }
    internal nint WindowHandle=>window;
    internal Task Ready=>ready.Task;

    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException(L.T("Osadzony pulpit RDP jest dostępny na Windows."));
        try
        {
            if(disposed)throw new ObjectDisposedException(nameof(WindowsRdpHost));
            if(window!=0) { SetParent(window,parent.Handle); return new PlatformHandle(window,"HWND"); }
            if(Thread.CurrentThread.GetApartmentState()!=ApartmentState.STA)throw new InvalidOperationException(L.T("Kontrolka RDP wymaga wątku STA."));
            if(!AtlAxWinInit())throw new InvalidOperationException(L.T("Nie udało się uruchomić hosta RDP Windows."));
            parking=CreateWindowExW(0,"STATIC","",0x80000000,0,0,1,1,0,0,0,0);
            if(parking==0)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            window=CreateWindowExW(0,"AtlAxWin","{8B918B82-7985-4C24-89DF-C33AD2BBFBCD}",0x50000000,0,0,1,1,parent.Handle,0,0,0);
            if(window==0)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            Marshal.ThrowExceptionForHR(AtlAxGetControl(window,out var unknown));
            try { control=Marshal.GetObjectForIUnknown(unknown); } finally { Marshal.Release(unknown); }
            Subscribe(3,(Action)(()=> { IsLoggedIn=true; IsReconnecting=false; repaintTimer.Start(); LoginComplete?.Invoke(); }));
            Subscribe(4,(Action<int>)(reason=> { repaintTimer.Stop(); IsRunning=IsLoggedIn=IsReconnecting=false; Disconnected?.Invoke(reason); }));
            Subscribe(12,(Action<int,int>)((width,height)=>RemoteDesktopSize=(width,height)));
            Subscribe(23,(Action<int>)(_=>FocusReleased?.Invoke()));
            Subscribe(17,(ReconnectHandler)((int reason,int attempt,ref int continuation)=> { repaintTimer.Stop(); IsLoggedIn=false; IsReconnecting=true; continuation=0; Reconnecting?.Invoke(attempt); }));
            Subscribe(33,(Action)(()=> { IsLoggedIn=true; IsReconnecting=false; LastDisplaySettings=null; repaintTimer.Start(); Reconnected?.Invoke(); }));
            RegisterFileDrop();ready.TrySetResult();
            return new PlatformHandle(window,"HWND");
        }
        catch(Exception ex) { ready.TrySetException(ex); Release(); return base.CreateNativeControlCore(parent); }
    }
    protected override void DestroyNativeControlCore(IPlatformHandle handle)
    {
        if(!disposed && parking!=0 && handle.Handle==window) { ShowWindow(handle.Handle,0); SetParent(handle.Handle,parking); }
        else if(handle.Handle!=window)base.DestroyNativeControlCore(handle);
    }
    private void Subscribe(int id,Delegate handler)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        ComEventsHelper.Combine(control!,EventsId,id,handler); handlers.Add((id,handler));
    }
    private object Get(string name)=>control!.GetType().InvokeMember(name,BindingFlags.GetProperty,null,control,null)!;
    private static void Put(object target,string name,object value)=>target.GetType().InvokeMember(name,BindingFlags.SetProperty,null,target,[value]);
    private object? Call(string name,params object[] values)=>control!.GetType().InvokeMember(name,BindingFlags.InvokeMethod,null,control,values);
    internal string Version=>Get("Version").ToString()!;
    internal void Connect(ServerDesk.Core.ServerProfile profile,string password,bool clipboard)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        if(control==null || disposed)throw new InvalidOperationException(L.T("Kontrolka RDP nie jest gotowa."));
        var identity=ServerDesk.Core.RdpIdentity.Parse(profile.Username,profile.Domain);
        Put(control,"Server",profile.Host); Put(control,"UserName",identity.User); Put(control,"Domain",identity.Domain);
        Put(control,"DesktopWidth",profile.Rdp.Width); Put(control,"DesktopHeight",profile.Rdp.Height);
        var advanced=Get("AdvancedSettings9");
        try
        {
            Put(advanced,"RDPPort",profile.Port); Put(advanced,"EnableCredSspSupport",true);
            // Preserve Windows certificate warnings. Never disable identity checks.
            Put(advanced,"AuthenticationLevel",2u); Put(advanced,"RedirectClipboard",clipboard);
            Put(advanced,"RedirectDrives",false); Put(advanced,"RedirectPrinters",false); Put(advanced,"RedirectPorts",false);
            Put(advanced,"RedirectSmartCards",false); Put(advanced,"EnableAutoReconnect",profile.Rdp.AutoReconnect); Put(advanced,"MaxReconnectAttempts",5);
            Put(advanced,"keepAliveInterval",10000); Put(advanced,"overallConnectionTimeout",30); Put(advanced,"singleConnectionTimeout",15);
            Put(advanced,"HotKeyFocusReleaseLeft",0x25); Put(advanced,"HotKeyFocusReleaseRight",0x27);
            var secured=Get("SecuredSettings2"); try { Put(secured,"KeyboardHookMode",(int)profile.Rdp.KeyboardMode); } finally { Marshal.ReleaseComObject(secured); }
            Put(advanced,"SmartSizing",profile.Rdp.DynamicResolution); Put(advanced,"ClearTextPassword",password);
            IsRunning=true; IsLoggedIn=IsReconnecting=false; LastDisplaySettings=null;
            try { Call("Connect"); } catch { IsRunning=false; throw; }
        }
        finally { Marshal.ReleaseComObject(advanced); }
    }
    internal bool RefreshDisplay()
    {
        if(!OperatingSystem.IsWindows() || disposed || window==0 || !IsEffectivelyVisible || scalingRoot is Window { WindowState:WindowState.Minimized } || !IsWindowVisible(window))return false;
        // Avalonia invalidation alone does not repaint the HWND hosted in its
        // native hole. Invalidate only this control and its children, without
        // synchronous paint reentrancy, network probes or synthetic remote input.
        TryUpdateNativeControlPosition(); InvalidateVisual();
        return RedrawWindow(window,0,0,0x0001|0x0002|0x0080); // INVALIDATE | INTERNALPAINT | ALLCHILDREN
    }
    internal void FocusDesktop() { if(window!=0) { RefreshDisplay(); SetFocus(window); } }
    internal void ReleaseKeyboard() { if(TopLevel.GetTopLevel(this)?.TryGetPlatformHandle() is {} root)SetFocus(root.Handle); }
    internal void Disconnect() { repaintTimer.Stop(); if(control!=null && IsRunning) { try { Call("Disconnect"); } catch(Exception ex) when(ex is COMException or TargetInvocationException) { } } IsRunning=IsLoggedIn=IsReconnecting=false; }
    internal string DisconnectDescription(int reason)
    {
        if(reason is 0 or 1 or 2)return L.T("RDP rozłączony · wybierz Połącz ponownie");
        try { return "RDP: "+Call("GetErrorDescription",(uint)reason,Convert.ToUInt32(Get("ExtendedDisconnectReason"))); }
        catch(Exception ex) when(ex is COMException or TargetInvocationException) { return L.F($"Połączenie RDP zakończone (kod {reason}). Sprawdź serwer i poświadczenia."); }
    }
    internal RdpDisplaySettings? LastDisplaySettings { get; private set; }
    internal int? LastDisplayError { get; private set; }
    internal bool ResizeDesktop(int width,int height)
        =>ResizeDesktop(new RdpDisplaySettings(width+(width&1),height,100,100));
    internal bool ResizeDesktop(RdpDisplaySettings settings,bool force=false)
    {
        if(!IsLoggedIn || !force && LastDisplaySettings==settings)return false;
        try { Call("UpdateSessionDisplaySettings",(uint)settings.Width,(uint)settings.Height,0u,0u,0u,(uint)settings.DesktopScaleFactor,(uint)settings.DeviceScaleFactor); LastDisplaySettings=settings; LastDisplayError=null; RefreshDisplay(); return true; }
        catch(Exception ex) when(ex is COMException or TargetInvocationException) { LastDisplayError=(ex.InnerException??ex).HResult; return false; } // SmartSizing remains available on older servers.
    }
    private void Release()
    {
        if(!OperatingSystem.IsWindows())return;
        fileDrop?.Dispose();fileDrop=null;
        if(control!=null)
        {
            foreach(var item in handlers) { try { ComEventsHelper.Remove(control,EventsId,item.Id,item.Handler); } catch(COMException) { } }
            handlers.Clear(); Marshal.ReleaseComObject(control); control=null;
        }
        if(window!=0) { DestroyWindow(window); window=0; }
        if(parking!=0) { DestroyWindow(parking); parking=0; }
    }
    public void Dispose() { if(disposed)return; if(scalingRoot!=null)scalingRoot.ScalingChanged-=ScalingChanged; if(scalingRoot is Window root)root.Activated-=WindowActivated; scalingRoot=null; Disconnect(); disposed=true; Release(); }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("atl.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool AtlAxWinInit();
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("atl.dll")] private static extern int AtlAxGetControl(nint window,out nint unknown);
    [DllImport("user32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern nint CreateWindowExW(uint extended,string name,string title,uint style,int x,int y,int width,int height,nint parent,nint menu,nint instance,nint param);
    [DllImport("user32.dll")] private static extern nint SetParent(nint child,nint parent);
    [DllImport("user32.dll")] private static extern nint SetFocus(nint window);
    [DllImport("user32.dll")] private static extern bool ShowWindow(nint window,int command);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")] [return:MarshalAs(UnmanagedType.Bool)] private static extern bool RedrawWindow(nint window,nint rectangle,nint region,uint flags);
}
