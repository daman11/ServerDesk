using System.Collections.Concurrent;
using ServerDesk.Core;
using Xunit;

namespace ServerDesk.Tests;
public sealed class CoalescingProgressTests
{
    [Fact] public void FloodSchedulesOneCallbackAndDeliversNewestValue()
    {
        var callbacks=new Queue<Action>(); var values=new List<int>();
        using var progress=new CoalescingProgress<int>(callbacks.Enqueue,values.Add);
        for(var i=0;i<100000;i++)progress.Report(i);
        Assert.Single(callbacks); callbacks.Dequeue()(); Assert.Equal([99999],values);
        progress.Report(100000); Assert.Single(callbacks); callbacks.Dequeue()(); Assert.Equal([99999,100000],values);
    }
    [Fact] public void DisposePreventsStaleCallbackOverwritingFinalState()
    {
        var callbacks=new Queue<Action>(); var value="Running";
        var progress=new CoalescingProgress<string>(callbacks.Enqueue,p=>value=p);
        progress.Report("50%"); progress.Dispose(); value="Failed"; progress.Report("100%");
        Assert.Single(callbacks); callbacks.Dequeue()(); Assert.Equal("Failed",value);
    }
    [Fact] public async Task ConcurrentProducersKeepOnePendingCallback()
    {
        var callbacks=new ConcurrentQueue<Action>(); var values=new List<int>();
        using var progress=new CoalescingProgress<int>(callbacks.Enqueue,values.Add);
        await Task.WhenAll(Enumerable.Range(0,8).Select(n=>Task.Run(()=> { for(var i=0;i<10000;i++)progress.Report(n*10000+i); })));
        progress.Report(123456); Assert.Single(callbacks); Assert.True(callbacks.TryDequeue(out var callback)); callback(); Assert.Equal([123456],values);
    }
}
