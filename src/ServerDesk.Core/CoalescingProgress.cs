namespace ServerDesk.Core;

// Bound scheduled UI work independently of a producer's report frequency. The
// owner disposes on its dispatcher before presenting a final success/error state.
public sealed class CoalescingProgress<T>(Action<Action> schedule,Action<T> consume) : IProgress<T>,IDisposable
{
    private readonly object gate=new();
    private T? latest;
    private bool pending,disposed;
    public void Report(T value)
    {
        lock(gate) { if(disposed)return; latest=value; if(pending)return; pending=true; }
        schedule(Drain);
    }
    private void Drain()
    {
        T value;
        lock(gate) { pending=false; if(disposed)return; value=latest!; latest=default; }
        consume(value);
    }
    public void Dispose() { lock(gate) { disposed=true; latest=default; } }
}
