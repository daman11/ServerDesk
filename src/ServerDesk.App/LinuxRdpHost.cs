using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

// Avalonia owns the X11 holder; a retained native child survives tab changes and
// moves between top levels. Only the owned FreeRDP process may create its desktop.
internal sealed class LinuxRdpHost:NativeControlHost,IDisposable
{
    private readonly TaskCompletionSource ready=new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly DispatcherTimer timer=new() { Interval=TimeSpan.FromMilliseconds(200) };
    private IPlatformHandle? retained;
    private nint display,desktop;
    private TopLevel? scalingRoot;
    private LinuxRdpProcessSession? process;
    private bool disposed;
    private PixelSize lastSize;
    internal Task Ready=>ready.Task;
    internal Task Shutdown { get; private set; }=Task.CompletedTask;
    internal bool IsRunning=>process?.IsRunning==true;
    internal bool IsLoggedIn { get; private set; }
    internal nint WindowHandle=>retained?.Handle??0;
    internal nint DesktopHandle=>desktop;
    internal int ProcessId=>process?.ProcessId??0;
    internal event Action? DesktopCreated;
    internal event Action<int>? Disconnected;
    internal event Action? FocusReleased;
    internal event Action? FocusEntered;
    internal LinuxRdpHost() { timer.Tick+=(_,_)=>Poll(); SizeChanged+=(_,_)=>ResizeDesktop(); }
    protected override IPlatformHandle CreateNativeControlCore(IPlatformHandle parent)
    {
        if(disposed)throw new ObjectDisposedException(nameof(LinuxRdpHost));
        if(!OperatingSystem.IsLinux() || parent.HandleDescriptor!="XID")throw new PlatformNotSupportedException(L.T("Osadzony pulpit Linux wymaga backendu X11/XWayland."));
        if(retained!=null)return retained;
        try
        {
            display=X11.Connect(); if(display==0)throw new InvalidOperationException(L.T("Nie można otworzyć połączenia X11 dla pulpitu RDP."));
            retained=base.CreateNativeControlCore(parent);
            GrabReleaseKeys(); timer.Start(); ready.TrySetResult(); return retained;
        }
        catch(Exception ex) { ready.TrySetException(ex); throw; }
    }
    protected override void DestroyNativeControlCore(IPlatformHandle handle)
    {
        // NativeControlHost first reparents the holder to its hidden orphan window.
        // Do not destroy the desktop while this session can be docked again.
        if(!ReferenceEquals(handle,retained))base.DestroyNativeControlCore(handle);
    }
    internal async Task StartAsync(RdpLaunch launch,string password,CancellationToken ct)
    {
        if(disposed || retained==null)throw new InvalidOperationException(L.T("Osadzony pulpit RDP nie ma gotowego okna X11."));
        if(process!=null)throw new InvalidOperationException(L.T("Klient RDP jest już uruchomiony w osobnym oknie."));
        process=await LinuxRdpProcessSession.StartAsync(launch.Executable,launch.Arguments,password,ct);
        _=ObserveAsync(process); Poll();
    }
    private async Task ObserveAsync(LinuxRdpProcessSession observed)
    {
        int code;
        try { code=await observed.Completion; await observed.StopAsync(); }
        catch(Exception ex) { Serilog.Log.Warning("RDP child cleanup failed: {Type}",ex.GetType().Name); code=-1; }
        await Dispatcher.UIThread.InvokeAsync(()=>
        {
            if(disposed || !ReferenceEquals(process,observed))return;
            IsLoggedIn=false; desktop=0; Disconnected?.Invoke(code);
        });
    }
    private void Poll()
    {
        if(disposed || display==0 || retained==null)return;
        var events=X11.Events(display);
        if(events.Release) { if(TopLevel.GetTopLevel(this)?.TryGetPlatformHandle() is {} top)X11.Focus(display,top.Handle); FocusReleased?.Invoke(); }
        if(events.Focus)FocusEntered?.Invoke();
        if(!IsRunning)return;
        foreach(var candidate in X11.Children(display,retained.Handle))
        {
            if(X11.Pid(display,candidate)!=ProcessId)continue;
            if(desktop!=(nint)candidate) { desktop=(nint)candidate; lastSize=default; X11.WatchFocus(display,desktop); }
            ResizeDesktop();
            if(!IsLoggedIn) { IsLoggedIn=true; DesktopCreated?.Invoke(); }
            break;
        }
    }
    private void ResizeDesktop()
    {
        if(disposed || display==0 || desktop==0 || !IsRunning || !IsEffectivelyVisible || Bounds.Width<=0 || Bounds.Height<=0)return;
        var scale=TopLevel.GetTopLevel(this)?.RenderScaling??1;
        var size=new PixelSize(Math.Clamp((int)Math.Ceiling(Bounds.Width*scale),1,32768),Math.Clamp((int)Math.Ceiling(Bounds.Height*scale),1,32768));
        if(size==lastSize)return;
        lastSize=size; X11.Resize(display,desktop,(uint)size.Width,(uint)size.Height);
    }
    internal void FocusDesktop()
    {
        if(disposed || display==0 || desktop==0 || !IsRunning || !IsEffectivelyVisible)return;
        X11.Focus(display,desktop);
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    { base.OnAttachedToVisualTree(e); scalingRoot=TopLevel.GetTopLevel(this); if(scalingRoot!=null)scalingRoot.ScalingChanged+=ScalingChanged; lastSize=default; ResizeDesktop(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { if(scalingRoot!=null)scalingRoot.ScalingChanged-=ScalingChanged; scalingRoot=null; base.OnDetachedFromVisualTree(e); }
    private void ScalingChanged(object? sender,EventArgs e) { TryUpdateNativeControlPosition(); ResizeDesktop(); }
    private void GrabReleaseKeys()
    {
        X11.GrabReleaseKeys(display,retained!.Handle);
    }
    public void Dispose()
    {
        if(disposed)return; disposed=true; timer.Stop(); IsLoggedIn=false; Shutdown=CloseAsync();
    }
    private async Task CloseAsync()
    {
        try { if(process!=null)await process.StopAsync(); }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(()=>
            {
                if(retained!=null) { base.DestroyNativeControlCore(retained); retained=null; }
                if(display!=0) { X11.Disconnect(display); display=0; }
            },DispatcherPriority.Background);
        }
    }
    private static class X11
    {
        // Checked XCB requests have per-request errors. A child closing during a
        // resize cannot change Avalonia's global Xlib error handler or abort the UI.
        private const string Library="libxcb.so.1";
        [DllImport("libc.so.6")] private static extern void free(nint pointer);
        [DllImport(Library)] private static extern nint xcb_connect(string? name,out int screen);
        [DllImport(Library)] private static extern int xcb_connection_has_error(nint connection);
        [DllImport(Library)] private static extern void xcb_disconnect(nint connection);
        [DllImport(Library)] private static extern uint xcb_query_tree(nint connection,uint window);
        [DllImport(Library)] private static extern nint xcb_query_tree_reply(nint connection,uint cookie,out nint error);
        [DllImport(Library)] private static extern int xcb_query_tree_children_length(nint reply);
        [DllImport(Library)] private static extern nint xcb_query_tree_children(nint reply);
        [DllImport(Library)] private static extern uint xcb_intern_atom(nint connection,byte onlyIfExists,ushort length,string name);
        [DllImport(Library)] private static extern nint xcb_intern_atom_reply(nint connection,uint cookie,out nint error);
        [DllImport(Library)] private static extern uint xcb_get_property(nint connection,byte delete,uint window,uint property,uint type,uint offset,uint length);
        [DllImport(Library)] private static extern nint xcb_get_property_reply(nint connection,uint cookie,out nint error);
        [DllImport(Library)] private static extern int xcb_get_property_value_length(nint reply);
        [DllImport(Library)] private static extern nint xcb_get_property_value(nint reply);
        [DllImport(Library)] private static extern uint xcb_configure_window_checked(nint connection,uint window,ushort mask,uint[] values);
        [DllImport(Library)] private static extern uint xcb_change_window_attributes_checked(nint connection,uint window,uint mask,uint[] values);
        [DllImport(Library)] private static extern uint xcb_set_input_focus_checked(nint connection,byte revert,uint window,uint time);
        [DllImport(Library)] private static extern uint xcb_grab_key_checked(nint connection,byte owner,uint window,ushort modifiers,byte key,byte pointerMode,byte keyboardMode);
        [DllImport(Library)] private static extern nint xcb_request_check(nint connection,uint cookie);
        [DllImport(Library)] private static extern nint xcb_poll_for_event(nint connection);
        [DllImport("libX11.so.6")] private static extern nint XOpenDisplay(string? name);
        [DllImport("libX11.so.6")] private static extern int XCloseDisplay(nint display);
        [DllImport("libX11.so.6")] private static extern nint XStringToKeysym(string name);
        [DllImport("libX11.so.6")] private static extern byte XKeysymToKeycode(nint display,nint symbol);
        internal static nint Connect()
        {
            var connection=xcb_connect(null,out _); if(connection!=0 && xcb_connection_has_error(connection)==0)return connection;
            if(connection!=0)xcb_disconnect(connection); return 0;
        }
        internal static void Disconnect(nint connection)=>xcb_disconnect(connection);
        private static void Checked(nint connection,uint cookie) { var error=xcb_request_check(connection,cookie); if(error!=0)free(error); }
        internal static uint[] Children(nint connection,nint window)
        {
            var reply=xcb_query_tree_reply(connection,xcb_query_tree(connection,checked((uint)window.ToInt64())),out var error);
            try
            {
                if(reply==0 || error!=0)return [];
                var length=xcb_query_tree_children_length(reply); if(length is <0 or >4096)return [];
                var values=xcb_query_tree_children(reply); var result=new uint[length]; for(var i=0;i<length;i++)result[i]=unchecked((uint)Marshal.ReadInt32(values,i*4)); return result;
            }
            finally { if(reply!=0)free(reply); if(error!=0)free(error); }
        }
        internal static int Pid(nint connection,uint window)
        {
            const string name="_NET_WM_PID";
            var atomReply=xcb_intern_atom_reply(connection,xcb_intern_atom(connection,1,(ushort)name.Length,name),out var atomError);
            uint atom=0; try { if(atomReply!=0 && atomError==0)atom=unchecked((uint)Marshal.ReadInt32(atomReply,8)); } finally { if(atomReply!=0)free(atomReply); if(atomError!=0)free(atomError); }
            if(atom==0)return 0;
            var reply=xcb_get_property_reply(connection,xcb_get_property(connection,0,window,atom,0,0,1),out var error);
            try { return reply!=0 && error==0 && Marshal.ReadByte(reply,1)==32 && xcb_get_property_value_length(reply)==4?Marshal.ReadInt32(xcb_get_property_value(reply)):0; }
            finally { if(reply!=0)free(reply); if(error!=0)free(error); }
        }
        internal static void Resize(nint connection,nint window,uint width,uint height)=>Checked(connection,xcb_configure_window_checked(connection,checked((uint)window.ToInt64()),15,[0,0,width,height]));
        internal static void Focus(nint connection,nint window)=>Checked(connection,xcb_set_input_focus_checked(connection,2,checked((uint)window.ToInt64()),0));
        internal static void WatchFocus(nint connection,nint window)=>Checked(connection,xcb_change_window_attributes_checked(connection,checked((uint)window.ToInt64()),2048,[1u<<21]));
        internal static (bool Release,bool Focus) Events(nint connection)
        {
            var pressed=false; var focused=false; nint input;
            while((input=xcb_poll_for_event(connection))!=0) { var type=Marshal.ReadByte(input)&127; if(type==2)pressed=true; if(type==9)focused=true; free(input); } return (pressed,focused);
        }
        internal static void GrabReleaseKeys(nint connection,nint window)
        {
            var mapping=XOpenDisplay(null); if(mapping==0)return;
            try { foreach(var symbol in new[]{"Left","Right"})foreach(var locks in new ushort[]{0,2,16,18}) { var key=XKeysymToKeycode(mapping,XStringToKeysym(symbol)); if(key!=0)Checked(connection,xcb_grab_key_checked(connection,0,checked((uint)window.ToInt64()),(ushort)(12|locks),key,1,1)); } }
            finally { XCloseDisplay(mapping); }
        }
    }
}
