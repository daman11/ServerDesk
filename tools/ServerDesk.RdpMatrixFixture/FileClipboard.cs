using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;

internal static class FileClipboard
{
    internal static object Paste(string root)
    {
        // The RDP clipboard supplies virtual files via FILEGROUPDESCRIPTORW and
        // indexed IStream objects, not shared local drives. Accept only this
        // fixture's fixed names and bytes; never follow arbitrary CF_HDROP paths.
        var data=Clipboard.GetDataObject()??throw new InvalidDataException("No clipboard data");
        var formats=data.GetFormats(false);
        if(data.GetData("FileGroupDescriptorW",false) is not Stream descriptor)return new { Passed=false,Formats=formats,Reason="No virtual file descriptor" };
        using var bytes=new MemoryStream();descriptor.Position=0;
        var descriptorBuffer=new byte[4+16*592+1];var descriptorLength=descriptor.ReadAtLeast(descriptorBuffer,descriptorBuffer.Length,false);
        if(descriptorLength==descriptorBuffer.Length)throw new InvalidDataException("Oversized fixture descriptor");
        bytes.Write(descriptorBuffer,0,descriptorLength);var group=bytes.ToArray();
        if(group.Length<4)throw new InvalidDataException();var count=BitConverter.ToInt32(group);
        if(count<1 || count>16 || group.Length<4+count*592)throw new InvalidDataException();
        var expected=new Dictionary<string,byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["ServerDesk żółć test.txt"]=new UTF8Encoding(false).GetBytes(Fixture.Sample),
            ["ServerDesk folder\\nested.txt"]=Encoding.UTF8.GetBytes("Nested RDP fixture"),
            ["ServerDesk binary.bin"]=Enumerable.Range(0,1024*1024+17).Select(i=>(byte)(i*37+11)).ToArray()
        };
        Marshal.ThrowExceptionForHR(OleGetClipboard(out var native));
        var destination=Path.Combine(root,"received-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(destination);
        var files=new List<object>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            for(var index=0;index<count;index++)
            {
                var offset=4+index*592;var name=Encoding.Unicode.GetString(group,offset+72,520).Split('\0')[0];
                if(!seen.Add(name))throw new InvalidDataException("Duplicate fixture descriptor");
                var attributes=BitConverter.ToInt32(group,offset+36);
                if(name.Equals("ServerDesk folder",StringComparison.OrdinalIgnoreCase) && (attributes&16)!=0){Directory.CreateDirectory(Path.Combine(destination,name));continue;}
                if(!expected.TryGetValue(name,out var content))throw new InvalidDataException("Non-fixture file rejected");
                var target=Path.GetFullPath(Path.Combine(destination,name));
                if(!target.StartsWith(destination+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException();
                var format=new FORMATETC { cfFormat=unchecked((short)DataFormats.GetFormat("FileContents").Id),dwAspect=DVASPECT.DVASPECT_CONTENT,lindex=index,tymed=TYMED.TYMED_ISTREAM|TYMED.TYMED_HGLOBAL };
                native.GetData(ref format,out var medium);
                try
                {
                    using var received=new MemoryStream();
                    if(medium.tymed==TYMED.TYMED_ISTREAM)
                    {
                        var stream=(IStream)Marshal.GetObjectForIUnknown(medium.unionmember);
                        var read=Marshal.AllocHGlobal(sizeof(int));
                        try
                        {
                            var buffer=new byte[65536];
                            while(true){stream.Read(buffer,buffer.Length,read);var length=Marshal.ReadInt32(read);if(length<=0)break;if(received.Length+length>content.Length)throw new InvalidDataException("Oversized fixture stream");received.Write(buffer,0,length);}
                        }
                        finally { Marshal.FreeHGlobal(read);Marshal.ReleaseComObject(stream); }
                    }
                    else if(medium.tymed==TYMED.TYMED_HGLOBAL)
                    {
                        var size=checked((int)GlobalSize(medium.unionmember));if(size>content.Length)throw new InvalidDataException();
                        var pointer=GlobalLock(medium.unionmember);if(pointer==0)throw new InvalidDataException();
                        try {var buffer=new byte[size];Marshal.Copy(pointer,buffer,0,size);received.Write(buffer);}
                        finally {GlobalUnlock(medium.unionmember);}
                    }
                    else throw new InvalidDataException("Unsupported file medium");
                    var value=received.ToArray();var match=value.AsSpan().SequenceEqual(content);
                    if(match){Directory.CreateDirectory(Path.GetDirectoryName(target)!);using var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None);output.Write(value);}
                    files.Add(new { Name=name,Passed=match,Bytes=value.Length,Sha256=Convert.ToHexString(SHA256.HashData(value)) });
                    if(!match)return new { Passed=false,Formats=formats,Files=files };
                }
                finally {ReleaseStgMedium(ref medium);}
            }
            return new { Passed=files.Count>0,Formats=formats,Files=files,Destination=Path.GetFileName(destination) };
        }
        finally {Marshal.ReleaseComObject(native);}
    }
    [DllImport("ole32.dll")] private static extern int OleGetClipboard(out System.Runtime.InteropServices.ComTypes.IDataObject data);
    [DllImport("ole32.dll")] private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint handle);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint handle);
    [DllImport("kernel32.dll")] private static extern nuint GlobalSize(nint handle);
}
