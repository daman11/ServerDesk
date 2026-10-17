using System.Xml;
using System.Xml.Linq;
using Avalonia.Media;

namespace ServerDesk.App;

internal static partial class DeskIcons
{
    private const string BrandPrefix="ServerDesk.App.Assets.Brands.";
    private static readonly HashSet<string> BrandNames=typeof(DeskIcons).Assembly.GetManifestResourceNames()
        .Where(n=>n.StartsWith(BrandPrefix,StringComparison.Ordinal) && n.EndsWith(".svg",StringComparison.Ordinal)).Select(n=>n[BrandPrefix.Length..^4]).ToHashSet(StringComparer.Ordinal);
    internal static IReadOnlyCollection<string> Brands=>BrandNames;
    internal static bool IsBrand(string name)=>BrandNames.Contains(name);
    private static Geometry LoadBrand(string name)
    {
        using var stream=typeof(DeskIcons).Assembly.GetManifestResourceStream(BrandPrefix+name+".svg")??throw new InvalidDataException("Missing bundled brand: "+name);
        using var reader=XmlReader.Create(stream,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null });
        var root=XDocument.Load(reader).Root??throw new InvalidDataException("Empty brand icon");
        if((string?)root.Attribute("viewBox")!="0 0 24 24" || root.Elements().Any(e=>e.Name.LocalName is not ("title" or "path") || e.HasElements || e.Attribute("transform")!=null))throw new InvalidDataException("Unexpected brand SVG: "+name);
        var paths=root.Elements().Where(e=>e.Name.LocalName=="path").ToArray();
        if(paths.Length!=1)throw new InvalidDataException("Unexpected brand paths: "+name);
        return Avalonia.Media.Geometry.Parse("F1 "+((string?)paths[0].Attribute("d")??throw new InvalidDataException("Missing brand path")));
    }
}
