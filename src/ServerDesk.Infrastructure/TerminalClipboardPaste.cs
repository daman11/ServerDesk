namespace ServerDesk.Infrastructure;

// The emulator owns selection replacement and paste framing. Normalize only
// writes belonging to an explicit clipboard operation, never ordinary input,
// protocol responses or command dispatch. AsyncLocal keeps concurrent input out
// of the paste scope while following the emulator's asynchronous clipboard read.
public static class TerminalClipboardPaste
{
    private static readonly AsyncLocal<CancellationToken?> pending=new();
    public static async Task RunAsync(Func<Task> paste,CancellationToken connection)
    {
        connection.ThrowIfCancellationRequested();
        var previous=pending.Value; pending.Value=connection;
        try { await paste(); }
        finally { pending.Value=previous; }
    }
    public static Stream WrapWriter(Stream writer)=>new PasteWriter(writer);
    private static (byte[] Bytes,int Count) Normalize(ReadOnlySpan<byte> input)
    {
        var start=input.IndexOf("\u001b[200~"u8); var end=input.LastIndexOf("\u001b[201~"u8);
        var bracketed=start>=0 && end>=start+6;
        var first=bracketed?start+6:0; var last=bracketed?end:input.Length;
        var result=new byte[input.Length]; var used=0;
        for(var i=0;i<input.Length;i++)
        {
            var value=input[i];
            if(i>=first && i<last && value is 13 or 10)
            {
                if(value==13 && i+1<last && input[i+1]==10)i++;
                value=bracketed?(byte)10:(byte)13;
            }
            result[used++]=value;
        }
        return (result,used);
    }
    private sealed class PasteWriter(Stream target):Stream
    {
        public override bool CanRead=>false; public override bool CanWrite=>target.CanWrite; public override bool CanSeek=>false;
        public override long Length=>throw new NotSupportedException(); public override long Position { get=>throw new NotSupportedException(); set=>throw new NotSupportedException(); }
        public override void Write(byte[] buffer,int offset,int count)
        {
            if(pending.Value is not {} connection) { target.Write(buffer,offset,count); return; }
            connection.ThrowIfCancellationRequested();
            var (normalized,used)=Normalize(buffer.AsSpan(offset,count));
            try { target.Write(normalized,0,used); } finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(normalized); }
        }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer,CancellationToken ct=default)
        {
            if(pending.Value is not {} connection) { await target.WriteAsync(buffer,ct).ConfigureAwait(false); return; }
            using var scope=CancellationTokenSource.CreateLinkedTokenSource(connection,ct); scope.Token.ThrowIfCancellationRequested();
            var (normalized,used)=Normalize(buffer.Span);
            try { await target.WriteAsync(normalized.AsMemory(0,used),scope.Token).ConfigureAwait(false); }
            finally { System.Security.Cryptography.CryptographicOperations.ZeroMemory(normalized); }
        }
        public override Task WriteAsync(byte[] buffer,int offset,int count,CancellationToken ct)=>WriteAsync(buffer.AsMemory(offset,count),ct).AsTask();
        public override void Flush()=>target.Flush();
        public override Task FlushAsync(CancellationToken ct)=>target.FlushAsync(ct);
        public override int Read(byte[] buffer,int offset,int count)=>throw new NotSupportedException(); public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException(); public override void SetLength(long value)=>throw new NotSupportedException();
        // The session owns the target stream; disposing this facade never closes it.
    }
}
