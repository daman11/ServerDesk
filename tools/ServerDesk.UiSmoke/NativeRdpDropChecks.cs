using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Avalonia;
using ServerDesk.App;

internal static class NativeRdpDropChecks
{
    internal static void Run(nint parent)
    {
        var window=CreateWindowExW(0,"STATIC","",0x50000000,0,0,32,32,parent,0,0,0);
        if(window==0)throw new Exception("Drop fixture window unavailable");
        var available=true;var count=0;var errors=0;var expected=new[]{@"C:\synthetic\żółć.txt",@"C:\synthetic\folder"};
        var target=new WindowsRdpDropTarget(window,()=>available,(paths,point)=>
        { if(!paths.SequenceEqual(expected) || point!=new PixelPoint(10,20))throw new Exception("OLE drop corrupted paths/point");count++; },_=>errors++);
        try
        {
            var data=new DropData(expected);uint effect=1;
            target.DragEnter(data,0,new(10,20),ref effect);target.Drop(data,0,new(10,20),ref effect);
            if(effect!=1 || count!=1)throw new Exception("Valid native drop rejected");
            effect=1;target.Drop(data,0,new(10,20),ref effect);
            if(effect!=0 || count!=1)throw new Exception("Replayed drop accepted without enter");
            effect=1;target.DragEnter(data,0,new(),ref effect);available=false;effect=1;target.DragOver(0,new(),ref effect);
            if(effect!=0)throw new Exception("Disconnected/disabled drop accepted");available=true;
            effect=1;target.DragEnter(data,0,new(),ref effect);target.DragLeave();effect=1;target.Drop(data,0,new(),ref effect);
            if(effect!=0)throw new Exception("Cancelled drag accepted");
            effect=2;target.DragEnter(data,0,new(),ref effect);if(effect!=0)throw new Exception("Move-only drag accepted");
            foreach(var invalid in new[]{new DropData([]),new DropData(Enumerable.Repeat(expected[0],257).ToArray()),new DropData(["relative.txt"]),new DropData(expected,true)})
            { effect=1;target.DragEnter(invalid,0,new(),ref effect);target.Drop(invalid,0,new(),ref effect);if(effect!=0)throw new Exception("Invalid drop accepted"); }
            if(errors!=4 || count!=1)throw new Exception("Native drop errors escaped or caused unintended copies");
            target.Dispose();target.Dispose();effect=1;target.DragEnter(data,0,new(),ref effect);if(effect!=0)throw new Exception("Disposed drop target accepted data");
            var throwAvailability=false;
            using var replacement=new WindowsRdpDropTarget(window,()=>throwAvailability?throw new InvalidOperationException():true,(_,_)=>throw new Exception(),_=>throw new Exception());
            effect=1;replacement.DragEnter(data,0,new(),ref effect);throwAvailability=true;replacement.DragOver(0,new(),ref effect);
            if(effect!=0)throw new Exception("Throwing availability callback accepted a drag");
            throwAvailability=false;effect=1;replacement.DragEnter(data,0,new(),ref effect);throwAvailability=true;replacement.Drop(data,0,new(),ref effect);
            if(effect!=0)throw new Exception("Throwing availability callback accepted a drop");
            Console.WriteLine("PASS native OLE drop: Unicode paths/point, Copy-only, cancellation, state change, replay, empty/oversize/relative/unreadable payload, callback failures, repeated disposal/re-registration");
        }
        finally {target.Dispose();DestroyWindow(window);}
    }
    private sealed class DropData(string[] paths,bool unreadable=false):IDataObject
    {
        public int QueryGetData(ref FORMATETC format)=>format.cfFormat==15?0:unchecked((int)0x80040064);
        public void GetData(ref FORMATETC format,out STGMEDIUM medium)
        {
            if(unreadable)throw new IOException("Synthetic fixture failure");
            var names=Encoding.Unicode.GetBytes(string.Join('\0',paths)+"\0\0");
            var bytes=new byte[20+names.Length];BitConverter.GetBytes(20).CopyTo(bytes,0);BitConverter.GetBytes(1).CopyTo(bytes,16);names.CopyTo(bytes,20);
            var memory=GlobalAlloc(2,(nuint)bytes.Length);var pointer=GlobalLock(memory);
            try {Marshal.Copy(bytes,0,pointer,bytes.Length);}finally {GlobalUnlock(memory);}
            medium=new() {tymed=TYMED.TYMED_HGLOBAL,unionmember=memory};
        }
        public void GetDataHere(ref FORMATETC f,ref STGMEDIUM m)=>throw new NotSupportedException();
        public int GetCanonicalFormatEtc(ref FORMATETC f,out FORMATETC o){o=f;return 1;}
        public void SetData(ref FORMATETC f,ref STGMEDIUM m,bool release)=>throw new NotSupportedException();
        public IEnumFORMATETC EnumFormatEtc(DATADIR direction)=>throw new NotSupportedException();
        public int DAdvise(ref FORMATETC f,ADVF flags,IAdviseSink sink,out int connection){connection=0;return unchecked((int)0x80040003);}
        public void DUnadvise(int connection)=>throw new NotSupportedException();
        public int EnumDAdvise(out IEnumSTATDATA e){e=null!;return unchecked((int)0x80040003);}
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern nint CreateWindowExW(uint extended,string name,string title,uint style,int x,int y,int width,int height,nint parent,nint menu,nint instance,nint param);
    [DllImport("user32.dll")] private static extern bool DestroyWindow(nint window);
    [DllImport("kernel32.dll")] private static extern nint GlobalAlloc(uint flags,nuint bytes);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint memory);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint memory);
}
