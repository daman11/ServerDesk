using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
namespace ServerDesk.App;
internal sealed class NetworkLatencyChart : Control
{
    private static readonly StyledProperty<IBrush> LineBrushProperty=AvaloniaProperty.Register<NetworkLatencyChart,IBrush>("LineBrush",Brushes.DodgerBlue);
    private static readonly StyledProperty<IBrush> RuleBrushProperty=AvaloniaProperty.Register<NetworkLatencyChart,IBrush>("RuleBrush",Brushes.Gray);
    private static readonly StyledProperty<IBrush> ErrorBrushProperty=AvaloniaProperty.Register<NetworkLatencyChart,IBrush>("ErrorBrush",Brushes.OrangeRed);
    static NetworkLatencyChart()=>AffectsRender<NetworkLatencyChart>(LineBrushProperty,RuleBrushProperty,ErrorBrushProperty);
    internal NetworkLatencyChart() { DesignSystem.Resource(this,LineBrushProperty,"AccentPrimary"); DesignSystem.Resource(this,RuleBrushProperty,"BorderDefault"); DesignSystem.Resource(this,ErrorBrushProperty,"Danger"); }
    private readonly Queue<double?> samples=[];
    internal void Append(double? latency) { samples.Enqueue(latency); while(samples.Count>360)samples.Dequeue(); InvalidateVisual(); }
    internal void Clear() { samples.Clear(); InvalidateVisual(); }
    public override void Render(DrawingContext context)
    {
        var values=samples.ToArray(); var max=Math.Max(1,values.Where(x=>x.HasValue).Select(x=>x!.Value).DefaultIfEmpty().Max()); var pen=new Pen(GetValue(LineBrushProperty),2); Point? previous=null;
        context.DrawLine(new Pen(GetValue(RuleBrushProperty),1),new Point(0,Bounds.Height-4),new Point(Bounds.Width,Bounds.Height-4));
        for(var i=0;i<values.Length;i++)
        {
            var x=Bounds.Width*i/Math.Max(1,values.Length-1);
            if(values[i] is not {} value) { previous=null; context.DrawLine(new Pen(GetValue(ErrorBrushProperty),2),new Point(x,Bounds.Height-6),new Point(x,Bounds.Height)); continue; }
            var point=new Point(x,4+(Bounds.Height-12)*(1-value/max)); if(previous is {} p)context.DrawLine(pen,p,point); else context.DrawEllipse(pen.Brush,null,point,2,2); previous=point;
        }
    }
}
