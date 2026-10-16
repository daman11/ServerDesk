using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using ServerDesk.Core;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private WindowGeometry normalWindowBounds=new(0,0,1320,840);
    private bool placementReady,adjustingPlacement,lastWindowMaximized,normalRestorePending,restoreStartupBounds;
    private CancellationTokenSource? placementDelay;
    private void ConfigureWindowPlacement()
    {
        ApplyMainBounds(normalWindowBounds,true);
        PositionChanged+=(_,_)=>QueuePlacementUpdate(false);
        PropertyChanged+=(_,e)=>
        {
            if(e.Property==WindowStateProperty && placementReady && !adjustingPlacement)
            {
                if(WindowState!=WindowState.Minimized)lastWindowMaximized=WindowState==WindowState.Maximized;
                normalRestorePending=WindowState==WindowState.Normal && restoreStartupBounds;
                if(normalRestorePending)restoreStartupBounds=false;
                QueuePlacementUpdate(WindowState==WindowState.Normal);
            }
            else if(e.Property==BoundsProperty)QueuePlacementUpdate(false);
        };
        Screens.Changed+=ScreensChanged;
        ScalingChanged+=(_,_)=>QueuePlacementUpdate(true);
        Closed+=(_,_)=> { Screens.Changed-=ScreensChanged; placementDelay?.Cancel(); };
    }
    private void ScreensChanged(object? sender,EventArgs args)
    {
        QueuePlacementUpdate(true);
        foreach(var floating in floatingDocks.Values.ToArray())RepairFloatingPlacement(floating);
    }
    private async Task RestoreMainPlacementAsync()
    {
        var saved=await repository.MainWindowPlacementAsync(lifetime.Token);
        ApplyMainBounds(saved?.NormalBounds??normalWindowBounds,saved==null);
        lastWindowMaximized=saved?.Maximized==true;
        restoreStartupBounds=lastWindowMaximized;
        placementReady=true;
        if(lastWindowMaximized)WindowState=WindowState.Maximized;
        QueuePlacementUpdate(!lastWindowMaximized);
    }
    private static (double Width,double Height) FrameAllowance(Window window,bool main)
    {
        if(window.ClientSize.Width<=0 || window.ClientSize.Height<=0)return (16,main?16:40);
        var frame=window.FrameSize;
        return (Math.Max(16,frame?.Width-window.ClientSize.Width??0),Math.Max(main?16:40,frame?.Height-window.ClientSize.Height??0));
    }
    private void ApplyMainBounds(WindowGeometry geometry,bool center=false)
    {
        var frame=FrameAllowance(this,true);
        var fit=geometry.Fit(ScreenAreas(),900,600,2400,1600,frame.Width,frame.Height,center);
        adjustingPlacement=true;
        try
        {
            MinWidth=Math.Min(900,fit.Width); MinHeight=Math.Min(600,fit.Height);
            Width=fit.Width; Height=fit.Height; Position=new PixelPoint(fit.X,fit.Y); normalWindowBounds=fit;
        }
        finally { adjustingPlacement=false; }
    }
    private void RememberNormalBounds()
    {
        if(!placementReady || adjustingPlacement || normalRestorePending || WindowState!=WindowState.Normal || !IsVisible)return;
        if(ClientSize.Width>0 && ClientSize.Height>0 && ClientSize.Width<=4096 && ClientSize.Height<=4096)
            normalWindowBounds=new(Position.X,Position.Y,ClientSize.Width,ClientSize.Height);
    }
    private MainWindowPlacement CaptureMainPlacement()
    { RememberNormalBounds(); return new(1,normalWindowBounds,lastWindowMaximized); }
    private void QueuePlacementUpdate(bool repair)
    {
        if(!placementReady || adjustingPlacement || closing)return;
        placementRepairPending|=repair;
        placementDelay?.Cancel(); var delay=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); placementDelay=delay;
        _=UpdatePlacementAfterDelayAsync(delay);
    }
    private bool placementRepairPending;
    private async Task UpdatePlacementAfterDelayAsync(CancellationTokenSource delay)
    {
        try
        {
            await Task.Delay(150,delay.Token);
            if(!IsVisible || closing)return;
            var repair=placementRepairPending; placementRepairPending=false;
            if(WindowState==WindowState.Normal)
            {
                // Native restore can briefly report the initial default size. Keep
                // the last normal rectangle until the transition has settled.
                if(!normalRestorePending)RememberNormalBounds();
                if(repair)ApplyMainBounds(normalWindowBounds);
                normalRestorePending=false;
            }
        }
        catch(OperationCanceledException) when(delay.IsCancellationRequested) { }
        finally { if(ReferenceEquals(placementDelay,delay))placementDelay=null; delay.Dispose(); }
    }
    private void RepairFloatingPlacement(FloatingDock floating)
    {
        if(closing || !floating.Window.IsVisible || floating.Window.WindowState!=WindowState.Normal || floating.Window.ClientSize.Width<=0 || floating.Window.ClientSize.Height<=0)return;
        var w=floating.Window;
        ApplyFloatingGeometry(floating,new(w.Position.X,w.Position.Y,w.ClientSize.Width,w.ClientSize.Height));
    }
}
