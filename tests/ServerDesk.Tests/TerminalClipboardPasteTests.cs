using System.Text;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class TerminalClipboardPasteTests
{
    [Fact]
    public async Task OrdinaryInputIsVerbatimAndTheSessionRetainsOwnership()
    {
        using var sink=new MemoryStream(); var writer=TerminalClipboardPaste.WrapWriter(sink);
        var source=Encoding.UTF8.GetBytes("raw\r\ninput\n\x1b[200~raw\r\n\x1b[201~");
        await writer.WriteAsync(source); writer.Dispose();
        Assert.Equal(source,sink.ToArray()); Assert.True(sink.CanWrite);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PasteNormalizesOnlyPayloadAndPreservesSelectionDeletion(bool bracketed)
    {
        using var sink=new MemoryStream(); using var writer=TerminalClipboardPaste.WrapWriter(sink);
        const string deletion="\x1b[D\x7f",text="printf '%s\\n' \\\r\n  'żółć 🙂' \\\n  'last'\r";
        var source=Encoding.UTF8.GetBytes(deletion+(bracketed?"\x1b[200~"+text+"\x1b[201~":text));
        var normalized=text.Replace("\r\n","\n").Replace('\r','\n');
        await TerminalClipboardPaste.RunAsync(async()=>await writer.WriteAsync(source),default);
        var expected=deletion+(bracketed?"\x1b[200~"+normalized+"\x1b[201~":normalized.Replace('\n','\r'));
        Assert.Equal(expected,Encoding.UTF8.GetString(sink.ToArray()));
        Assert.Equal(deletion+(bracketed?"\x1b[200~"+text+"\x1b[201~":text),Encoding.UTF8.GetString(source));
    }
    [Fact]
    public async Task PendingPasteDoesNotChangeConcurrentTyping()
    {
        using var sink=new MemoryStream(); using var writer=TerminalClipboardPaste.WrapWriter(sink);
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var paste=TerminalClipboardPaste.RunAsync(async()=> { ready.SetResult(); await release.Task; await writer.WriteAsync(Encoding.UTF8.GetBytes("paste\r\ntext")); },default);
        await ready.Task; await writer.WriteAsync(Encoding.UTF8.GetBytes("raw\r\ninput")); release.SetResult(); await paste;
        Assert.Equal("raw\r\ninputpaste\rtext",Encoding.UTF8.GetString(sink.ToArray()));
    }
    [Fact]
    public async Task FailedPasteRestoresTheOrdinaryInputContext()
    {
        using var sink=new MemoryStream(); using var writer=TerminalClipboardPaste.WrapWriter(sink);
        await Assert.ThrowsAsync<IOException>(()=>TerminalClipboardPaste.RunAsync(()=>Task.FromException(new IOException()),default));
        await writer.WriteAsync(Encoding.UTF8.GetBytes("raw\r\ninput"));
        Assert.Equal("raw\r\ninput",Encoding.UTF8.GetString(sink.ToArray()));
    }
    [Fact]
    public async Task ReconnectingBeforeClipboardReadCompletesDropsTheOldPaste()
    {
        using var sink=new MemoryStream(); using var writer=TerminalClipboardPaste.WrapWriter(sink); using var oldConnection=new CancellationTokenSource();
        var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var paste=TerminalClipboardPaste.RunAsync(async()=> { ready.SetResult(); await release.Task; await writer.WriteAsync(Encoding.UTF8.GetBytes("old clipboard")); },oldConnection.Token);
        await ready.Task; oldConnection.Cancel(); release.SetResult(); await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>paste);
        Assert.Empty(sink.ToArray());
    }
    [Fact]
    public async Task SynchronousWritesUseTheSamePasteRules()
    {
        using var sink=new MemoryStream(); using var writer=TerminalClipboardPaste.WrapWriter(sink);
        await TerminalClipboardPaste.RunAsync(()=> { var bytes=Encoding.UTF8.GetBytes("one\r\ntwo\n"); writer.Write(bytes,0,bytes.Length); return Task.CompletedTask; },default);
        Assert.Equal("one\rtwo\r",Encoding.UTF8.GetString(sink.ToArray()));
    }
}
