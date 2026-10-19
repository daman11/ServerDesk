using System.Drawing.Imaging;
using System.Text;
using System.Runtime.InteropServices;

internal static class ClipboardFixtures
{
    private const string Rtf="{\\rtf1\\ansi\\deff0 {\\fonttbl {\\f0 Segoe UI;}}\\f0\\fs24\\b ServerDesk\\b0  \\i RDP fixture\\i0\\par second line}";
    private static string Html()
    {
        const string before="<html><body><!--StartFragment-->",fragment="<b>ServerDesk</b> <i>żółć λ🙂</i>",after="<!--EndFragment--></body></html>";
        var header="Version:0.9\r\nStartHTML:0000000000\r\nEndHTML:0000000000\r\nStartFragment:0000000000\r\nEndFragment:0000000000\r\n";
        var start=Encoding.UTF8.GetByteCount(header);var fragmentStart=start+Encoding.UTF8.GetByteCount(before);var fragmentEnd=fragmentStart+Encoding.UTF8.GetByteCount(fragment);
        return $"Version:0.9\r\nStartHTML:{start:D10}\r\nEndHTML:{fragmentEnd+Encoding.UTF8.GetByteCount(after):D10}\r\nStartFragment:{fragmentStart:D10}\r\nEndFragment:{fragmentEnd:D10}\r\n"+before+fragment+after;
    }
    internal static void SetRich(nint owner)
    {
        if(!OpenClipboard(owner))throw new InvalidOperationException("Cannot own fixture clipboard");
        try
        {
            if(!EmptyClipboard())throw new InvalidOperationException();
            Put(13,Encoding.Unicode.GetBytes("ServerDesk RDP fixture\0"));
            Put((uint)DataFormats.GetFormat(DataFormats.Rtf).Id,Encoding.ASCII.GetBytes(Rtf+"\0"));
            Put((uint)DataFormats.GetFormat(DataFormats.Html).Id,Encoding.UTF8.GetBytes(Html()+"\0"));
        }
        finally {CloseClipboard();}
        static void Put(uint format,byte[] bytes)
        {
            var memory=GlobalAlloc(2,(nuint)bytes.Length);if(memory==0)throw new OutOfMemoryException();
            var pointer=GlobalLock(memory);if(pointer==0){GlobalFree(memory);throw new InvalidOperationException();}
            try {Marshal.Copy(bytes,0,pointer,bytes.Length);}finally {GlobalUnlock(memory);}
            if(SetClipboardData(format,memory)==0){GlobalFree(memory);throw new InvalidOperationException("Cannot publish fixture format");}
        }
    }
    internal static object VerifyRich()
    {
        var data=Clipboard.GetDataObject();
        var rtf=data?.GetData(DataFormats.Rtf,false) as string;var html=data?.GetData(DataFormats.Html,false) as string;
        var rtfPassed=rtf?.TrimEnd('\0')==Rtf;var htmlPassed=html?.TrimEnd('\0')==Html();
        return new { Passed=rtfPassed&&htmlPassed,RtfPassed=rtfPassed,HtmlPassed=htmlPassed,Formats=data?.GetFormats(false)??[],NativeHtml=NativeHtml() };
    }
    private static object NativeHtml()
    {
        var format=(uint)DataFormats.GetFormat(DataFormats.Html).Id;
        if(!OpenClipboard(0))return new { Available=false,OpenFailed=true };
        try
        {
            var handle=GetClipboardData(format);if(handle==0)return new { Available=false,OpenFailed=false };
            var size=checked((int)GlobalSize(handle));if(size<0 || size>65536)throw new InvalidDataException();
            var pointer=GlobalLock(handle);if(pointer==0)throw new InvalidDataException();
            try
            {
                var bytes=new byte[size];Marshal.Copy(pointer,bytes,0,size);
                var text=Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                return new { Available=true,Bytes=size,ExpectedUtf8=text==Html(),FirstBytes=Convert.ToHexString(bytes.AsSpan(0,Math.Min(16,size))) };
            }
            finally {GlobalUnlock(handle);}
        }
        finally {CloseClipboard();}
    }
    [DllImport("user32.dll")] private static extern bool OpenClipboard(nint owner);
    [DllImport("user32.dll")] private static extern bool CloseClipboard();
    [DllImport("user32.dll")] private static extern nint GetClipboardData(uint format);
    [DllImport("user32.dll")] private static extern nint SetClipboardData(uint format,nint data);
    [DllImport("user32.dll")] private static extern bool EmptyClipboard();
    [DllImport("kernel32.dll")] private static extern nint GlobalAlloc(uint flags,nuint bytes);
    [DllImport("kernel32.dll")] private static extern nint GlobalFree(nint handle);
    [DllImport("kernel32.dll")] private static extern nint GlobalLock(nint handle);
    [DllImport("kernel32.dll")] private static extern bool GlobalUnlock(nint handle);
    [DllImport("kernel32.dll")] private static extern nuint GlobalSize(nint handle);
    private static Color Pixel(int x,int y,bool transparent)=>Color.FromArgb(transparent?(x%3==0?0:x%3==1?128:255):255,(x*7+y*13)%256,(x*3+y*5)%256,(x*17+y*11)%256);
    private static byte[] Dib(string kind,int width,int height)
    {
        var v5=kind=="dibv5"; var header=v5?124:40;var result=new byte[header+width*height*4];
        void I32(int offset,int value)=>BitConverter.GetBytes(value).CopyTo(result,offset);
        void I16(int offset,short value)=>BitConverter.GetBytes(value).CopyTo(result,offset);
        I32(0,header);I32(4,width);I32(8,-height);I16(12,1);I16(14,32);I32(16,v5?3:0);I32(20,width*height*4);I32(24,3780);I32(28,3780);
        if(v5){I32(40,0x00ff0000);I32(44,0x0000ff00);I32(48,0x000000ff);I32(52,unchecked((int)0xff000000));I32(56,0x73524742);I32(108,4);}
        for(var y=0;y<height;y++)for(var x=0;x<width;x++) { var c=Pixel(x,y,v5);var p=header+(y*width+x)*4;result[p]=c.B;result[p+1]=c.G;result[p+2]=c.R;result[p+3]=c.A; }
        return result;
    }
    private static byte[] Png(int width,int height)
    {
        using var image=new Bitmap(width,height,PixelFormat.Format32bppArgb);
        for(var y=0;y<height;y++)for(var x=0;x<width;x++)image.SetPixel(x,y,Pixel(x,y,true));
        using var stream=new MemoryStream();image.Save(stream,ImageFormat.Png);return stream.ToArray();
    }
    internal static void SetImage(string kind,int width,int height)
    {
        var format=kind switch { "dib"=>DataFormats.Dib,"dibv5"=>DataFormats.GetFormat(17).Name,"png"=>"PNG",_=>throw new ArgumentException() };
        // WinForms names predefined CF_DIBV5 as Format17. Do not auto-convert
        // this test data, so readback proves the requested format traversed RDP.
        var data=new DataObject();data.SetData(format,false,new MemoryStream(kind=="png"?Png(width,height):Dib(kind,width,height)));
        Clipboard.SetDataObject(data,true);
    }
    private static byte[]? Bytes(object? value)
    {
        if(value is byte[] bytes)return bytes;
        if(value is Stream stream){using var copy=new MemoryStream();stream.Position=0;stream.CopyTo(copy);return copy.ToArray();}
        return null;
    }
    internal static object VerifyImage(string kind,int width,int height)
    {
        var format=kind switch { "dib"=>DataFormats.Dib,"dibv5"=>DataFormats.GetFormat(17).Name,"png"=>"PNG",_=>throw new ArgumentException() };
        var data=Clipboard.GetDataObject();var bytes=Bytes(data?.GetData(format,false));
        var passed=false;var alpha=false;
        if(bytes!=null && kind=="png")
        {
            using var stream=new MemoryStream(bytes);using var image=new Bitmap(stream);passed=image.Width==width && image.Height==height;alpha=passed;
            if(passed)for(var y=0;y<height;y++)for(var x=0;x<width;x++) { var expected=Pixel(x,y,true);var actual=image.GetPixel(x,y);passed&=expected.ToArgb()==actual.ToArgb();alpha&=expected.A==actual.A; }
        }
        else if(bytes!=null){var expected=Dib(kind,width,height);passed=bytes.AsSpan().SequenceEqual(expected);alpha=kind=="dibv5" && passed;}
        return new { Passed=passed,Format=format,Width=width,Height=height,AlphaExact=alpha,Bytes=bytes?.Length??0,Formats=data?.GetFormats(false)??[] };
    }
}
