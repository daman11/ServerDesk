using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Avalonia;

namespace ServerDesk.App;

// OLE sends drops over the native desktop HWND directly, bypassing Avalonia's
// routed events. Never revoke another component's existing registration.
[ComVisible(true),Guid("00000122-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IRdpOleDropTarget
{
    [PreserveSig] int DragEnter([MarshalAs(UnmanagedType.Interface)] IDataObject data,uint keys,OleDropPoint point,ref uint effect);
    [PreserveSig] int DragOver(uint keys,OleDropPoint point,ref uint effect);
    [PreserveSig] int DragLeave();
    [PreserveSig] int Drop([MarshalAs(UnmanagedType.Interface)] IDataObject data,uint keys,OleDropPoint point,ref uint effect);
}
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct OleDropPoint(int X,int Y);

[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
internal sealed class WindowsRdpDropTarget:IRdpOleDropTarget,IDisposable
{
    private readonly nint window;
    private readonly Func<bool> available;
    private readonly Action<string[],PixelPoint> dropped;
    private readonly Action<Exception> failed;
    private bool disposed,entered;
    internal WindowsRdpDropTarget(nint handle,Func<bool> canDrop,Action<string[],PixelPoint> onDrop,Action<Exception> onFailure)
    {
        window=handle;available=canDrop;dropped=onDrop;failed=onFailure;
        Marshal.ThrowExceptionForHR(OleInitialize(0));
        try { Marshal.ThrowExceptionForHR(RegisterDragDrop(window,this)); }
        catch { OleUninitialize();throw; }
    }
    private static FORMATETC Format()=>new() { cfFormat=15,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL };
    private void Report(Exception error)
    {
        // No managed exception may escape a native OLE callback, including a
        // failure in the UI error presenter itself.
        try { failed(error); }
        catch(Exception presentationError) { try { ApplicationDiagnostics.Report("RDP.FileDropPresentation",presentationError); } catch { } }
    }
    public int DragEnter(IDataObject data,uint keys,OleDropPoint point,ref uint effect)
    {
        entered=false;
        try { var format=Format();entered=!disposed && available() && (effect&1)!=0 && data.QueryGetData(ref format)==0; }
        catch(Exception ex) { Report(ex); }
        effect=entered?1u:0u;return 0;
    }
    public int DragOver(uint keys,OleDropPoint point,ref uint effect)
    {
        try { effect=!disposed && entered && available() && (effect&1)!=0?1u:0u; }
        catch(Exception ex) { entered=false;effect=0;Report(ex); }
        return 0;
    }
    public int DragLeave() { entered=false;return 0; }
    public int Drop(IDataObject data,uint keys,OleDropPoint point,ref uint effect)
    {
        try
        {
            var accept=!disposed && entered && (effect&1)!=0;entered=false;effect=0;
            if(!accept || !available())return 0;
            var format=Format();data.GetData(ref format,out var medium);
            try
            {
                if(medium.tymed!=TYMED.TYMED_HGLOBAL || medium.unionmember==0)throw new InvalidDataException();
                var count=DragQueryFileW(medium.unionmember,uint.MaxValue,null,0);
                if(count is 0 or >256)throw new ArgumentException(L.T("Wybierz od 1 do 256 plików lub folderów."));
                var paths=new string[count];
                for(uint i=0;i<count;i++)
                {
                    var length=DragQueryFileW(medium.unionmember,i,null,0);
                    if(length is 0 or >32767)throw new InvalidDataException();
                    var path=new StringBuilder((int)length+1);
                    if(DragQueryFileW(medium.unionmember,i,path,(uint)path.Capacity)!=length)throw new InvalidDataException();
                    paths[i]=path.ToString();if(!Path.IsPathFullyQualified(paths[i]))throw new InvalidDataException();
                }
                dropped(paths,new PixelPoint(point.X,point.Y));effect=1; // Copy, never Move/delete the source.
            }
            finally { ReleaseStgMedium(ref medium); }
        }
        catch(Exception ex) { effect=0;Report(ex); }
        return 0;
    }
    public void Dispose()
    { if(disposed)return;disposed=true;entered=false;RevokeDragDrop(window);OleUninitialize(); }
    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("ole32.dll")] private static extern int RegisterDragDrop(nint window,[MarshalAs(UnmanagedType.Interface)] IRdpOleDropTarget target);
    [DllImport("ole32.dll")] private static extern int RevokeDragDrop(nint window);
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] private static extern uint DragQueryFileW(nint drop,uint index,StringBuilder? file,uint size);
}
