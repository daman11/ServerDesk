using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using ServerDesk.Core;

namespace ServerDesk.App;

// An explicit clipboard-to-drag bridge. Never forwards MOVE feedback to RDP,
// exposes arbitrary shell formats/paths or stages private files in a cache.
[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
internal sealed class WindowsRdpClipboardExport:IClipboardDataObject,IDisposable
{
    private readonly IDataObject source;
    private readonly byte[] descriptor;
    private readonly Func<bool> available;
    private readonly uint sequence;
    private volatile bool disposed;
    private readonly short descriptorFormat,contentsFormat,effectFormat;
    internal RdpClipboardFiles Files { get; }
    internal static WindowsRdpClipboardExport Capture(Func<bool> available)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        Marshal.ThrowExceptionForHR(OleGetClipboard(out var source));
        try { return new(source,available); }
        catch { Marshal.ReleaseComObject(source);throw; }
    }
    internal WindowsRdpClipboardExport(IDataObject data,Func<bool> canUse)
    {
        source=data;available=canUse;
        descriptorFormat=unchecked((short)RegisterClipboardFormatW("FileGroupDescriptorW"));
        contentsFormat=unchecked((short)RegisterClipboardFormatW("FileContents"));
        effectFormat=unchecked((short)RegisterClipboardFormatW("Preferred DropEffect"));
        var format=Format(descriptorFormat,TYMED.TYMED_HGLOBAL);
        if(source.QueryGetData(ref format)!=0)throw new InvalidOperationException(L.T("Skopiuj pliki w zdalnym Eksploratorze przez Ctrl+C, a następnie wybierz Odśwież."));
        source.GetData(ref format,out var medium);
        try
        {
            if(medium.tymed!=TYMED.TYMED_HGLOBAL || medium.unionmember==0)throw new InvalidDataException();
            var size=GlobalSize(medium.unionmember);if(size<4 || size>RdpClipboardFiles.MaximumDescriptorBytes+16)throw new InvalidDataException();
            var pointer=GlobalLock(medium.unionmember);if(pointer==0)throw new InvalidDataException();
            try
            {
                var count=unchecked((uint)Marshal.ReadInt32(pointer));var length=4+(long)count*RdpClipboardFiles.DescriptorBytes;
                if(count is 0 or >RdpClipboardFiles.MaximumEntries || length>(long)size)throw new InvalidDataException();
                descriptor=new byte[(int)length];Marshal.Copy(pointer,descriptor,0,descriptor.Length);
            }
            finally {GlobalUnlock(medium.unionmember);}
        }
        finally {ReleaseStgMedium(ref medium);}
        Files=RdpClipboardFiles.Parse(descriptor);sequence=WindowsRdpHost.GetClipboardSequenceNumber();Check();
    }
    private void Check()
    {
        if(disposed || !available() || WindowsRdpHost.GetClipboardSequenceNumber()!=sequence)
            throw new OperationCanceledException(L.T("Schowek lub sesja zmieniły się. Skopiuj pliki ponownie i odśwież listę."));
    }
    internal bool IsCurrent { get { try { Check();return true; }catch {return false;} } }
    internal bool BeginDrag()
    {
        Check();Marshal.ThrowExceptionForHR(OleInitialize(0));
        try
        {
            var result=DoDragDrop(this,new ClipboardDropSource(()=>IsCurrent),1,out var effect);
            Marshal.ThrowExceptionForHR(result);return result==0x40100 && (effect&1)!=0;
        }
        finally {OleUninitialize();}
    }
    private static FORMATETC Format(short id,TYMED medium,int index=-1)=>new() {cfFormat=id,dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=index,tymed=medium};
    public int QueryGetData(ref FORMATETC format)
    {
        if(!IsCurrent)return unchecked((int)0x80040064);
        return Supported(format) || format.cfFormat==contentsFormat && format.lindex==-1 && format.dwAspect==DVASPECT.DVASPECT_CONTENT && format.ptd==0 && (format.tymed&TYMED.TYMED_ISTREAM)!=0?0:unchecked((int)0x80040064);
    }
    private bool Supported(FORMATETC f)=>f.dwAspect==DVASPECT.DVASPECT_CONTENT && f.ptd==0 &&
        ((f.cfFormat==descriptorFormat || f.cfFormat==effectFormat) && f.lindex==-1 && (f.tymed&TYMED.TYMED_HGLOBAL)!=0 ||
        f.cfFormat==contentsFormat && f.lindex>=0 && f.lindex<Files.Entries.Count && (f.tymed&TYMED.TYMED_ISTREAM)!=0);
    public void GetData(ref FORMATETC format,out STGMEDIUM medium)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        medium=default;Check();if(!Supported(format))throw new COMException("Unsupported file format",unchecked((int)0x80040064));
        if(format.cfFormat==descriptorFormat){medium=Memory(descriptor);return;}
        if(format.cfFormat==effectFormat){medium=Memory(BitConverter.GetBytes(1));return;}
        if(Files.Entries[format.lindex].IsDirectory)
        {
            Marshal.ThrowExceptionForHR(CreateStreamOnHGlobal(0,true,out var empty));
            medium=new() {tymed=TYMED.TYMED_ISTREAM,unionmember=Marshal.GetComInterfaceForObject(new RdpClipboardReadStream(empty,0,Check),typeof(IStream))};return;
        }
        var request=Format(contentsFormat,TYMED.TYMED_ISTREAM,format.lindex);source.GetData(ref request,out var original);
        try
        {
            if(original.tymed!=TYMED.TYMED_ISTREAM || original.unionmember==0)throw new InvalidDataException();
            var stream=(IStream)Marshal.GetObjectForIUnknown(original.unionmember);
            var limited=new RdpClipboardReadStream(stream,Files.Entries[format.lindex].Length,Check);
            limited.Seek(0,0,0);
            medium=new() {tymed=TYMED.TYMED_ISTREAM,unionmember=Marshal.GetComInterfaceForObject(limited,typeof(IStream))};
        }
        finally {ReleaseStgMedium(ref original);}
    }
    private static STGMEDIUM Memory(byte[] bytes)
    {
        var handle=GlobalAlloc(2,(nuint)bytes.Length);if(handle==0)throw new OutOfMemoryException();
        var pointer=GlobalLock(handle);if(pointer==0){GlobalFree(handle);throw new InvalidDataException();}
        try {Marshal.Copy(bytes,0,pointer,bytes.Length);}catch {GlobalUnlock(handle);GlobalFree(handle);throw;}
        GlobalUnlock(handle);return new() {tymed=TYMED.TYMED_HGLOBAL,unionmember=handle};
    }
    public IClipboardFormatEnumerator EnumFormatEtc(DATADIR direction)
    {
        if(direction!=DATADIR.DATADIR_GET)throw new NotSupportedException();
        return new ClipboardFormatEnumerator([Format(descriptorFormat,TYMED.TYMED_HGLOBAL),Format(contentsFormat,TYMED.TYMED_ISTREAM,0),Format(effectFormat,TYMED.TYMED_HGLOBAL)]);
    }
    // Do not propagate performed/logical effect or delete-on-paste feedback.
    public void SetData(ref FORMATETC format,ref STGMEDIUM medium,bool release) { if(release)ReleaseStgMedium(ref medium); }
    public void GetDataHere(ref FORMATETC f,ref STGMEDIUM m)=>throw new NotSupportedException();
    public int GetCanonicalFormatEtc(ref FORMATETC f,out FORMATETC result){result=f;result.ptd=0;return 0x40130;}
    public int DAdvise(ref FORMATETC f,ADVF flags,IAdviseSink sink,out int connection){connection=0;return unchecked((int)0x80040003);}
    public void DUnadvise(int connection)=>throw new NotSupportedException();
    public int EnumDAdvise(out IEnumSTATDATA result){result=null!;return unchecked((int)0x80040003);}
    public void Dispose(){if(disposed)return;disposed=true;if(OperatingSystem.IsWindows() && Marshal.IsComObject(source))Marshal.ReleaseComObject(source);}
    [DllImport("ole32.dll")] private static extern int OleGetClipboard(out IDataObject data);
    [DllImport("ole32.dll")] private static extern int CreateStreamOnHGlobal(nint memory,[MarshalAs(UnmanagedType.Bool)] bool release,out IStream stream);
    [DllImport("ole32.dll")] private static extern int OleInitialize(nint reserved);
    [DllImport("ole32.dll")] private static extern void OleUninitialize();
    [DllImport("ole32.dll")] private static extern int DoDragDrop([MarshalAs(UnmanagedType.Interface)] IClipboardDataObject data,[MarshalAs(UnmanagedType.Interface)] IClipboardDropSource source,uint allowed,out uint effect);
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint RegisterClipboardFormatW(string name);
    [DllImport("kernel32.dll")] private static extern nuint GlobalSize(nint handle);
    [DllImport("kernel32.dll")] private static extern nint GlobalAlloc(uint flags,nuint length);
    [DllImport("kernel32.dll")] private static extern nint GlobalFree(nint handle);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint handle);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint handle);
}

// The framework IEnumFORMATETC marshals pceltFetched as an array sized by
// celt. Native callers provide ONE ULONG, including when celt is 64. Use an
// explicit pointer for that counter to avoid overwriting the caller's stack.
[ComVisible(true),Guid("00000103-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClipboardFormatEnumerator
{
    [PreserveSig] int Next(int count,[Out,MarshalAs(UnmanagedType.LPArray,SizeParamIndex=0)] FORMATETC[] output,nint fetched);
    [PreserveSig] int Skip(int count);
    [PreserveSig] int Reset();
    void Clone(out IClipboardFormatEnumerator copy);
}
[ComVisible(true),Guid("0000010E-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClipboardDataObject
{
    void GetData(ref FORMATETC format,out STGMEDIUM medium);
    void GetDataHere(ref FORMATETC format,ref STGMEDIUM medium);
    [PreserveSig] int QueryGetData(ref FORMATETC format);
    [PreserveSig] int GetCanonicalFormatEtc(ref FORMATETC format,out FORMATETC result);
    void SetData(ref FORMATETC format,ref STGMEDIUM medium,[MarshalAs(UnmanagedType.Bool)] bool release);
    [return:MarshalAs(UnmanagedType.Interface)] IClipboardFormatEnumerator EnumFormatEtc(DATADIR direction);
    [PreserveSig] int DAdvise(ref FORMATETC format,ADVF flags,IAdviseSink sink,out int connection);
    void DUnadvise(int connection);
    [PreserveSig] int EnumDAdvise(out IEnumSTATDATA result);
}

[ComVisible(true),Guid("00000121-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IClipboardDropSource
{
    [PreserveSig] int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escape,uint keys);
    [PreserveSig] int GiveFeedback(uint effect);
}
[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
internal sealed class ClipboardDropSource(Func<bool> available):IClipboardDropSource
{
    public int QueryContinueDrag(bool escape,uint keys)
    {try {return escape || !available()?0x40101:(keys&1)==0?0x40100:0;}catch {return 0x40101;}}
    public int GiveFeedback(uint effect)=>0x40102;
}
[ComVisible(true),ClassInterface(ClassInterfaceType.None)]
internal sealed class ClipboardFormatEnumerator(FORMATETC[] formats,int start=0):IClipboardFormatEnumerator
{
    private int position=start;
    public int Next(int count,FORMATETC[] output,nint fetched)
    {if(fetched!=0)Marshal.WriteInt32(fetched,0);if(count<0 || count>output.Length || fetched==0 && count!=1)return unchecked((int)0x80070057);var copied=0;while(copied<count && position<formats.Length)output[copied++]=formats[position++];if(fetched!=0)Marshal.WriteInt32(fetched,copied);return copied==count?0:1;}
    public int Skip(int count){if(count<0)return unchecked((int)0x80070057);var previous=position;position=(int)Math.Min(formats.Length,(long)position+count);return position-previous==count?0:1;}
    public int Reset(){position=0;return 0;}
    public void Clone(out IClipboardFormatEnumerator result)=>result=new ClipboardFormatEnumerator(formats,position);
}
