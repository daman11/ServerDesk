using System.Threading.Channels;

namespace ServerDesk.Infrastructure;

// Layout never performs network I/O. Keep one pending grid while a slow server
// processes the previous request; a resize drag must not build an unbounded queue.
internal sealed class TerminalResizeQueue
{
    private readonly Channel<(int Columns,int Rows)> pending=Channel.CreateBounded<(int,int)>(new BoundedChannelOptions(1) { FullMode=BoundedChannelFullMode.DropOldest,SingleReader=true });
    private readonly Action<int,int> resize;
    private readonly Action<Exception> failed;
    private int stopped;
    internal Task Completion { get; }
    internal TerminalResizeQueue(Action<int,int> resize,Action<Exception> failed)
    { this.resize=resize; this.failed=failed; Completion=Task.Run(PumpAsync); }
    internal void Request(int columns,int rows)
    { if(columns>0 && rows>0 && Volatile.Read(ref stopped)==0)pending.Writer.TryWrite((columns,rows)); }
    internal void Stop() { Interlocked.Exchange(ref stopped,1); pending.Writer.TryComplete(); }
    private async Task PumpAsync()
    {
        try
        {
            await foreach(var value in pending.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                var latest=value; while(pending.Reader.TryRead(out var next))latest=next;
                if(Volatile.Read(ref stopped)!=0)break;
                resize(latest.Columns,latest.Rows);
            }
        }
        catch(Exception ex) { if(Interlocked.Exchange(ref stopped,1)==0)failed(ex); pending.Writer.TryComplete(); }
    }
}
