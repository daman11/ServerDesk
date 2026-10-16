using System.Collections.Immutable;
using MarcusW.VncClient;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class VncSecurityTests
{
    [Theory][InlineData(8,false)][InlineData(8,true)][InlineData(16,false)][InlineData(16,true)][InlineData(32,false)][InlineData(32,true)]
    public void Standard_true_color_formats_work_in_both_byte_orders(int bits,bool bigEndian)
    {
        var header=Format(bits); header[2]=(byte)(bigEndian?1:0); var format=VncSecurity.ParsePixelFormat(header);
        Assert.Equal(bits,format.BitsPerPixel); Assert.Equal(bigEndian,format.BigEndian); Assert.Equal(bits/8,format.BytesPerPixel);
    }
    [Fact] public void Invalid_depth_channels_shifts_and_overlaps_are_rejected()
    {
        foreach(var change in new Action<byte[]>[]{h=>h[0]=0,h=>h[0]=24,h=>h[1]=33,h=>h[1]=8,h=>h[2]=2,h=>h[3]=0,h=>h[5]=0,h=>h[5]=6,h=>h[10]=32,h=>h[10]=8})
        { var header=Format(32); change(header); Assert.Throws<InvalidDataException>(()=>VncSecurity.ParsePixelFormat(header)); }
    }
    [Fact] public void Raw_rectangles_are_checked_before_reading_payload()
    {
        using var framebuffer=new VncFramebuffer(); using var stream=new CountingEofStream();
        using var frame=framebuffer.GrabFramebufferReference(new(2,2),ImmutableHashSet<Screen>.Empty);
        foreach(var rectangle in new[]{new Rectangle(0,0,0,1),new Rectangle(1,0,2,1),new Rectangle(0,1,1,2)})
            Assert.Throws<InvalidDataException>(()=>new VncSecurity.Raw().ReadFrameEncoding(stream,frame,rectangle,frame.Size,frame.Format));
        Assert.Equal(0,stream.Reads);
    }
    private static byte[] Format(int bits)=>bits switch
    {
        8=>[8,8,0,1,0,7,0,7,0,3,5,2,0,0,0,0],
        16=>[16,16,0,1,0,31,0,63,0,31,11,5,0,0,0,0],
        _=>[32,24,0,1,0,255,0,255,0,255,16,8,0,0,0,0]
    };
    [Theory][InlineData(16385u)][InlineData(2147483647u)][InlineData(uint.MaxValue)]
    public async Task Oversized_text_is_rejected_before_allocating_or_reading_payload(uint length)
    {
        using var stream=new CountingEofStream();
        await Assert.ThrowsAsync<InvalidDataException>(()=>VncSecurity.ReadTextAsync(stream,length,CancellationToken.None)); Assert.Equal(0,stream.Reads);
    }
    [Fact] public async Task Text_at_the_limit_is_read_completely()
    {
        using var stream=new MemoryStream(new byte[VncSecurity.MaximumTextBytes]);
        Assert.Equal(VncSecurity.MaximumTextBytes,(await VncSecurity.ReadTextAsync(stream,(uint)stream.Length,CancellationToken.None)).Length);
    }
    [Theory][InlineData(-1)][InlineData(int.MaxValue)][InlineData(VncSecurity.MaximumCompressedBytes+1)]
    public void Compressed_lengths_are_bounded_before_reading(int length)
    {
        using var inflater=new VncSecurity.Inflater(); using var stream=new CountingEofStream();
        Assert.Throws<InvalidDataException>(()=>inflater.ReadAndInflate(stream,length)); Assert.Equal(0,stream.Reads);
    }
    [Fact] public void Truncated_raw_frame_stops_on_first_eof_and_releases_framebuffer()
    {
        using var framebuffer=new VncFramebuffer(); using var stream=new CountingEofStream();
        using(var frame=framebuffer.GrabFramebufferReference(new(2,2),ImmutableHashSet<Screen>.Empty))
            Assert.Throws<EndOfStreamException>(()=>new VncSecurity.Raw().ReadFrameEncoding(stream,frame,new(0,0,2,2),frame.Size,frame.Format));
        Assert.Equal(1,stream.Reads); framebuffer.Read((_,pixels)=>Assert.Equal(16,pixels.Length));
    }
    [Theory][InlineData(3,null)][InlineData(7,null)][InlineData(8,null)][InlineData(3,"test1234")][InlineData(7,"test1234")][InlineData(8,"test1234")]
    public async Task Supported_protocol_versions_keep_explicit_authentication(int minor,string? password)
    {
        using var server=new VncTestServer(password,minor:minor);
        using var session=await VncSession.ConnectAsync(Profile(server,password),password??"").WaitAsync(TimeSpan.FromSeconds(5));
        await Until(()=>HasPixels(session)); Assert.Contains(0,server.Encodings); Assert.Contains(6,server.Encodings); Assert.DoesNotContain(7,server.Encodings); Assert.DoesNotContain(16,server.Encodings);
        session.Dispose(); await session.Shutdown.WaitAsync(TimeSpan.FromSeconds(5));
    }
    [Theory][InlineData(VncTestFault.DesktopNameLength)][InlineData(VncTestFault.FailureReasonLength)][InlineData(VncTestFault.PixelFormat)][InlineData(VncTestFault.DesktopSize)]
    public async Task Malformed_server_initialization_is_rejected(VncTestFault fault)
    {
        using var server=new VncTestServer(fault:fault);
        var error=await Assert.ThrowsAsync<InvalidOperationException>(()=>VncSession.ConnectAsync(Profile(server,null),"").WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }
    [Theory][InlineData(VncTestFault.TruncatedRaw)][InlineData(VncTestFault.CompressedLength)]
    public async Task Malformed_frame_disconnects_without_spinning_or_holding_framebuffer(VncTestFault fault)
    {
        using var server=new VncTestServer(fault:fault); using var session=await VncSession.ConnectAsync(Profile(server,null),"");
        var completion=await session.Completion.WaitAsync(TimeSpan.FromSeconds(5)); Assert.NotEqual(TerminalEndReason.Disposed,completion.Reason); Assert.False(session.IsConnected);
        session.Framebuffer.Read((_,_)=> { }); session.Dispose(); await session.Shutdown.WaitAsync(TimeSpan.FromSeconds(5));
    }
    [Fact] public async Task Bounded_zlib_decoder_still_renders_a_valid_frame()
    {
        using var server=new VncTestServer(encoding:6); using var session=await VncSession.ConnectAsync(Profile(server,null),"");
        await Until(()=>HasPixels(session)); session.Dispose(); await session.Shutdown.WaitAsync(TimeSpan.FromSeconds(5));
    }
    private static ServerProfile Profile(VncTestServer server,string? password)=>new() { Name="Security fixture",Protocol=Protocol.VNC,Host="127.0.0.1",Port=server.Port,Vnc=new() { Authentication=password==null?VncAuthentication.None:VncAuthentication.Password } };
    private static bool HasPixels(VncSession session) { var rendered=false; session.Framebuffer.Read((_,pixels)=>rendered=pixels[0]==40); return rendered; }
    private static async Task Until(Func<bool> ready) { using var ct=new CancellationTokenSource(TimeSpan.FromSeconds(5)); while(!ready())await Task.Delay(10,ct.Token); }
    private sealed class CountingEofStream : MemoryStream
    {
        public int Reads { get; private set; }
        public override int Read(Span<byte> buffer) { if(++Reads>3)throw new IOException("Reader repeated EOF"); return 0; }
    }
}
