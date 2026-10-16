using System.IO.Ports;
using System.Threading.Channels;
using Porta.Pty;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed class SerialTerminalSession : IPtyConnection
{
    private readonly SerialPort port;
    private readonly CancellationTokenSource lifetime=new();
    private readonly Channel<byte[]> output=Channel.CreateBounded<byte[]>(new BoundedChannelOptions(128) { SingleReader=true,SingleWriter=true,FullMode=BoundedChannelFullMode.Wait });
    private readonly Task pump;
    private int disposed;
    public Task<TerminalCompletion> Completion { get; }
    public Stream ReaderStream { get; }
    public Stream WriterStream { get; }
    public bool SupportsCancellableRead=>true;
    public int Pid=>0;
    public int ExitCode=>Completion.IsCompletedSuccessfully && Completion.Result.Reason==TerminalEndReason.TransportError?1:0;
    public event EventHandler<PtyExitedEventArgs>? ProcessExited { add { } remove { } }
    private SerialTerminalSession(SerialPort port)
    {
        this.port=port; ReaderStream=new Input(output.Reader); WriterStream=new Output(port);
        var completion=new TaskCompletionSource<TerminalCompletion>(TaskCreationOptions.RunContinuationsAsynchronously); Completion=completion.Task;
        pump=Task.Run(async()=>
        {
            Exception? failure=null;
            try
            {
                var buffer=new byte[4096];
                while(!lifetime.IsCancellationRequested)
                {
                    int count; try { count=port.Read(buffer,0,buffer.Length); } catch(TimeoutException) { continue; }
                    if(count==0)break;
                    await output.Writer.WriteAsync(buffer[..count].ToArray(),lifetime.Token);
                }
            }
            catch(Exception ex) { if(Volatile.Read(ref disposed)==0)failure=ex; }
            finally
            {
                output.Writer.TryComplete(); port.Dispose();
                completion.TrySetResult(new(Volatile.Read(ref disposed)!=0?TerminalEndReason.Disposed:failure!=null?TerminalEndReason.TransportError:TerminalEndReason.RemoteClosed,failure?.GetType().Name));
            }
        });
    }
    public static async Task<SerialTerminalSession> OpenAsync(SerialOptions options,CancellationToken ct=default)
    {
        options.Validate();
        var port=new SerialPort(options.PortName,options.BaudRate,(Parity)options.Parity,options.DataBits,(StopBits)options.StopBits)
        { DtrEnable=options.DtrEnable,RtsEnable=options.RtsEnable,Handshake=(Handshake)options.Handshake,ReadTimeout=250,WriteTimeout=2000 };
        try { await Task.Run(()=> { ct.ThrowIfCancellationRequested(); port.Open(); },ct); ct.ThrowIfCancellationRequested(); return new(port); }
        catch(OperationCanceledException) { port.Dispose(); throw; }
        catch(Exception ex) { port.Dispose(); throw new InvalidOperationException(L.F($"Nie można otworzyć portu {options.PortName}. Sprawdź podłączenie urządzenia, uprawnienia i czy port jest wolny."),ex); }
    }
    public static string[] PortNames()=>SerialPort.GetPortNames().Order(StringComparer.OrdinalIgnoreCase).ToArray();
    public void Resize(int cols,int rows) { }
    public bool WaitForExit(int milliseconds)=>Completion.Wait(milliseconds);
    public void Kill()=>Dispose();
    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        // The read pump owns closing the port. Concurrent SerialPort.Dispose calls can
        // deadlock the native Linux event loop; ReadTimeout bounds the idle shutdown.
        lifetime.Cancel(); output.Writer.TryComplete();
        _=pump.ContinueWith(_=>lifetime.Dispose(),TaskScheduler.Default);
    }
    private sealed class Input(ChannelReader<byte[]> source) : Stream
    {
        private byte[] current=[]; private int offset;
        public override bool CanRead=>true; public override bool CanWrite=>false; public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException(); public override long Position { get=>throw new NotSupportedException(); set=>throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default)
        {
            if(buffer.Length==0)return 0;
            while(offset==current.Length) { if(!await source.WaitToReadAsync(ct))return 0; if(!source.TryRead(out var next))continue; current=next; offset=0; }
            var count=Math.Min(buffer.Length,current.Length-offset); current.AsMemory(offset,count).CopyTo(buffer); offset+=count; return count;
        }
        public override Task<int> ReadAsync(byte[] bytes,int offset,int count,CancellationToken ct)=>ReadAsync(bytes.AsMemory(offset,count),ct).AsTask();
        public override int Read(byte[] bytes,int offset,int count)=>ReadAsync(bytes,offset,count,CancellationToken.None).GetAwaiter().GetResult();
        public override void Flush() { }
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException(); public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException(); public override void SetLength(long length)=>throw new NotSupportedException();
    }
    private sealed class Output(SerialPort port) : Stream
    {
        private readonly SemaphoreSlim gate=new(1);
        public override bool CanRead=>false; public override bool CanWrite=>true; public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException(); public override long Position { get=>throw new NotSupportedException(); set=>throw new NotSupportedException(); }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken ct=default)
        { await gate.WaitAsync(ct); try { var bytes=buffer.ToArray(); await Task.Run(()=>port.Write(bytes,0,bytes.Length),ct); } finally { gate.Release(); } }
        public override Task WriteAsync(byte[] buffer,int offset,int count,CancellationToken ct)=>WriteAsync(buffer.AsMemory(offset,count),ct).AsTask();
        public override void Write(byte[] buffer,int offset,int count) { gate.Wait(); try { port.Write(buffer,offset,count); } finally { gate.Release(); } }
        public override void Flush() { }
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException(); public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException(); public override void SetLength(long length)=>throw new NotSupportedException();
    }
}
