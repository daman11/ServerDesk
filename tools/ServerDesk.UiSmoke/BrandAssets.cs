using Avalonia;
using Avalonia.Media.Imaging;
using ServerDesk.App;

internal static class BrandAssets
{
    internal static void Generate(string directory)
    {
        var sizes=new[]{16,24,32,48,64,128,256}; var frames=new List<byte[]>();
        foreach(var size in sizes)
        {
            var mark=new BrandMark { Width=size,Height=size }; mark.Measure(new Size(size,size)); mark.Arrange(new Rect(0,0,size,size));
            using var image=new RenderTargetBitmap(new PixelSize(size,size),new Vector(96,96)); image.Render(mark); using var png=new MemoryStream(); image.Save(png); frames.Add(png.ToArray());
            image.Save(Path.Combine(directory,"serverdesk-"+size+".png"));
        }
        using var ico=File.Create(Path.Combine(directory,"serverdesk.ico")); using var writer=new BinaryWriter(ico);
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length); var offset=6+16*sizes.Length;
        for(var i=0;i<sizes.Length;i++)
        {
            writer.Write((byte)(sizes[i]==256?0:sizes[i])); writer.Write((byte)(sizes[i]==256?0:sizes[i])); writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32); writer.Write(frames[i].Length); writer.Write(offset); offset+=frames[i].Length;
        }
        foreach(var frame in frames)writer.Write(frame);
        Console.WriteLine("Generated ServerDesk application icon in 7 sizes (16–256 px)");
    }
}
