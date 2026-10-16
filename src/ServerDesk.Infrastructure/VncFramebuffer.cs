using System.Collections.Immutable;
using System.Runtime.InteropServices;
using MarcusW.VncClient;
using MarcusW.VncClient.Rendering;

namespace ServerDesk.Infrastructure;

public sealed class VncFramebuffer : IRenderTarget,IDisposable
{
    private readonly object sync=new();
    private byte[] pixels=[];
    private GCHandle pin;
    private Size size;
    private bool disposed;
    public event Action? Updated;
    internal static void ValidateSize(Size size)
    {
        if(size.Width is <1 or >8192 || size.Height is <1 or >8192 || (long)size.Width*size.Height>16777216)throw new InvalidDataException(L.T("Pulpit VNC przekracza limit 16 milionów pikseli / 8192 px na wymiar."));
    }
    public IFramebufferReference GrabFramebufferReference(Size requested,IImmutableSet<Screen> layout)
    {
        Monitor.Enter(sync);
        try
        {
            ObjectDisposedException.ThrowIf(disposed,this);
            ValidateSize(requested);
            if(size!=requested)
            { var next=new byte[checked(requested.Width*requested.Height*4)]; if(pin.IsAllocated)pin.Free(); pixels=next; pin=GCHandle.Alloc(pixels,GCHandleType.Pinned); size=requested; }
            return new Reference(this,pin.AddrOfPinnedObject(),size);
        }
        catch { Monitor.Exit(sync); throw; }
    }
    public void Read(Action<Size,byte[]> reader)
    {
        // A frame can remain locked while the peer is silent mid-packet. The UI
        // keeps its last bitmap and must remain able to disconnect that peer.
        if(!Monitor.TryEnter(sync))return;
        try { if(!disposed && pixels.Length>0)reader(size,pixels); }
        finally { Monitor.Exit(sync); }
    }
    public void Dispose() { lock(sync) { disposed=true; if(pin.IsAllocated)pin.Free(); pixels=[]; } }
    private sealed class Reference(VncFramebuffer owner,IntPtr address,Size size) : IFramebufferReference
    {
        private int disposed;
        public IntPtr Address=>address;
        public Size Size=>size;
        // Little-endian BGRX; Avalonia presents it as opaque BGRA.
        public PixelFormat Format=>new("BGRX32",32,24,false,true,false,255,255,255,0,16,8,0,0);
        public double HorizontalDpi=>96; public double VerticalDpi=>96;
        public void Dispose() { if(Interlocked.Exchange(ref disposed,1)!=0)return; Monitor.Exit(owner.sync); owner.Updated?.Invoke(); }
    }
}
