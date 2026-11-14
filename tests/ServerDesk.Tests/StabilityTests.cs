using System.Collections.Immutable;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class StabilityTests : IDisposable
{
    private readonly string root=Path.Combine(Path.GetTempPath(),"serverdesk-stability-"+Guid.NewGuid().ToString("N"));
    [Fact] public async Task Vnc_display_does_not_wait_for_an_incomplete_frame()
    {
        using var frame=new VncFramebuffer();
        using var ready=new ManualResetEventSlim(); using var release=new ManualResetEventSlim();
        // Dedicated workers isolate this contract from a busy CI thread pool.
        // The writer must still hold the incomplete frame when Read returns;
        // this tests nonblocking access rather than machine scheduling latency.
        var writer=Task.Factory.StartNew(()=> { using var reference=frame.GrabFramebufferReference(new(2,2),ImmutableHashSet<MarcusW.VncClient.Screen>.Empty); ready.Set(); release.Wait(TimeSpan.FromSeconds(20)); },CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
        Task read=Task.CompletedTask; var called=false;
        try
        {
            Assert.True(ready.Wait(TimeSpan.FromSeconds(5)));
            read=Task.Factory.StartNew(()=>frame.Read((_,_)=>called=true),CancellationToken.None,TaskCreationOptions.LongRunning,TaskScheduler.Default);
            await read.WaitAsync(TimeSpan.FromSeconds(5)); Assert.False(called); Assert.False(writer.IsCompleted);
        }
        finally { release.Set(); await writer.WaitAsync(TimeSpan.FromSeconds(5)); await read.WaitAsync(TimeSpan.FromSeconds(5)); }
        frame.Read((_,pixels)=> { called=true; Assert.Equal(16,pixels.Length); }); Assert.True(called);
    }
    [Fact] public async Task Malformed_vnc_frame_is_reported_as_failure_not_normal_remote_close()
    {
        using var server=new VncTestServer(fault:VncTestFault.CompressedLength);
        using var session=await VncSession.ConnectAsync(new() { Name="Fixture",Host="127.0.0.1",Port=server.Port,Protocol=Protocol.VNC,Vnc=new() { Authentication=VncAuthentication.None } },"");
        var result=await session.Completion.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.Equal(TerminalEndReason.TransportError,result.Reason); Assert.Equal(nameof(InvalidDataException),result.ErrorType);
        session.Dispose(); await session.Shutdown.WaitAsync(TimeSpan.FromSeconds(3));
    }
    [Fact] public async Task Cancelled_password_update_preserves_previous_encrypted_file()
    {
        if(!OperatingSystem.IsWindows())return;
        var paths=new AppPaths(root); var store=new CredentialStore(paths); var id=Guid.NewGuid(); await store.SaveAsync(id,"original-fixture");
        var path=Path.Combine(root,"credentials",id+".bin"); var original=await File.ReadAllBytesAsync(path);
        using var cancelled=new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>store.SaveAsync(id,"updated-fixture",cancelled.Token));
        Assert.Equal(original,await File.ReadAllBytesAsync(path)); Assert.Equal("original-fixture",await store.GetAsync(id));
    }
    [Fact] public async Task Silent_peer_mid_frame_can_be_disconnected_and_disposed_repeatedly()
    {
        for(var attempt=0;attempt<6;attempt++)
        {
            using var server=new VncTestServer(fault:VncTestFault.SilentRaw);
            using var session=await VncSession.ConnectAsync(new() { Name="Fixture",Host="127.0.0.1",Port=server.Port,Protocol=Protocol.VNC,Vnc=new() { Authentication=VncAuthentication.None } },"");
            await server.PartialFrameSent.Task.WaitAsync(TimeSpan.FromSeconds(3));
            session.Dispose(); await session.Shutdown.WaitAsync(TimeSpan.FromSeconds(3)); session.Dispose();
            Assert.Equal(TerminalEndReason.Disposed,(await session.Completion).Reason);
        }
    }
    public void Dispose()
    {
        if(Directory.Exists(root))Directory.Delete(root,true);
    }
}
