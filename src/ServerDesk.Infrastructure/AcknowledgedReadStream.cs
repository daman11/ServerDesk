namespace ServerDesk.Infrastructure;

// Bound SSH.NET's upload pipeline before it can fill the SSH send window and
// enter a synchronous wait that cannot observe the transfer token. Normal links
// still have several packets in flight; ACKs come from the SSH reader thread.
internal sealed class AcknowledgedReadStream(Stream source,int window=256*1024) : Stream
{
    private readonly object gate=new();
    private long read,acknowledged;
    private System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure;
    private TaskCompletionSource changed=new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal void Acknowledge(long bytes)
    {
        TaskCompletionSource signal;
        lock(gate) { if(bytes<=acknowledged)return; acknowledged=bytes; signal=changed; changed=new(TaskCreationOptions.RunContinuationsAsynchronously); }
        signal.TrySetResult();
    }
    internal void Fail(Exception error)
    {
        TaskCompletionSource signal;
        lock(gate) { failure??=System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error); signal=changed; changed=new(TaskCreationOptions.RunContinuationsAsynchronously); }
        signal.TrySetResult();
    }
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default)
    {
        if(buffer.IsEmpty)return 0;
        while(true)
        {
            ct.ThrowIfCancellationRequested(); Task waiting; int count;
            lock(gate) { failure?.Throw(); count=(int)Math.Min(buffer.Length,Math.Max(0,window-(read-acknowledged))); waiting=changed.Task; }
            if(count>0)
            { var actual=await source.ReadAsync(buffer[..count],ct).ConfigureAwait(false); lock(gate)read+=actual; return actual; }
            await waiting.WaitAsync(ct).ConfigureAwait(false);
        }
    }
    public override Task<int> ReadAsync(byte[] buffer,int offset,int count,CancellationToken ct)=>ReadAsync(buffer.AsMemory(offset,count),ct).AsTask();
    public override int Read(byte[] buffer,int offset,int count)=>ReadAsync(buffer,offset,count,default).GetAwaiter().GetResult();
    public override bool CanRead=>source.CanRead; public override bool CanSeek=>false; public override bool CanWrite=>false;
    public override long Length=>source.Length; public override long Position { get=>source.Position; set=>throw new NotSupportedException(); }
    public override void Flush() { }
    public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
    public override void SetLength(long value)=>throw new NotSupportedException();
    public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
}
