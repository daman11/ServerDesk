using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ServerDesk.App;
// The application mark is ServerDesk's own identity; interface glyphs are Lucide.
internal sealed class BrandMark : Control
{
    public override void Render(DrawingContext context)
    {
        var size=Math.Min(Bounds.Width,Bounds.Height); if(size<=0)return;
        using var scale=context.PushTransform(Matrix.CreateScale(size/64,size/64));
        var background=new SolidColorBrush(Color.Parse("#181818")); var accent=new Pen(new SolidColorBrush(Color.Parse("#4C9CE5")),3.5,lineCap:PenLineCap.Round,lineJoin:PenLineJoin.Round); var text=new Pen(new SolidColorBrush(Color.Parse("#E6E6E6")),3,lineCap:PenLineCap.Round,lineJoin:PenLineJoin.Round);
        context.DrawRectangle(background,null,new Rect(2,2,60,60),10,10);
        context.DrawRectangle(null,accent,new Rect(13,13,38,15),3,3);
        context.DrawRectangle(null,accent,new Rect(13,35,38,17),3,3);
        context.DrawEllipse(text.Brush,null,new Point(20,20.5),1.5,1.5);
        context.DrawGeometry(null,text,Geometry.Parse("M21,41 L25,44 21,47 M32,47 L41,47"));
    }
}
