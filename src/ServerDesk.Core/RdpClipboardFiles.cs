using System.Buffers.Binary;
using System.Text;

namespace ServerDesk.Core;

public sealed record RdpClipboardFile(string Name,bool IsDirectory,long Length);

// FILEGROUPDESCRIPTORW is untrusted server metadata, not a local filesystem path.
public sealed record RdpClipboardFiles(IReadOnlyList<RdpClipboardFile> Entries,long Bytes)
{
    public const int DescriptorBytes=592,MaximumEntries=10000;
    public const int MaximumDescriptorBytes=4+MaximumEntries*DescriptorBytes;
    public static RdpClipboardFiles Parse(ReadOnlySpan<byte> data)
    {
        if(data.Length<4 || data.Length>MaximumDescriptorBytes)throw Invalid();
        var count=BinaryPrimitives.ReadUInt32LittleEndian(data);
        if(count is 0 or >MaximumEntries || data.Length!=4+(long)count*DescriptorBytes)throw Invalid();
        var entries=new List<RdpClipboardFile>();var names=new Dictionary<string,bool>(StringComparer.OrdinalIgnoreCase);long bytes=0;
        var roots=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for(var i=0;i<count;i++)
        {
            var descriptor=data.Slice(4+i*DescriptorBytes,DescriptorBytes);
            var flags=BinaryPrimitives.ReadUInt32LittleEndian(descriptor);
            var attributes=BinaryPrimitives.ReadUInt32LittleEndian(descriptor[36..]);
            if((flags&4)==0 || (attributes&0x400)!=0)throw Invalid(); // attributes required, no reparse points
            var nameBytes=descriptor.Slice(72,520);var end=-1;
            for(var j=0;j<520;j+=2)if(nameBytes[j]==0 && nameBytes[j+1]==0){end=j;break;}
            if(end<0)throw Invalid();
            string name;try { name=new UnicodeEncoding(false,false,true).GetString(nameBytes[..end]); }catch(DecoderFallbackException){throw Invalid();}
            var parts=name.Split('\\');if(parts.Length>64 || parts.Any(InvalidPart))throw Invalid();
            var directory=(attributes&16)!=0;
            if(!names.TryAdd(name,directory) || !directory && (flags&0x40)==0)throw Invalid();
            roots.Add(parts[0]);if(roots.Count>256)throw Invalid();
            var length=directory?0:((ulong)BinaryPrimitives.ReadUInt32LittleEndian(descriptor[64..])<<32)|BinaryPrimitives.ReadUInt32LittleEndian(descriptor[68..]);
            if(length>(ulong)RdpFileSelection.MaxBytes || bytes>RdpFileSelection.MaxBytes-(long)length)throw Invalid();
            bytes+=(long)length;entries.Add(new(name,directory,(long)length));
        }
        foreach(var entry in entries)
        {
            var parent=entry.Name;
            while(parent.LastIndexOf('\\') is var separator && separator>=0)
            { parent=parent[..separator];if(names.TryGetValue(parent,out var directory) && !directory)throw Invalid(); }
        }
        return new(entries.AsReadOnly(),bytes);
    }
    private static bool InvalidPart(string part)
    {
        if(part.Length==0 || part is "." or ".." || part.EndsWith('.') || part.EndsWith(' ') || part.Any(c=>char.IsControl(c) || "<>:\"/|?*".Contains(c)))return true;
        var stem=part.Split('.')[0].TrimEnd(' ');
        return stem.Equals("CON",StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN",StringComparison.OrdinalIgnoreCase) || stem.Equals("AUX",StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL",StringComparison.OrdinalIgnoreCase) || stem.Equals("CONIN$",StringComparison.OrdinalIgnoreCase) || stem.Equals("CONOUT$",StringComparison.OrdinalIgnoreCase) ||
            stem.Length==4 && (stem.StartsWith("COM",StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT",StringComparison.OrdinalIgnoreCase)) && "123456789¹²³".Contains(stem[3]);
    }
    private static InvalidDataException Invalid()=>new(L.T("Schowek nie zawiera bezpiecznej listy plików RDP. Skopiuj mniejszy zestaw zwykłych plików i spróbuj ponownie."));
}
