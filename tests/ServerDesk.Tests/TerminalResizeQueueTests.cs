using System.Collections.Concurrent;
using ServerDesk.Infrastructure;
using Xunit;

namespace ServerDesk.Tests;

public sealed class TerminalResizeQueueTests
{
    [Fact] public async Task SlowNetworkDoesNotBlockRequestsAndLatestGridWins()
    {
        using var blocked=new ManualResetEventSlim(); var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var latest=new TaskCompletionSource<(int,int)>(TaskCreationOptions.RunContinuationsAsynchronously); var calls=new ConcurrentQueue<(int,int)>();
        var queue=new TerminalResizeQueue((cols,rows)=>
        {
            calls.Enqueue((cols,rows)); if(calls.Count==1) { started.SetResult(); blocked.Wait(TimeSpan.FromSeconds(8)); } else latest.TrySetResult((cols,rows));
        },ex=>latest.TrySetException(ex));
        try
        {
            queue.Request(80,24); await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var requests=Task.Run(()=> { for(var i=0;i<10000;i++)queue.Request(100+i,30); queue.Request(180,52); });
            await requests.WaitAsync(TimeSpan.FromSeconds(2)); Assert.Single(calls);
            blocked.Set(); Assert.Equal((180,52),await latest.Task.WaitAsync(TimeSpan.FromSeconds(5)));
            Assert.Equal(2,calls.Count);
        }
        finally { blocked.Set(); queue.Stop(); await queue.Completion.WaitAsync(TimeSpan.FromSeconds(5)); }
    }
    [Fact] public async Task StopDiscardsPendingWorkAndRejectsFurtherRequests()
    {
        using var blocked=new ManualResetEventSlim(); var started=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls=0;
        var queue=new TerminalResizeQueue((_,_)=> { Interlocked.Increment(ref calls); started.SetResult(); blocked.Wait(TimeSpan.FromSeconds(8)); },_=>{});
        try { queue.Request(80,24); await started.Task.WaitAsync(TimeSpan.FromSeconds(5)); queue.Request(90,30); queue.Stop(); queue.Request(120,40); }
        finally { queue.Stop(); blocked.Set(); await queue.Completion.WaitAsync(TimeSpan.FromSeconds(5)); }
        Assert.Equal(1,calls);
    }
    [Fact] public async Task InvalidGridsAreIgnoredAndFailureIsObservedOnce()
    {
        var calls=0; var failure=new IOException("fixture"); var reported=new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var queue=new TerminalResizeQueue((_,_)=> { calls++; throw failure; },ex=>reported.SetResult(ex));
        queue.Request(0,24); queue.Request(80,-1); queue.Request(80,24);
        Assert.Same(failure,await reported.Task.WaitAsync(TimeSpan.FromSeconds(5))); await queue.Completion; queue.Request(90,30);
        Assert.Equal(1,calls);
    }
}
