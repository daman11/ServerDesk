using Avalonia;
using Avalonia.Media;

namespace ServerDesk.App;
internal sealed class BusyIndicator : DeskIcon
{
    internal static readonly StyledProperty<double> AngleProperty=AvaloniaProperty.Register<BusyIndicator,double>("Angle");
    static BusyIndicator()=>AffectsRender<BusyIndicator>(AngleProperty);
    internal BusyIndicator()
    {
        Glyph="circle-dashed"; Width=Height=16; ClipToBounds=true;
    }
    public override void Render(DrawingContext context)
    {
        var center=new Point(Bounds.Width/2,Bounds.Height/2);
        using var rotation=context.PushTransform(Matrix.CreateTranslation(-center.X,-center.Y)*Matrix.CreateRotation(GetValue(AngleProperty)*Math.PI/180)*Matrix.CreateTranslation(center.X,center.Y));
        base.Render(context);
    }
}
