using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace ServerDesk.App;

// Only trusted, embedded Lucide SVG primitives are accepted. No external SVG or runtime download.
internal class DeskIcon : TemplatedControl
{
    internal static readonly StyledProperty<string> GlyphProperty=AvaloniaProperty.Register<DeskIcon,string>(nameof(Glyph),"file");
    internal string Glyph { get=>GetValue(GlyphProperty); set=>SetValue(GlyphProperty,value); }
    static DeskIcon()=>AffectsRender<DeskIcon>(GlyphProperty,ForegroundProperty);
    public DeskIcon() { Focusable=false; IsHitTestVisible=false; }
    public override void Render(DrawingContext context)
    {
        var size=Math.Min(Bounds.Width,Bounds.Height); if(size<=0)return;
        using var transform=context.PushTransform(Matrix.CreateScale(size/24,size/24)*Matrix.CreateTranslation((Bounds.Width-size)/2,(Bounds.Height-size)/2));
        context.DrawGeometry(null,new Pen(Foreground,2,lineCap:PenLineCap.Round,lineJoin:PenLineJoin.Round),DeskIcons.Geometry(Glyph));
    }
}

internal static partial class DeskIcons
{
    private static readonly Assembly Assembly=typeof(DeskIcons).Assembly;
    private const string Prefix="ServerDesk.App.Assets.Icons.";
    private static readonly HashSet<string> Names=Assembly.GetManifestResourceNames().Where(n=>n.StartsWith(Prefix,StringComparison.Ordinal)&&n.EndsWith(".svg",StringComparison.Ordinal)).Select(n=>n[Prefix.Length..^4]).ToHashSet(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string,Geometry> Cache=new(StringComparer.Ordinal);
    private static readonly IReadOnlyDictionary<string,string> Aliases=new Dictionary<string,string> {
        ["connections"]="server",["favorite"]="star",["recent"]="clock-arrow-left",["history"]="clock-arrow-left",["transfers"]="arrow-up-down",["ssh"]="square-terminal",["rdp"]="monitor",["serial"]="link-2",["close"]="x",["refresh"]="refresh-cw",["sidebar"]="panel-left",["up"]="arrow-up",["link"]="link-2",["delete"]="trash",["edit"]="pencil",["connect"]="log-in",["disconnect"]="log-out",["more"]="ellipsis",["import"]="download",["export"]="upload",["key"]="key-round",["warning"]="triangle-alert",["error"]="circle-alert",["success"]="circle-check",["unknown"]="circle-dashed",["file-json"]="file-braces"
    };
    internal static IReadOnlyCollection<string> Available=>Names;
    internal static string Resolve(string name)=>name=="commands"?"square-terminal":Aliases.TryGetValue(name,out var alias)?alias:Names.Contains(name)||ApplicationPaths.ContainsKey(name)?name:"file";
    internal static Geometry Geometry(string name)=>Cache.GetOrAdd(Resolve(name),Load);
    private static Geometry Load(string name)
    {
        if(ApplicationPaths.TryGetValue(name,out var path))return Avalonia.Media.Geometry.Parse(path);
        using var stream=Assembly.GetManifestResourceStream(Prefix+name+".svg")??throw new InvalidDataException("Missing bundled icon: "+name);
        using var reader=XmlReader.Create(stream,new XmlReaderSettings { DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null });
        var root=XDocument.Load(reader).Root??throw new InvalidDataException("Empty icon");
        if((string?)root.Attribute("viewBox")!="0 0 24 24")throw new InvalidDataException("Unexpected icon viewBox");
        var group=new GeometryGroup();
        foreach(var element in root.Elements())
        {
            if(element.Attribute("transform")!=null)throw new InvalidDataException("Unsupported icon transform: "+name);
            double Number(string key,double fallback=0)=>double.TryParse((string?)element.Attribute(key),NumberStyles.Float,CultureInfo.InvariantCulture,out var value)?value:fallback;
            string Invariant(FormattableString value)=>value.ToString(CultureInfo.InvariantCulture);
            Geometry geometry;
            switch(element.Name.LocalName)
            {
                case "path": geometry=Avalonia.Media.Geometry.Parse((string?)element.Attribute("d")??throw new InvalidDataException("Missing path")); break;
                case "circle": var r=Number("r"); geometry=new EllipseGeometry(new Rect(Number("cx")-r,Number("cy")-r,2*r,2*r)); break;
                case "ellipse": var rx=Number("rx"); var ry=Number("ry"); geometry=new EllipseGeometry(new Rect(Number("cx")-rx,Number("cy")-ry,2*rx,2*ry)); break;
                case "line": geometry=Avalonia.Media.Geometry.Parse(Invariant($"M {Number("x1")},{Number("y1")} L {Number("x2")},{Number("y2")}")); break;
                case "rect":
                    var x=Number("x"); var y=Number("y"); var w=Number("width"); var h=Number("height"); var radius=Number("rx",Number("ry"));
                    geometry=radius==0?new RectangleGeometry(new Rect(x,y,w,h)):Avalonia.Media.Geometry.Parse(Invariant($"M {x+radius},{y} H {x+w-radius} A {radius},{radius} 0 0 1 {x+w},{y+radius} V {y+h-radius} A {radius},{radius} 0 0 1 {x+w-radius},{y+h} H {x+radius} A {radius},{radius} 0 0 1 {x},{y+h-radius} V {y+radius} A {radius},{radius} 0 0 1 {x+radius},{y} Z")); break;
                case "polyline": case "polygon":
                    var values=((string?)element.Attribute("points")??"").Split([' ',',','\r','\n','\t'],StringSplitOptions.RemoveEmptyEntries);
                    if(values.Length<4||values.Length%2!=0)throw new InvalidDataException("Invalid points");
                    var points=Enumerable.Range(0,values.Length/2).Select(i=>values[i*2]+","+values[i*2+1]); geometry=Avalonia.Media.Geometry.Parse("M "+string.Join(" L ",points)+(element.Name.LocalName=="polygon"?" Z":"")); break;
                default: throw new InvalidDataException("Unsupported SVG primitive: "+element.Name.LocalName);
            }
            group.Children.Add(geometry);
        }
        return group;
    }
    internal static DeskIcon Create(string name,double size=DesignTokens.IconSize,string? role=null) { var icon=new DeskIcon { Glyph=Resolve(name),Width=size,Height=size,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center }; if(role!=null)DesignSystem.Resource(icon,TemplatedControl.ForegroundProperty,role); return icon; }
    internal static Control Labelled(string glyph,string text,double size=16,string? iconRole=null)
    {
        var label=new TextBlock { Text=text,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis }; DesignSystem.Resource(label,TextBlock.FontSizeProperty,"FontBody");
        var row=new Grid { ColumnDefinitions=new ColumnDefinitions("Auto,*"),ColumnSpacing=DesignTokens.IconGap,VerticalAlignment=VerticalAlignment.Center,ClipToBounds=true };
        row.Children.Add(Create(glyph,size,iconRole)); Grid.SetColumn(label,1); row.Children.Add(label); return row;
    }
    internal static Button Button(string icon,string label,Action action,double size=DesignTokens.ControlHeight)
    { var button=new Button { Content=Create(icon),Width=size,Height=size,Classes={"icon","chrome"} }; ToolTip.SetTip(button,L.T(label)); AutomationProperties.SetName(button,L.T(label)); button.Click+=(_,_)=>action(); return button; }
}
