using System.Buffers.Binary;
using System.Collections.Immutable;
using System.Text;
using MarcusW.VncClient;
using MarcusW.VncClient.Protocol;
using MarcusW.VncClient.Protocol.Implementation;
using MarcusW.VncClient.Protocol.Implementation.EncodingTypes.Frame;
using MarcusW.VncClient.Protocol.Implementation.Services.Communication;
using MarcusW.VncClient.Protocol.SecurityTypes;
using MarcusW.VncClient.Protocol.Services;
using MarcusW.VncClient.Rendering;
using MarcusW.VncClient.Utils;

namespace ServerDesk.Infrastructure;

// Bound lengths before the alpha4 implementation can allocate from network input.
// Protocol negotiation and authentication still use the explicitly selected security type.
internal static class VncSecurity
{
    internal const int MaximumTextBytes=16*1024;
    internal const int MaximumCompressedBytes=68*1024*1024;
    internal static async Task<byte[]> ReadAsync(Stream stream,int count,CancellationToken ct)
    { var bytes=new byte[count]; await stream.ReadExactlyAsync(bytes,ct).ConfigureAwait(false); return bytes; }
    internal static async Task<string> ReadTextAsync(Stream stream,uint count,CancellationToken ct)
    {
        if(count>MaximumTextBytes)throw new InvalidDataException("VNC text exceeds the protocol limit.");
        return Encoding.UTF8.GetString(await ReadAsync(stream,(int)count,ct).ConfigureAwait(false));
    }
    internal sealed class Handshaker(RfbConnectionContext context) : IRfbHandshaker
    {
        public async Task<ITransport?> DoHandshakeAsync(CancellationToken ct=default)
        {
            var transport=context.Transport!; var stream=transport.Stream; var state=context.GetState<ProtocolState>();
            var banner=Encoding.ASCII.GetString(await ReadAsync(stream,12,ct).ConfigureAwait(false));
            if(!banner.StartsWith("RFB 003.",StringComparison.Ordinal) || banner[11]!='\n' || !int.TryParse(banner.AsSpan(8,3),out var minor) || minor<3)throw new InvalidDataException("Invalid VNC protocol version.");
            var version=minor>=8?RfbProtocolVersion.RFB_3_8:minor>=7?RfbProtocolVersion.RFB_3_7:RfbProtocolVersion.RFB_3_3;
            state.ProtocolVersion=version;
            await stream.WriteAsync(Encoding.ASCII.GetBytes(version.GetStringRepresentation()+"\n"),ct).ConfigureAwait(false);
            ISecurityType? security;
            if(version==RfbProtocolVersion.RFB_3_3)
            {
                var id=BinaryPrimitives.ReadUInt32BigEndian(await ReadAsync(stream,4,ct).ConfigureAwait(false));
                if(id==0)await Failure(stream,ct).ConfigureAwait(false);
                security=context.SupportedSecurityTypes!.FirstOrDefault(s=>s.Id==id);
            }
            else
            {
                var count=(await ReadAsync(stream,1,ct).ConfigureAwait(false))[0];
                if(count==0)await Failure(stream,ct).ConfigureAwait(false);
                var offered=await ReadAsync(stream,count,ct).ConfigureAwait(false);
                security=context.SupportedSecurityTypes!.Where(s=>offered.Contains(s.Id)).OrderByDescending(s=>s.Priority).FirstOrDefault();
                if(security!=null)await stream.WriteAsync(new[]{security.Id},ct).ConfigureAwait(false);
            }
            if(security==null)throw new InvalidDataException("The server does not offer the selected VNC authentication method.");
            state.UsedSecurityType=security;
            var authentication=await security.AuthenticateAsync(context.Connection.Parameters.AuthenticationHandler,ct).ConfigureAwait(false);
            if(authentication.TunnelTransport!=null)throw new InvalidDataException("Unsupported VNC tunnel transport.");
            if(authentication.ExpectSecurityResult && BinaryPrimitives.ReadUInt32BigEndian(await ReadAsync(stream,4,ct).ConfigureAwait(false))!=0)
            {
                if(version>=RfbProtocolVersion.RFB_3_8)await Failure(stream,ct).ConfigureAwait(false);
                throw new InvalidDataException("VNC authentication failed.");
            }
            return null;
        }
        private static async Task Failure(Stream stream,CancellationToken ct)
        {
            var count=BinaryPrimitives.ReadUInt32BigEndian(await ReadAsync(stream,4,ct).ConfigureAwait(false));
            _=await ReadTextAsync(stream,count,ct).ConfigureAwait(false);
            throw new InvalidDataException("The VNC server rejected the connection.");
        }
    }
    internal sealed class Initializer(RfbConnectionContext context) : IRfbInitializer
    {
        public async Task InitializeAsync(CancellationToken ct=default)
        {
            var stream=context.Transport!.Stream; var state=context.GetState<ProtocolState>();
            await stream.WriteAsync(new[]{(byte)(context.Connection.Parameters.AllowSharedConnection?1:0)},ct).ConfigureAwait(false);
            var header=await ReadAsync(stream,24,ct).ConfigureAwait(false);
            var size=new Size(BinaryPrimitives.ReadUInt16BigEndian(header),BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(2)));
            VncFramebuffer.ValidateSize(size);
            var format=ParsePixelFormat(header.AsSpan(4,16));
            var name=await ReadTextAsync(stream,BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(20)),ct).ConfigureAwait(false);
            state.RemoteFramebufferSize=size; state.RemoteFramebufferFormat=format;
            state.RemoteFramebufferLayout=ImmutableHashSet.Create(new Screen(1,new Rectangle(Position.Origin,size),0)); state.DesktopName=name;
            await state.UsedSecurityType!.ReadServerInitExtensionAsync(ct).ConfigureAwait(false);
        }
    }
    internal static PixelFormat ParsePixelFormat(ReadOnlySpan<byte> data)
    {
        var bits=data[0]; var depth=data[1]; var red=BinaryPrimitives.ReadUInt16BigEndian(data[4..]); var green=BinaryPrimitives.ReadUInt16BigEndian(data[6..]); var blue=BinaryPrimitives.ReadUInt16BigEndian(data[8..]);
        if(bits is not (8 or 16 or 32) || depth<1 || depth>bits || data[2]>1 || data[3]!=1)throw InvalidFormat();
        static int ChannelBits(ushort maximum) { if(maximum==0 || (maximum&(maximum+1))!=0)throw InvalidFormat(); return System.Numerics.BitOperations.Log2(maximum)+1; }
        var rb=ChannelBits(red); var gb=ChannelBits(green); var bb=ChannelBits(blue);
        if(rb+gb+bb>depth || rb+data[10]>bits || gb+data[11]>bits || bb+data[12]>bits)throw InvalidFormat();
        var rm=(uint)red<<data[10]; var gm=(uint)green<<data[11]; var bm=(uint)blue<<data[12];
        if(((rm&gm)|(gm&bm)|(bm&rm))!=0)throw InvalidFormat();
        return new PixelFormat("RFB",bits,depth,data[2]==1,true,false,red,green,blue,0,data[10],data[11],data[12],0);
    }
    private static InvalidDataException InvalidFormat()=>new("Invalid VNC pixel format.");
    internal sealed class Inflater : IZLibInflater
    {
        private readonly BuiltinZLibInflater inner=new();
        public Stream ReadAndInflate(Stream source,int sourceLength,int zlibStreamId=-1,CancellationToken cancellationToken=default)
        {
            if(sourceLength<0 || sourceLength>MaximumCompressedBytes)throw new InvalidDataException("VNC compressed frame exceeds the protocol limit.");
            return inner.ReadAndInflate(source,sourceLength,zlibStreamId,cancellationToken);
        }
        public void ResetZlibStream(int id)=>inner.ResetZlibStream(id);
        public void Dispose()=>inner.Dispose();
    }
    internal sealed class Raw : RawEncodingType
    {
        public override void ReadFrameEncoding(Stream stream,IFramebufferReference? framebuffer,in Rectangle rectangle,in Size size,in PixelFormat format)
        {
            VncFramebuffer.ValidateSize(size);
            if(rectangle.IsEmpty() || !rectangle.FitsInside(size))throw new InvalidDataException("VNC rectangle is outside the desktop.");
            // alpha4's Raw reader loops forever when Read returns zero mid-frame.
            base.ReadFrameEncoding(new RequiredDataStream(stream),framebuffer,rectangle,size,format);
        }
    }
    private sealed class RequiredDataStream(Stream source) : Stream
    {
        public override int Read(Span<byte> buffer) { var count=source.Read(buffer); if(count==0 && buffer.Length>0)throw new EndOfStreamException("Truncated VNC frame."); return count; }
        public override int Read(byte[] buffer,int offset,int count)=>Read(buffer.AsSpan(offset,count));
        public override bool CanRead=>true; public override bool CanSeek=>false; public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException(); public override long Position { get=>throw new NotSupportedException(); set=>throw new NotSupportedException(); }
        public override void Flush()=>throw new NotSupportedException(); public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException(); public override void SetLength(long length)=>throw new NotSupportedException(); public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}
