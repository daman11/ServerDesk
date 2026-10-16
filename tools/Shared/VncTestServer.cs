using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.IO.Compression;

// Loopback-only RFB 3.8 fixture. Never used by the production application.
public enum VncTestFault { None,DesktopNameLength,FailureReasonLength,PixelFormat,DesktopSize,TruncatedRaw,CompressedLength,SilentRaw }
internal sealed class VncTestServer : IDisposable
{
    private readonly TcpListener listener;
    private readonly CancellationTokenSource lifetime=new(TimeSpan.FromSeconds(45));
    private TcpClient? peer;
    private readonly string? password;
    private readonly int minor;
    private readonly VncTestFault fault;
    private readonly int encoding;
    internal int Port=>((IPEndPoint)listener.LocalEndpoint).Port;
    internal ConcurrentQueue<(uint Symbol,bool Down)> Keys { get; }=new();
    internal ConcurrentQueue<(int X,int Y,int Buttons)> Pointers { get; }=new();
    internal ConcurrentQueue<int> Encodings { get; }=new();
    internal Task Worker { get; }
    internal TaskCompletionSource PartialFrameSent { get; }=new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal const int Width=96,Height=64;
    internal VncTestServer(string? password=null,int port=0,int minor=8,VncTestFault fault=VncTestFault.None,int encoding=0) { this.password=password; this.minor=minor; this.fault=fault; this.encoding=encoding; listener=new(IPAddress.Loopback,port); listener.Start(); Worker=RunAsync(); }
    private async Task RunAsync()
    {
        try
        {
            peer=await listener.AcceptTcpClientAsync(lifetime.Token); using var stream=peer.GetStream();
            async Task<byte[]> Read(int count) { var data=new byte[count]; await stream.ReadExactlyAsync(data,lifetime.Token); return data; }
            async Task Send(byte[] bytes)=>await stream.WriteAsync(bytes,lifetime.Token);
            await Send(Encoding.ASCII.GetBytes($"RFB 003.{minor:000}\n")); await Read(12);
            if(fault==VncTestFault.FailureReasonLength) { await Send([0,127,255,255,255]); return; }
            var security=(byte)(password==null?1:2);
            if(minor==3)await Send([0,0,0,security]);
            else { await Send([1,security]); var selected=await Read(1); if(selected[0]!=security)throw new InvalidDataException("Unexpected security selection"); }
            if(password!=null)
            {
                var challenge=Enumerable.Range(0,16).Select(i=>(byte)(i*7+3)).ToArray(); await Send(challenge);
                var answer=await Read(16); var key=new byte[8]; Encoding.Latin1.GetBytes(password.AsSpan(0,Math.Min(8,password.Length)),key);
                for(var i=0;i<key.Length;i++) { var b=key[i]; b=(byte)((b>>4)|(b<<4)); b=(byte)(((b&0xcc)>>2)|((b&0x33)<<2)); key[i]=(byte)(((b&0xaa)>>1)|((b&0x55)<<1)); }
                using var des=DES.Create(); des.Key=key; var expected=des.EncryptEcb(challenge,PaddingMode.None);
                if(!answer.SequenceEqual(expected)) { await Send([0,0,0,1,0,0,0,12]); await Send(Encoding.ASCII.GetBytes("Wrong secret")); return; }
            }
            if(password!=null || minor>=8)await Send([0,0,0,0]); await Read(1);
            var init=new byte[24+9]; BinaryPrimitives.WriteUInt16BigEndian(init,(ushort)Width); BinaryPrimitives.WriteUInt16BigEndian(init.AsSpan(2),(ushort)Height);
            new byte[]{32,24,0,1,0,255,0,255,0,255,16,8,0,0,0,0}.CopyTo(init,4); BinaryPrimitives.WriteUInt32BigEndian(init.AsSpan(20),9); Encoding.ASCII.GetBytes("VNC local").CopyTo(init,24);
            if(fault==VncTestFault.DesktopNameLength)BinaryPrimitives.WriteUInt32BigEndian(init.AsSpan(20),int.MaxValue);
            if(fault==VncTestFault.PixelFormat)init[4]=0;
            if(fault==VncTestFault.DesktopSize)BinaryPrimitives.WriteUInt16BigEndian(init,ushort.MaxValue);
            await Send(init);
            if(fault is VncTestFault.DesktopNameLength or VncTestFault.PixelFormat or VncTestFault.DesktopSize)return;
            while(!lifetime.IsCancellationRequested)
            {
                var type=(await Read(1))[0];
                switch(type)
                {
                    case 0: await Read(19); break;
                    case 2:
                        var encodingHeader=await Read(3); var encodingBytes=await Read(BinaryPrimitives.ReadUInt16BigEndian(encodingHeader.AsSpan(1))*4);
                        for(var i=0;i<encodingBytes.Length;i+=4)Encodings.Enqueue(BinaryPrimitives.ReadInt32BigEndian(encodingBytes.AsSpan(i))); break;
                    case 3:
                        await Read(9); var frame=new byte[16+Width*Height*4]; frame[3]=1; BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(8),(ushort)Width); BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(10),(ushort)Height);
                        for(var y=0;y<Height;y++)for(var x=0;x<Width;x++) { var index=16+(y*Width+x)*4; frame[index]=(byte)(x<Width/2?40:200); frame[index+1]=(byte)(y<Height/2?190:60); frame[index+2]=90; }
                        if(fault==VncTestFault.TruncatedRaw) { await Send(frame[..22]); return; }
                        if(fault==VncTestFault.SilentRaw) { await Send(frame[..22]); PartialFrameSent.TrySetResult(); await Task.Delay(Timeout.Infinite,lifetime.Token); return; }
                        if(fault==VncTestFault.CompressedLength) { BinaryPrimitives.WriteInt32BigEndian(frame.AsSpan(12),6); BinaryPrimitives.WriteUInt32BigEndian(frame.AsSpan(16),int.MaxValue); await Send(frame[..20]); return; }
                        if(encoding==6)
                        {
                            using var compressed=new MemoryStream(); using(var zlib=new ZLibStream(compressed,CompressionLevel.Fastest,true))zlib.Write(frame.AsSpan(16));
                            var encoded=new byte[20+compressed.Length]; frame.AsSpan(0,16).CopyTo(encoded); BinaryPrimitives.WriteInt32BigEndian(encoded.AsSpan(12),6); BinaryPrimitives.WriteInt32BigEndian(encoded.AsSpan(16),(int)compressed.Length); compressed.ToArray().CopyTo(encoded,20); await Send(encoded);
                            // The library uses persistent zlib state. One completed frame is enough for this fixture.
                            await Task.Delay(Timeout.Infinite,lifetime.Token); return;
                        }
                        await Send(frame); await Task.Delay(30,lifetime.Token); break;
                    case 4: var key=await Read(7); Keys.Enqueue((BinaryPrimitives.ReadUInt32BigEndian(key.AsSpan(3)),key[0]!=0)); break;
                    case 5: var pointer=await Read(5); Pointers.Enqueue((BinaryPrimitives.ReadUInt16BigEndian(pointer.AsSpan(1)),BinaryPrimitives.ReadUInt16BigEndian(pointer.AsSpan(3)),pointer[0])); break;
                    default: throw new InvalidDataException("Unexpected client message: "+type);
                }
            }
        }
        catch(Exception ex) when(ex is OperationCanceledException or EndOfStreamException or IOException or SocketException or ObjectDisposedException) { }
    }
    internal void Drop()=>peer?.Dispose();
    public void Dispose() { lifetime.Cancel(); peer?.Dispose(); listener.Stop(); }
}
