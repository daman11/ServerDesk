using Porta.Pty;
using Renci.SshNet;
using System.Threading.Channels;
using ServerDesk.Core;
using Renci.SshNet.Common;

namespace ServerDesk.Infrastructure;

public sealed class SshTerminalSession : IPtyConnection
{
    private readonly SshClient client;
    private readonly ConnectionInfo connectionInfo;
    private readonly ShellStream shell;
    private readonly CancellationTokenSource lifetime = new();
    private readonly Channel<byte[]> output = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(128) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = true });
    private readonly Task pump;
    private readonly TerminalResizeQueue resize;
    private readonly TaskCompletionSource<TerminalCompletion> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Exception? transportError;
    private int exitCode = -1;
    private int disposed;
    public Task<TerminalCompletion> Completion => completion.Task;
    // SSH has no local process; EOF on ReaderStream reports session termination to the emulator.
    public event EventHandler<PtyExitedEventArgs>? ProcessExited { add { } remove { } }
    public Stream ReaderStream { get; }
    public Stream WriterStream => shell;
    public bool SupportsCancellableRead => true;
    public int Pid => 0;
    public int ExitCode => Volatile.Read(ref exitCode);
    public SshTerminalSession(SshClient client)
    {
        this.client = client;
        connectionInfo=client.ConnectionInfo;
        shell = client.CreateShellStream("xterm-256color", 120, 36, 0, 0, 65536);
        resize=new((cols,rows)=> { if(client.IsConnected)shell.ChangeWindowSize((uint)cols,(uint)rows,0,0); },OnResizeError);
        client.ErrorOccurred += OnTransportError;
        ReaderStream = new ChannelReadStream(output.Reader);
        pump = Task.Run(PumpAsync);
    }
    public static async Task<SshTerminalSession> CreateAsync(SshClient client,CancellationToken ct)
    {
        // Opening a channel and requesting a PTY are synchronous SSH.NET calls.
        // Cancellation disposes only this already-owned connection off the UI thread.
        using var cancellation=ct.Register(()=>_ = Task.Run(client.Dispose));
        try
        {
            return await Task.Run(()=>
            {
                ct.ThrowIfCancellationRequested(); var created=new SshTerminalSession(client);
                if(ct.IsCancellationRequested) { created.Dispose(); ct.ThrowIfCancellationRequested(); }
                return created;
            },ct).ConfigureAwait(false);
        }
        catch(Exception) when(ct.IsCancellationRequested) { throw new OperationCanceledException(ct); }
    }
    private async Task PumpAsync()
    {
        Exception? failure = null;
        try
        {
            var bytes = new byte[16384];
            while (!lifetime.IsCancellationRequested)
            {
                var count = await shell.ReadAsync(bytes.AsMemory(), lifetime.Token);
                if (count == 0) break;
                await output.Writer.WriteAsync(bytes[..count].ToArray(), lifetime.Token);
            }
        }
        catch (Exception ex) { failure = Volatile.Read(ref disposed) != 0 ? null : ex; }
        finally
        {
            failure = Volatile.Read(ref transportError) ?? failure;
            client.ErrorOccurred -= OnTransportError;
            resize.Stop();
            // A cleanup failure must not leave Completion pending forever. Always
            // attempt both resources and the resize worker before signalling EOF.
            try { shell.Dispose(); } catch(Exception ex) { failure??=ex; }
            try { client.Dispose(); } catch(Exception ex) { failure??=ex; }
            try { await resize.Completion.ConfigureAwait(false); } catch(Exception ex) { failure??=ex; }
            var result = Volatile.Read(ref disposed) != 0 ? new TerminalCompletion(TerminalEndReason.Disposed) : failure == null ? new(TerminalEndReason.RemoteClosed) : new(TerminalEndReason.TransportError, failure.GetType().Name);
            Volatile.Write(ref exitCode, result.Reason == TerminalEndReason.TransportError ? 1 : 0);
            output.Writer.TryComplete(Volatile.Read(ref disposed)!=0?null:failure);
            completion.TrySetResult(result);
        }
    }
    private void OnTransportError(object? sender, ExceptionEventArgs e)
    {
        Interlocked.CompareExchange(ref transportError, e.Exception, null);
        // Never wait for SSH reader-thread shutdown from a callback on that same thread.
        _ = Task.Run(() => { try { shell.Dispose(); } catch (ObjectDisposedException) { } });
    }
    private void OnResizeError(Exception error)
    {
        if(Volatile.Read(ref disposed)!=0 || lifetime.IsCancellationRequested)return;
        Interlocked.CompareExchange(ref transportError,error,null);
        _=Task.Run(()=> { try { shell.Dispose(); } catch(ObjectDisposedException) { } });
    }
    public bool WaitForExit(int milliseconds) => Completion.Wait(milliseconds);
    public void Resize(int cols, int rows) { if(Volatile.Read(ref disposed)==0 && !Completion.IsCompleted)resize.Request(cols,rows); }
    public void Kill() => Dispose();
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0) return;
        lifetime.Cancel(); resize.Stop();
        connectionInfo.ChannelCloseTimeout=TimeSpan.FromMilliseconds(500);
        try { shell.Dispose(); }
        finally
        {
            try { client.Dispose(); }
            finally { output.Writer.TryComplete(); _ = pump.ContinueWith(_ => lifetime.Dispose(), TaskScheduler.Default); }
        }
    }
    private sealed class ChannelReadStream(ChannelReader<byte[]> reader) : Stream
    {
        private byte[]? current; private int offset;
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (buffer.Length == 0) return 0;
            while (current == null || offset == current.Length)
            { if (!await reader.WaitToReadAsync(cancellationToken)) return 0; if (!reader.TryRead(out current)) continue; offset = 0; }
            var count = Math.Min(buffer.Length, current.Length - offset); current.AsMemory(offset, count).CopyTo(buffer); offset += count; return count;
        }
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken ct) => ReadAsync(buffer.AsMemory(offset,count),ct).AsTask();
        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer,offset,count,CancellationToken.None).GetAwaiter().GetResult();
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer,int offset,int count) => throw new NotSupportedException();
    }
}
