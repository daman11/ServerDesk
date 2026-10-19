using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ServerDesk.App;
using ServerDesk.Core;

internal static class RdpClipboardExportChecks
{
    internal static void Interactive(string root)
    {
        // Fixed synthetic source only. Opens the real product dialog for a
        // local OLE gesture check; this never claims a remote RDP result.
        using var done=new CancellationTokenSource();
        var owner=new Window {Title="ServerDesk — local RDP export fixture",Width=680,Height=700,Position=new PixelPoint(40,100)};
        var timer=new DispatcherTimer {Interval=TimeSpan.FromMinutes(3)};
        timer.Tick+=(_,_)=> {timer.Stop();done.Cancel();};
        owner.Opened+=async (_,_)=>
        {
            try {await RdpClipboardExportDialog.ShowAsync(owner,()=>!done.IsCancellationRequested,done.Token,()=>new(new VirtualFiles(),()=>!done.IsCancellationRequested));}
            finally {timer.Stop();owner.Close();done.Cancel();}
        };
        owner.Show();timer.Start();Dispatcher.UIThread.MainLoop(done.Token);
        foreach(var window in owner.OwnedWindows.ToArray())window.Close();owner.Close();
    }
    internal static void Native(string root)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        var available=true;var original=new VirtualFiles();using var data=new WindowsRdpClipboardExport(original,()=>available);
        if(data.Files.Entries.Count!=3 || data.Files.Bytes!=VirtualFiles.Text.Length+VirtualFiles.Binary.Length)throw new Exception("Virtual file metadata changed");
        var formats=data.EnumFormatEtc(DATADIR.DATADIR_GET);var list=new FORMATETC[3];var count=Marshal.AllocHGlobal(12);
        try {Marshal.WriteInt32(count,0,0x12345678);Marshal.WriteInt32(count,8,0x76543210);formats.Next(3,list,count+4);if(Marshal.ReadInt32(count,4)!=3 || Marshal.ReadInt32(count)!=0x12345678 || Marshal.ReadInt32(count,8)!=0x76543210)throw new Exception("Format counter overwrote native memory");}
        finally {Marshal.FreeHGlobal(count);}
        if(list.Any(f=>f.cfFormat==15))throw new Exception("Export exposed local paths or lost virtual formats");
        NativeEnumeration(formats,list[0].cfFormat);
        var output=Path.Combine(root,"received");Directory.CreateDirectory(output);
        for(var i=1;i<3;i++)
        {
            var f=list[1];f.lindex=i;data.GetData(ref f,out var medium);
            try
            {
                if(medium.tymed!=TYMED.TYMED_ISTREAM)throw new Exception("Expected read-only virtual stream");
                var stream=(IStream)Marshal.GetObjectForIUnknown(medium.unionmember);var content=Read(stream,data.Files.Entries[i].Length);
                var expected=i==1?VirtualFiles.Text:VirtualFiles.Binary;
                if(!content.AsSpan().SequenceEqual(expected))throw new Exception("Virtual file bytes differ");
                var path=Path.Combine(output,i==1?"unicode.txt":"binary.bin");File.WriteAllBytes(path,content);
                File.WriteAllText(path+".sha256",Convert.ToHexString(SHA256.HashData(content)));
            }
            finally {ReleaseStgMedium(ref medium);}
        }
        var copy=new FORMATETC {cfFormat=unchecked((short)RegisterClipboardFormatW("Preferred DropEffect")),dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=-1,tymed=TYMED.TYMED_HGLOBAL};
        data.GetData(ref copy,out var preferred);
        try {var p=GlobalLock(preferred.unionmember);try {if(Marshal.ReadInt32(p)!=1)throw new Exception("Remote files permit Move");}finally {GlobalUnlock(preferred.unionmember);}}
        finally {ReleaseStgMedium(ref preferred);}
        var feedback=copy;feedback.cfFormat=unchecked((short)RegisterClipboardFormatW("Performed DropEffect"));var ignored=new STGMEDIUM();data.SetData(ref feedback,ref ignored,false);
        if(original.Feedback!=0)throw new Exception("Target feedback reached remote source");
        available=false;if(data.QueryGetData(ref copy)==0)throw new Exception("Disconnected source still advertised files");
        try {data.GetData(ref copy,out _);throw new Exception("Disconnected source read succeeded");}catch(OperationCanceledException) { }
        available=true;var source=new ClipboardDropSource(()=>available);
        if(source.QueryContinueDrag(false,1)!=0 || source.QueryContinueDrag(true,1)!=0x40101 || source.QueryContinueDrag(false,0)!=0x40100)throw new Exception("Drag cancellation/release incorrect");
        available=false;if(source.QueryContinueDrag(false,1)!=0x40101)throw new Exception("Disconnected drag did not cancel");
        available=true;
        StreamChecks();
        Console.WriteLine("PASS native RDP clipboard export: Unicode/folder/binary streams and hashes, virtual-only formats, Copy-only, no remote delete feedback, disconnected cancellation, bounded/read-only/truncated streams and seek/copy");
    }
    private static void NativeEnumeration(IClipboardFormatEnumerator formats,short first)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
        formats.Reset();var instance=Marshal.GetComInterfaceForObject(formats,typeof(IClipboardFormatEnumerator));
        var entries=Marshal.AllocHGlobal(64*Marshal.SizeOf<FORMATETC>());var counter=Marshal.AllocHGlobal(4*64+8);
        try
        {
            var guard=Enumerable.Repeat((byte)0x5a,4*64+8).ToArray();Marshal.Copy(guard,0,counter,guard.Length);
            var next=Marshal.GetDelegateForFunctionPointer<EnumNext>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance),3*IntPtr.Size));
            if(next(instance,64,entries,counter+4)!=1 || Marshal.ReadInt32(counter,4)!=3 || Marshal.PtrToStructure<FORMATETC>(entries).cfFormat!=first)throw new Exception("Native format enumeration differs");
            var actual=new byte[guard.Length];Marshal.Copy(counter,actual,0,actual.Length);
            for(var i=0;i<actual.Length;i++)if((i<4 || i>=8) && actual[i]!=guard[i])throw new Exception("Native fetched count overwrote caller memory");
        }
        finally {Marshal.FreeHGlobal(entries);Marshal.FreeHGlobal(counter);Marshal.Release(instance);}
    }
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int EnumNext(nint self,int count,nint entries,nint fetched);
    private static byte[] Read(IStream stream,long size)
    {
        using var output=new MemoryStream();var buffer=new byte[32768];var count=Marshal.AllocHGlobal(4);
        try {while(true){Marshal.WriteInt32(count,0);stream.Read(buffer,buffer.Length,count);var length=Marshal.ReadInt32(count);if(length==0)break;output.Write(buffer,0,length);if(output.Length>size)throw new Exception("Stream exceeded declared size");}}
        finally {Marshal.FreeHGlobal(count);}
        return output.ToArray();
    }
    private static void StreamChecks()
    {
        var available=true;void Validate(){if(!available)throw new OperationCanceledException();}
        var stream=new RdpClipboardReadStream(new MemoryComStream([1,2,3]),3,Validate);
        if(!Read(stream,3).SequenceEqual(new byte[]{1,2,3}))throw new Exception("Stream cap failed");
        stream.Seek(0,0,0);var destination=new MemoryComStream([]);stream.CopyTo(destination,-1,0,0);
        if(!destination.Bytes.SequenceEqual(new byte[]{1,2,3}))throw new Exception("COM copy-all failed");
        stream.Seek(-1,2,0);if(!Read(stream,1).SequenceEqual(new byte[]{3}))throw new Exception("Bounded seek failed");
        try {stream.Seek(4,0,0);throw new Exception("Seek outside descriptor accepted");}catch(IOException) { }
        try {Read(new RdpClipboardReadStream(new MemoryComStream([1]),2,Validate),2);throw new Exception("Truncated remote file accepted");}catch(IOException) { }
        try {Read(new RdpClipboardReadStream(new MemoryComStream([1,2,3]),2,Validate),2);throw new Exception("Growing remote file was silently truncated");}catch(IOException) { }
        available=false;try {stream.Stat(out _,0);throw new Exception("Stale stream remained available");}catch(OperationCanceledException) { }
        try {stream.Write([1],1,0);throw new Exception("Remote stream allowed writes");}catch(NotSupportedException) { }
    }
    internal static void Layout(MainWindow main,string root)
    {
        if(!OperatingSystem.IsWindows())return;
        var previous=AppText.Language;
        try
        {
            foreach(var language in new[]{"pl","en"})
            {
                AppText.SetLanguage(language);var task=RdpClipboardExportDialog.ShowAsync(main,()=>true,CancellationToken.None,()=>new(new VirtualFiles(),()=>true));RedesignChecks.Frames();
                var dialog=main.OwnedWindows.Single();
                foreach(var width in new[]{360,640})
                {
                    dialog.Width=width;RedesignChecks.Frames();
                    var handle=dialog.GetVisualDescendants().OfType<Border>().Single(b=>b.Name=="RdpExportDrag");
                    if(!handle.IsEnabled || handle.Bounds.Width<1)throw new Exception("Export drag unavailable with valid files");
                    var point=handle.TranslatePoint(default,dialog)!.Value;
                    if(point.X<0 || point.X+handle.Bounds.Width>dialog.ClientSize.Width+1)throw new Exception("Export drag escapes dialog");
                    dialog.CaptureRenderedFrame()!.Save(Path.Combine(root,$"export-{language}-{width}.png"));
                }
                dialog.Close();RedesignChecks.Frames();task.GetAwaiter().GetResult();
            }
        }
        finally {AppText.SetLanguage(previous);}
        Console.WriteLine("PASS RDP export dialog: PL/EN, Unicode file preview, 360/640px, drag handle and clean close");
    }
    [ComVisible(true),ClassInterface(ClassInterfaceType.None)]
    private sealed class VirtualFiles:IDataObject
    {
        internal static readonly byte[] Text=Encoding.UTF8.GetBytes("ServerDesk żółć 日本語\r\n");
        internal static readonly byte[] Binary=Enumerable.Range(0,1024*1024+17).Select(i=>(byte)(i*37+11)).ToArray();
        internal int Feedback;
        private readonly short descriptor=unchecked((short)RegisterClipboardFormatW("FileGroupDescriptorW")),contents=unchecked((short)RegisterClipboardFormatW("FileContents"));
        public int QueryGetData(ref FORMATETC f)=>f.cfFormat==descriptor?0:unchecked((int)0x80040064);
        public void GetData(ref FORMATETC f,out STGMEDIUM medium)
        {
            if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException();
            if(f.cfFormat==contents)
            {
                var stream=new MemoryComStream(f.lindex==1?Text:Binary);
                medium=new() {tymed=TYMED.TYMED_ISTREAM,unionmember=Marshal.GetComInterfaceForObject(stream,typeof(IStream))};return;
            }
            if(f.cfFormat!=descriptor)throw new NotSupportedException();
            var bytes=new byte[4+3*592];BitConverter.GetBytes(3).CopyTo(bytes,0);
            var names=new[]{"folder żółć","folder żółć\\日本語.txt","binary.bin"};
            for(var i=0;i<3;i++)
            {
                var p=4+i*592;BitConverter.GetBytes(i==0?4:0x44).CopyTo(bytes,p);BitConverter.GetBytes(i==0?16:128).CopyTo(bytes,p+36);
                BitConverter.GetBytes(i==0?0:i==1?Text.Length:Binary.Length).CopyTo(bytes,p+68);Encoding.Unicode.GetBytes(names[i]).CopyTo(bytes,p+72);
            }
            var handle=GlobalAlloc(2,(nuint)bytes.Length);var pointer=GlobalLock(handle);try {Marshal.Copy(bytes,0,pointer,bytes.Length);}finally {GlobalUnlock(handle);}
            medium=new() {tymed=TYMED.TYMED_HGLOBAL,unionmember=handle};
        }
        public void SetData(ref FORMATETC f,ref STGMEDIUM m,bool release)=>Feedback++;
        public void GetDataHere(ref FORMATETC f,ref STGMEDIUM m)=>throw new NotSupportedException();
        public int GetCanonicalFormatEtc(ref FORMATETC f,out FORMATETC result){result=f;return 1;}
        public IEnumFORMATETC EnumFormatEtc(DATADIR d)=>throw new NotSupportedException();
        public int DAdvise(ref FORMATETC f,ADVF a,IAdviseSink s,out int c){c=0;return unchecked((int)0x80040003);}
        public void DUnadvise(int c)=>throw new NotSupportedException();
        public int EnumDAdvise(out IEnumSTATDATA e){e=null!;return unchecked((int)0x80040003);}
    }
    [ComVisible(true),ClassInterface(ClassInterfaceType.None)]
    private sealed class MemoryComStream(byte[] bytes):IStream
    {
        private readonly MemoryStream memory=new();private bool loaded;
        private MemoryStream Value {get {if(!loaded){memory.Write(bytes);memory.Position=0;loaded=true;}return memory;}}
        internal byte[] Bytes=>Value.ToArray();
        public void Read(byte[] b,int c,nint r){var n=Value.Read(b,0,c);if(r!=0)Marshal.WriteInt32(r,n);}
        public void Write(byte[] b,int c,nint w){Value.Write(b,0,c);if(w!=0)Marshal.WriteInt32(w,c);}
        public void Seek(long o,int k,nint p){var v=Value.Seek(o,(SeekOrigin)k);if(p!=0)Marshal.WriteInt64(p,v);}
        public void Stat(out STATSTG s,int f)=>s=new() {type=2,cbSize=Value.Length};
        public void CopyTo(IStream d,long c,nint r,nint w)=>throw new NotSupportedException();
        public void Clone(out IStream s){s=new MemoryComStream(Bytes);s.Seek(Value.Position,0,0);}
        public void SetSize(long s)=>throw new NotSupportedException();public void Commit(int f)=>throw new NotSupportedException();public void Revert()=>throw new NotSupportedException();
        public void LockRegion(long o,long c,int t)=>throw new NotSupportedException();public void UnlockRegion(long o,long c,int t)=>throw new NotSupportedException();
    }
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern uint RegisterClipboardFormatW(string name);
    [DllImport("kernel32.dll")] private static extern nint GlobalAlloc(uint flags,nuint bytes);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint handle);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint handle);
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
}
