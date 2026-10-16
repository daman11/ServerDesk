namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private readonly Task durationLoop;
    private Task monitorLoop=Task.CompletedTask;
    private Task transportShutdown=Task.CompletedTask;
    private readonly HashSet<Task> transferTasks=[];
    private readonly TaskCompletionSource closed=new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task CompleteCloseAsync()
    {
        Exception? failure=null;
        try
        {
            // A cancelled handshake or refresh may still own a client. Wait until
            // those operations have handed it back before releasing token sources.
            await connectionGate.WaitAsync(); connectionGate.Release();
            await appearanceGate.WaitAsync(); appearanceGate.Release();
            await refreshGate.WaitAsync(); refreshGate.Release();
            await Task.WhenAll(transferTasks.Concat([transportShutdown,durationLoop,historyWrite]));
        }
        catch(Exception ex) { failure=ex; }
        finally { connectionLifetime.Dispose(); lifetime.Dispose(); }
        if(failure==null)closed.TrySetResult(); else closed.TrySetException(failure);
    }
}
