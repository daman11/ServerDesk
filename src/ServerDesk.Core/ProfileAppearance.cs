using System.Buffers.Binary;
using System.IO.Compression;

namespace ServerDesk.Core;

public static class ProfileAppearance
{
    public const int MaximumIconBytes=96*1024;
    public const int MaximumIconSide=128;
    public static readonly string[] Colors=["","#7C8CFF","#4C9CE5","#69CFB6","#E5B65C","#EB8181","#C792EA","#66C5D6","#A8B56A"];
    public static bool IsColor(string? color)=>color!=null && (color.Length==0 || color.Length==7 && color[0]=='#' && color.Skip(1).All(Uri.IsHexDigit));
    public static void Validate(string? color,string? png)
    {
        if(!IsColor(color))throw new ArgumentException(L.T("Kolor musi mieć format #RRGGBB albo być pusty."));
        if(png==null || png.Length>(MaximumIconBytes+2)/3*4)throw new ArgumentException(L.T("Grafika serwera jest zbyt duża."));
        if(png.Length==0)return;
        byte[] data;
        try { data=Convert.FromBase64String(png); } catch(FormatException) { throw new ArgumentException(L.T("Niepoprawny zapis grafiki serwera.")); }
        ValidatePng(data);
    }
    // Only small, static, canonical RGB/RGBA PNGs are stored in profiles. Check their
    // structure and bounded decompressed rows before SQLite/import accepts them.
    public static void ValidatePng(byte[] bytes)
    {
        if(bytes.Length>MaximumIconBytes || bytes.Length<45 || !bytes.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw Invalid();
        var offset=8; var width=0; var height=0; var channels=0; var ended=false; using var compressed=new MemoryStream();
        while(offset<bytes.Length)
        {
            if(offset+12>bytes.Length)throw Invalid();
            var count=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset,4)); if(count>MaximumIconBytes || count>bytes.Length-offset-12)throw Invalid();
            var size=(int)count; var kind=System.Text.Encoding.ASCII.GetString(bytes,offset+4,4); var data=bytes.AsSpan(offset+8,size);
            var crc=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset+8+size,4)); if(Crc(bytes.AsSpan(offset+4,size+4))!=crc)throw Invalid();
            if(offset==8 && kind!="IHDR")throw Invalid();
            switch(kind)
            {
                case "IHDR":
                    if(offset!=8 || size!=13)throw Invalid();
                    var w=BinaryPrimitives.ReadUInt32BigEndian(data[..4]); var h=BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4,4));
                    if(w is <1 or >MaximumIconSide || h is <1 or >MaximumIconSide || data[8]!=8 || data[9] is not (2 or 6) || data[10]!=0 || data[11]!=0 || data[12]!=0)throw Invalid();
                    width=(int)w; height=(int)h; channels=data[9]==6?4:3; break;
                case "IDAT": if(ended)throw Invalid(); compressed.Write(data); break;
                case "IEND": if(size!=0 || offset+12!=bytes.Length || compressed.Length==0)throw Invalid(); ended=true; break;
                case "sRGB": case "sBIT": break;
                default: throw Invalid();
            }
            offset+=12+size;
        }
        if(!ended)throw Invalid(); compressed.Position=0;
        try
        {
            using var decoded=new ZLibStream(compressed,CompressionMode.Decompress); var row=new byte[width*channels+1];
            for(var y=0;y<height;y++) { decoded.ReadExactly(row); if(row[0]>4)throw Invalid(); }
            if(decoded.ReadByte()!=-1)throw Invalid();
        }
        catch(IOException) { throw Invalid(); }
    }
    private static ArgumentException Invalid()=>new(L.T("Grafika profilu musi być poprawnym PNG do 128 × 128 px. Zaimportuj ją w edytorze serwera."));
    private static uint Crc(ReadOnlySpan<byte> data)
    {
        var value=uint.MaxValue; foreach(var item in data) { value^=item; for(var bit=0;bit<8;bit++)value=(value>>1)^((value&1)!=0?0xEDB88320u:0); } return ~value;
    }
}
