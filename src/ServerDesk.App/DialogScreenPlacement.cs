using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed class DialogScreenPlacement
{
    private static readonly ConditionalWeakTable<Window,DialogScreenPlacement> tracked=new();
    private readonly Window dialog,owner;
    private readonly double minimumWidth,minimumHeight,maximumWidth,maximumHeight;
    private Func<ScreenArea?>? screenProvider;
    private readonly DispatcherTimer delay=new() { Interval=TimeSpan.FromMilliseconds(150) };
    private ScreenArea? previous;
    private bool adjusting,repairPending,closed;

    private DialogScreenPlacement(Window dialog,Window owner,Func<ScreenArea?>? screenProvider)
    {
        this.dialog=dialog; this.owner=owner;
        this.screenProvider=screenProvider;
        minimumWidth=dialog.MinWidth; minimumHeight=dialog.MinHeight;
        maximumWidth=dialog.MaxWidth; maximumHeight=dialog.MaxHeight;
        dialog.Opened+=Opened; dialog.PositionChanged+=PositionChanged; dialog.ScalingChanged+=ScalingChanged;
        dialog.Screens.Changed+=ScreensChanged; dialog.Closed+=Closed;
        delay.Tick+=(_,_)=> { delay.Stop(); var repair=repairPending; repairPending=false; Refresh(repair); };
    }
    internal static DialogScreenPlacement Track(Window dialog,Window owner,Func<ScreenArea?>? screenProvider=null)
    { var placement=tracked.GetValue(dialog,key=>new(key,owner,screenProvider)); if(screenProvider!=null)placement.screenProvider=screenProvider; placement.Refresh(false); return placement; }
    private void Opened(object? sender,EventArgs args)=>Queue(true);
    private void PositionChanged(object? sender,PixelPointEventArgs args)=>Queue(false);
    private void ScalingChanged(object? sender,EventArgs args)=>Queue(true);
    private void ScreensChanged(object? sender,EventArgs args)=>Queue(true);
    private void Queue(bool repair)
    {
        if(closed || adjusting)return;
        if(screenProvider!=null) { if(screenProvider() is {} area)Apply(area,repair); return; }
        repairPending|=repair; delay.Stop(); delay.Start();
    }
    private void Refresh(bool repair)
    {
        if(closed || adjusting)return;
        var screen=dialog.IsVisible?dialog.Screens.ScreenFromWindow(dialog):owner.Screens.ScreenFromWindow(owner);
        screen??=dialog.Screens.Primary;
        if(screen!=null)Apply(new(screen.WorkingArea.X,screen.WorkingArea.Y,screen.WorkingArea.Width,screen.WorkingArea.Height,screen.Scaling),repair);
    }
    // Also used by the opt-in UI harness to model monitor changes without changing OS settings.
    internal void Apply(ScreenArea area,bool repair)
    {
        if(closed || adjusting || area.Width<=0 || area.Height<=32 || !double.IsFinite(area.Scale) || area.Scale is <.5 or >4)return;
        adjusting=true;
        try
        {
            var maxWidth=Math.Min(maximumWidth,Math.Max(1,Math.Min(4096,area.Width/area.Scale-40)));
            var maxHeight=Math.Min(maximumHeight,Math.Max(1,Math.Min(4096,area.Height/area.Scale-64)));
            // Restore the original limits after returning to a larger display.
            dialog.MinWidth=Math.Min(dialog.MinWidth,maxWidth); dialog.MinHeight=Math.Min(dialog.MinHeight,maxHeight);
            dialog.MaxWidth=maxWidth; dialog.MaxHeight=maxHeight;
            dialog.MinWidth=Math.Min(minimumWidth,maxWidth); dialog.MinHeight=Math.Min(minimumHeight,maxHeight);
            if(double.IsFinite(dialog.Width))dialog.Width=Math.Clamp(dialog.Width,dialog.MinWidth,maxWidth);
            if(double.IsFinite(dialog.Height))dialog.Height=Math.Clamp(dialog.Height,dialog.MinHeight,maxHeight);
            if(dialog.IsVisible && dialog.WindowState==WindowState.Normal && (repair || previous!=area) && dialog.ClientSize.Width>0 && dialog.ClientSize.Height>0)
            {
                var frame=dialog.FrameSize;
                var frameWidth=Math.Max(16,(frame?.Width??0)-dialog.ClientSize.Width);
                var frameHeight=Math.Max(40,(frame?.Height??0)-dialog.ClientSize.Height);
                var geometry=new WindowGeometry(dialog.Position.X,dialog.Position.Y,Math.Clamp(dialog.ClientSize.Width,1,maxWidth),Math.Clamp(dialog.ClientSize.Height,1,maxHeight));
                var fit=geometry.Fit([area],1,1,maxWidth,maxHeight,frameWidth,frameHeight);
                dialog.Position=new PixelPoint(fit.X,fit.Y);
            }
            previous=area;
        }
        finally { adjusting=false; }
    }
    private void Closed(object? sender,EventArgs args)
    {
        closed=true; delay.Stop();
        dialog.Opened-=Opened; dialog.PositionChanged-=PositionChanged; dialog.ScalingChanged-=ScalingChanged;
        dialog.Screens.Changed-=ScreensChanged; dialog.Closed-=Closed; tracked.Remove(dialog);
    }
}
