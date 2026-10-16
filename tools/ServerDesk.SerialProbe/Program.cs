using System.Text;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

if(args.Length!=1)throw new ArgumentException("SerialProbe TEST_DEVICE");
foreach(var baud in new[]{9600,115200})
{
    var session=await SerialTerminalSession.OpenAsync(new() { PortName=args[0],BaudRate=baud });
    try
    {
        using(var cancel=new CancellationTokenSource(100))
        {
            try { await session.ReaderStream.ReadExactlyAsync(new byte[16],cancel.Token); throw new Exception("Idle serial read did not cancel"); }
            catch(OperationCanceledException) when(cancel.IsCancellationRequested) { }
        }
        var bytes=Encoding.UTF8.GetBytes("SERIAL-ROUNDTRIP żółć\r\n\u001b[A\u0003");
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await session.WriterStream.WriteAsync(bytes,timeout.Token); var response=new byte[bytes.Length]; await session.ReaderStream.ReadExactlyAsync(response,timeout.Token);
        if(!bytes.SequenceEqual(response))throw new Exception("Serial round trip changed raw UTF-8/control bytes");
        Console.WriteLine($"PASS Serial {baud} 8N1: cancelled idle read, UTF-8 and ANSI/Control bytes round trip");
    }
    finally { session.Dispose(); await session.Completion.WaitAsync(TimeSpan.FromSeconds(3)); }
    if((await session.Completion).Reason!=TerminalEndReason.Disposed)throw new Exception("Serial session did not report disposal");
}
Console.WriteLine("PASS port closed and reopened, read pump stopped after disposal");
