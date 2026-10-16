using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;
public sealed class AcknowledgedReadStreamTests
{
    [Fact] public async Task TransportFailureInterruptsWindowWaitWithoutCancellingTheUserRequest()
    {
        using var source=new MemoryStream(new byte[512]); using var stream=new AcknowledgedReadStream(source,128); var buffer=new byte[512];
        Assert.Equal(128,await stream.ReadAsync(buffer)); var pending=stream.ReadAsync(buffer).AsTask(); var error=new IOException("owned transport closed"); stream.Fail(error);
        Assert.Same(error,await Assert.ThrowsAsync<IOException>(()=>pending.WaitAsync(TimeSpan.FromSeconds(2))));
    }
    [Fact] public async Task FullWindowWaitsForAcknowledgementAndKeepsExactBytes()
    {
        var bytes=Enumerable.Range(0,1024).Select(i=>(byte)i).ToArray(); using var source=new MemoryStream(bytes); using var stream=new AcknowledgedReadStream(source,128);
        var output=new byte[1024]; Assert.Equal(128,await stream.ReadAsync(output));
        var waiting=stream.ReadAsync(output.AsMemory(128)).AsTask(); Assert.False(waiting.IsCompleted);
        stream.Acknowledge(64); Assert.Equal(64,await waiting.WaitAsync(TimeSpan.FromSeconds(2)));
        var offset=192;
        while(offset<output.Length) { stream.Acknowledge(offset); offset+=await stream.ReadAsync(output.AsMemory(offset)); }
        Assert.Equal(bytes,output); Assert.Equal(1024,stream.Position);
    }
    [Fact] public async Task CancelInterruptsFullWindowWithoutAnAcknowledgement()
    {
        using var source=new MemoryStream(new byte[1024]); using var stream=new AcknowledgedReadStream(source,128); var buffer=new byte[1024]; Assert.Equal(128,await stream.ReadAsync(buffer));
        using var stop=new CancellationTokenSource(); var pending=stream.ReadAsync(buffer,stop.Token).AsTask(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending.WaitAsync(TimeSpan.FromSeconds(2)));
    }
    [Fact] public async Task DuplicateOrOldAcknowledgementDoesNotAddCredit()
    {
        using var source=new MemoryStream(new byte[512]); using var stream=new AcknowledgedReadStream(source,128); var buffer=new byte[512];
        Assert.Equal(128,await stream.ReadAsync(buffer)); stream.Acknowledge(128); stream.Acknowledge(100); stream.Acknowledge(128); Assert.Equal(128,await stream.ReadAsync(buffer));
        using var stop=new CancellationTokenSource(); var pending=stream.ReadAsync(buffer,stop.Token).AsTask(); Assert.False(pending.IsCompleted); stop.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending);
    }
}
