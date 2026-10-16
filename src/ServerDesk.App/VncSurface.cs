using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using MarcusW.VncClient;
using MarcusW.VncClient.Protocol.Implementation.MessageTypes.Outgoing;
using ServerDesk.Infrastructure;
using Size=Avalonia.Size;

namespace ServerDesk.App;

internal sealed class VncSurface : Control,IDisposable
{
    private VncSession? session;
    private WriteableBitmap? bitmap;
    private readonly Dictionary<Key,uint> pressed=[];
    private int pending;
    private bool viewOnly,fit=true;
    private bool altGr;
    private Point lastPointer;
    internal event Action? EscapeFocus;
    internal int RenderedFrames { get; private set; }
    internal bool IsComposingAltGr=>altGr;
    internal bool ViewOnly { get=>viewOnly; set { if(value)ReleaseInputs(); viewOnly=value; } }
    internal bool FitToWindow { get=>fit; set { fit=value; InvalidateMeasure(); InvalidateVisual(); } }
    internal VncSurface()
    {
        Focusable=true; ClipToBounds=true; Name="VncDesktop";
        Avalonia.Automation.AutomationProperties.SetName(this,L.T("Pulpit VNC"));
        LostFocus+=(_,_)=>ReleaseInputs(); PointerCaptureLost+=(_,_)=>ReleasePointer(); DetachedFromVisualTree+=(_,_)=>ReleaseInputs();
    }
    internal void Attach(VncSession? next)
    {
        ReleaseInputs(); if(session!=null)session.Framebuffer.Updated-=FrameReady;
        session=next; bitmap?.Dispose(); bitmap=null;
        if(next!=null) { next.Framebuffer.Updated+=FrameReady; FrameReady(); }
        InvalidateMeasure(); InvalidateVisual();
    }
    private void FrameReady()
    {
        if(Interlocked.Exchange(ref pending,1)!=0)return;
        Dispatcher.UIThread.Post(()=>
        {
            Interlocked.Exchange(ref pending,0);
            session?.Framebuffer.Read((size,pixels)=>
            {
                if(bitmap?.PixelSize!=new PixelSize(size.Width,size.Height))
                { bitmap?.Dispose(); bitmap=new(new PixelSize(size.Width,size.Height),new Vector(96,96),Avalonia.Platform.PixelFormat.Bgra8888,AlphaFormat.Opaque); InvalidateMeasure(); }
                using var reference=bitmap.Lock();
                for(var row=0;row<size.Height;row++)Marshal.Copy(pixels,row*size.Width*4,reference.Address+row*reference.RowBytes,size.Width*4);
                RenderedFrames++;
            });
            InvalidateVisual();
        },DispatcherPriority.Render);
    }
    protected override Size MeasureOverride(Size available)=>fit?new(double.IsFinite(available.Width)?available.Width:640,double.IsFinite(available.Height)?available.Height:480):new(bitmap?.PixelSize.Width??640,bitmap?.PixelSize.Height??480);
    private Rect ImageRect()
    {
        if(bitmap==null || Bounds.Width<=0 || Bounds.Height<=0)return default;
        var scale=fit?Math.Min(Bounds.Width/bitmap.PixelSize.Width,Bounds.Height/bitmap.PixelSize.Height):1;
        var width=bitmap.PixelSize.Width*scale; var height=bitmap.PixelSize.Height*scale;
        return new((Bounds.Width-width)/2,(Bounds.Height-height)/2,width,height);
    }
    public override void Render(DrawingContext context)
    { context.FillRectangle(Brushes.Black,new Rect(Bounds.Size)); if(bitmap!=null)context.DrawImage(bitmap,new Rect(bitmap.Size),ImageRect()); }
    private bool CanSend=>!viewOnly && session?.IsConnected==true;
    private void KeyMessage(uint symbol,bool down)
    { if(session?.IsConnected==true)session.Connection.EnqueueMessage(new KeyEventMessage(down,(KeySymbol)symbol),CancellationToken.None); }
    internal void ReleaseInputs()
    {
        foreach(var symbol in pressed.Values)KeyMessage(symbol,false); pressed.Clear();
        altGr=false;
        ReleasePointer();
    }
    private void ReleasePointer()
    {
        if(session?.IsConnected==true)session.Connection.EnqueueMessage(new PointerEventMessage(new Position((int)lastPointer.X,(int)lastPointer.Y),0),CancellationToken.None);
    }
    internal void SendCtrlAltDelete()
    { if(!CanSend)return; ReleaseInputs(); foreach(var key in new uint[]{0xffe3,0xffe9,0xffff})KeyMessage(key,true); foreach(var key in new uint[]{0xffff,0xffe9,0xffe3})KeyMessage(key,false); }
    private void Pointer(PointerEventArgs e,int? wheel=null)
    {
        if(!CanSend || bitmap==null)return;
        var point=e.GetPosition(this); var rect=ImageRect(); if(rect.Width<=0 || !rect.Contains(point) && e.Pointer.Captured!=this)return;
        lastPointer=new(Math.Clamp((point.X-rect.X)*bitmap.PixelSize.Width/rect.Width,0,bitmap.PixelSize.Width-1),Math.Clamp((point.Y-rect.Y)*bitmap.PixelSize.Height/rect.Height,0,bitmap.PixelSize.Height-1));
        var p=e.GetCurrentPoint(this).Properties; var buttons=(p.IsLeftButtonPressed?1:0)|(p.IsMiddleButtonPressed?2:0)|(p.IsRightButtonPressed?4:0);
        session!.Connection.EnqueueMessage(new PointerEventMessage(new Position((int)lastPointer.X,(int)lastPointer.Y),(MouseButtons)(buttons|(wheel??0))),CancellationToken.None);
        if(wheel!=null)session.Connection.EnqueueMessage(new PointerEventMessage(new Position((int)lastPointer.X,(int)lastPointer.Y),(MouseButtons)buttons),CancellationToken.None);
        e.Handled=true;
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e) { base.OnPointerPressed(e); Focus(); if(CanSend) { e.Pointer.Capture(this); Pointer(e); } }
    protected override void OnPointerMoved(PointerEventArgs e) { base.OnPointerMoved(e); Pointer(e); }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { base.OnPointerReleased(e); Pointer(e); e.Pointer.Capture(null); }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) { base.OnPointerWheelChanged(e); Pointer(e,e.Delta.Y>0?8:e.Delta.Y<0?16:e.Delta.X>0?64:32); }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if(e.Key is Key.Left or Key.Right && e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Alt)) { ReleaseInputs(); EscapeFocus?.Invoke(); e.Handled=true; return; }
        if(!CanSend)return;
        // Compose AltGr characters locally and send their Unicode keysyms. Windows
        // can synthesize LeftCtrl before RightAlt; do not turn Polish text into Ctrl+A.
        if(e.Key==Key.RightAlt) { altGr=true; if(pressed.Remove(Key.LeftCtrl,out var control))KeyMessage(control,false); return; }
        var symbol=SpecialKey(e.Key);
        if(symbol==0 && !altGr && (e.KeyModifiers.HasFlag(KeyModifiers.Control)||e.KeyModifiers.HasFlag(KeyModifiers.Alt)))
        { if(e.Key is >=Key.A and <=Key.Z)symbol=(uint)('a'+(e.Key-Key.A)); else if(e.Key is >=Key.D0 and <=Key.D9)symbol=(uint)('0'+(e.Key-Key.D0)); }
        if(symbol==0)return;
        pressed[e.Key]=symbol; KeyMessage(symbol,true); e.Handled=true;
    }
    protected override void OnKeyUp(KeyEventArgs e) { if(e.Key==Key.RightAlt) { altGr=false; return; } if(pressed.Remove(e.Key,out var symbol)) { KeyMessage(symbol,false); e.Handled=true; } }
    protected override void OnTextInput(TextInputEventArgs e)
    { if(!CanSend || e.Text==null)return; foreach(var rune in e.Text.EnumerateRunes()) { var key=(uint)(rune.Value<=255?rune.Value:0x01000000|rune.Value); KeyMessage(key,true); KeyMessage(key,false); } e.Handled=true; }
    private static uint SpecialKey(Key key)=>key switch
    {
        Key.Back=>0xff08,Key.Tab=>0xff09,Key.Enter=>0xff0d,Key.Escape=>0xff1b,Key.Insert=>0xff63,Key.Delete=>0xffff,Key.Home=>0xff50,Key.End=>0xff57,Key.PageUp=>0xff55,Key.PageDown=>0xff56,Key.Left=>0xff51,Key.Up=>0xff52,Key.Right=>0xff53,Key.Down=>0xff54,
        Key.LeftShift=>0xffe1,Key.RightShift=>0xffe2,Key.LeftCtrl=>0xffe3,Key.RightCtrl=>0xffe4,Key.LeftAlt=>0xffe9,Key.RightAlt=>0xfe03,Key.LWin=>0xffeb,Key.RWin=>0xffec,Key.CapsLock=>0xffe5,Key.NumLock=>0xff7f,Key.Scroll=>0xff14,
        >=Key.F1 and <=Key.F24=>0xffbe+(uint)(key-Key.F1),_=>0
    };
    public void Dispose()=>Attach(null);
}
