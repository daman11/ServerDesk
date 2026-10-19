using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Threading;

namespace ServerDesk.App;

internal sealed partial class WindowsRdpHost
{
    private WindowsRdpDropTarget? fileDrop;
    private bool dropQueued,pastingFiles;
    internal Func<bool>? CanDropFiles { get; set; }
    internal Func<bool>? CanPasteFiles { get; set; }
    internal Action<string[],PixelPoint>? FilesDropped { get; set; }
    internal Action<Exception>? FileDropFailed { get; set; }
    internal bool FileDropRegistered=>fileDrop!=null;
    private void RegisterFileDrop()
    {
        try
        {
            fileDrop=new(window,()=>!dropQueued && !pastingFiles && IsLoggedIn && IsEffectivelyVisible && CanDropFiles?.Invoke()==true,
                (paths,point)=> {dropQueued=true;Dispatcher.UIThread.Post(()=> {try {if(!disposed)FilesDropped?.Invoke(paths,point);}finally {dropQueued=false;} });},
                error=>Dispatcher.UIThread.Post(()=> {if(!disposed)FileDropFailed?.Invoke(error);}));
        }
        catch(Exception error) {ApplicationDiagnostics.Report("RDP.RegisterFileDrop",error);}
    }
    internal NativeDropBounds CaptureDropBounds(PixelPoint point)
    {
        if(!IsLoggedIn || window==0 || !GetWindowRect(window,out var rect) || !rect.Contains(point))throw new InvalidOperationException(L.T("Sesja RDP została rozłączona. Przygotuj pliki ponownie po połączeniu."));
        return rect;
    }
    internal async Task PasteFilesAtAsync(PixelPoint screenPoint,NativeDropBounds originalBounds,uint clipboardSequence,CancellationToken ct)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        if(pastingFiles)throw new InvalidOperationException(L.T("Cel zrzutu lub schowek zmienił się. Upuść pliki ponownie."));
        pastingFiles=true;
        try
        {
            // Let native clipboard notifications run before forwarding paste.
            // Keep automatic RDP redirection enabled throughout the session.
            // Do not switch ManualClipboardSyncEnabled: some system clients
            // cannot resume automatic synchronization after that transition.
            await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background,ct);
            ct.ThrowIfCancellationRequested();
            if(!IsLoggedIn || disposed || CanPasteFiles?.Invoke()!=true || !IsEffectivelyVisible || control==null ||
                !GetWindowRect(window,out var current) || current!=originalBounds || !current.Contains(screenPoint) || GetClipboardSequenceNumber()!=clipboardSequence)
                throw new InvalidOperationException(L.T("Cel zrzutu lub schowek zmienił się. Upuść pliki ponownie."));
            // Only this host's visible child receives input; an obscuring local
            // window, moved viewport or new clipboard owner cancels the paste.
            var target=WindowFromPoint(screenPoint);
            if(target==0 || target!=window && !IsChild(window,target))throw new InvalidOperationException(L.T("Cel zrzutu lub schowek zmienił się. Upuść pliki ponownie."));
            var point=screenPoint;if(!ScreenToClient(target,ref point))throw new InvalidOperationException();
            FocusDesktop();var location=(nint)((point.Y<<16)|(point.X&0xffff));
            SendMessageW(target,0x200,0,location);SendMessageW(target,0x201,1,location);SendMessageW(target,0x202,0,location);
            // The native client queues mouse/clipboard work. Forwarding keys
            // in the same call can overtake that work and lose the paste.
            // Yield with a short, cancellable settling interval; this is not a
            // transfer acknowledgement and must never trigger a blind retry.
            await Task.Delay(150,ct);
            await Dispatcher.UIThread.InvokeAsync(()=>{},DispatcherPriority.Background,ct);
            ct.ThrowIfCancellationRequested();
            if(!IsLoggedIn || disposed || control==null || CanPasteFiles?.Invoke()!=true || !IsEffectivelyVisible ||
                !GetWindowRect(window,out current) || current!=originalBounds || GetClipboardSequenceNumber()!=clipboardSequence ||
                ((target=WindowFromPoint(screenPoint))==0 || target!=window && !IsChild(window,target)))
                throw new InvalidOperationException(L.T("Cel zrzutu lub schowek zmienił się. Upuść pliki ponownie."));
            var id=new Guid("2F079C4C-87B2-4AFD-97AB-20CDB43038AE");
            var unknown=Marshal.GetIUnknownForObject(control);
            try
            {
                Marshal.ThrowExceptionForHR(Marshal.QueryInterface(unknown,in id,out var keys));
                try
                {
                    var send=Marshal.GetDelegateForFunctionPointer<SendRdpKeys>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(keys),14*IntPtr.Size));
                    // Scan codes, with a separate VARIANT_BOOL key-up array.
                    short[] up=[0,0,-1,-1];int[] scans=[0x1d,0x2f,0x2f,0x1d];
                    var u=GCHandle.Alloc(up,GCHandleType.Pinned);var s=GCHandle.Alloc(scans,GCHandleType.Pinned);
                    try {Marshal.ThrowExceptionForHR(send(keys,4,u.AddrOfPinnedObject(),s.AddrOfPinnedObject()));}
                    finally {s.Free();u.Free();}
                }
                finally {Marshal.Release(keys);}
            }
            finally {Marshal.Release(unknown);}
        }
        finally {pastingFiles=false;}
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SendRdpKeys(nint self,int count,nint up,nint data);
    [StructLayout(LayoutKind.Sequential)] internal readonly record struct NativeDropBounds(int Left,int Top,int Right,int Bottom)
    {internal bool Contains(PixelPoint p)=>p.X>=Left && p.Y>=Top && p.X<Right && p.Y<Bottom;}
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window,out NativeDropBounds bounds);
    [DllImport("user32.dll")] private static extern bool ScreenToClient(nint window,ref PixelPoint point);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(PixelPoint point);
    [DllImport("user32.dll")] private static extern bool IsChild(nint parent,nint child);
    [DllImport("user32.dll")] private static extern nint SendMessageW(nint window,uint message,nint wparam,nint lparam);
    [DllImport("user32.dll")] internal static extern uint GetClipboardSequenceNumber();
}
