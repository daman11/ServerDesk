using Renci.SshNet;
using ServerDesk.Core;
using System.Diagnostics;

namespace ServerDesk.Infrastructure;

public sealed partial class SftpSession(SftpClient client) : IDisposable,IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly ConnectionInfo connectionInfo=client.ConnectionInfo;
    private int disposed;
    private readonly List<TransferCleanupWarning> cleanupWarnings=[];
    private Action? transferActivity;
    private Action<Exception>? transferFailure;
    public IReadOnlyList<TransferCleanupWarning> CleanupWarnings=>cleanupWarnings.ToArray();
    public bool IsConnected
    {
        get { try { return Volatile.Read(ref disposed)==0 && client.IsConnected; } catch(ObjectDisposedException) { return false; } }
    }
    public string WorkingDirectory => client.WorkingDirectory;
    private Task<T> Run<T>(Func<T> action,CancellationToken ct)=>RunAsync(()=>Task.FromResult(action()),ct);
    private Task RunAsync(Func<Task> action,CancellationToken ct)=>RunAsync(async()=> { await action().ConfigureAwait(false); return true; },ct);
    private async Task<T> RunAsync<T>(Func<Task<T>> action,CancellationToken ct)
    { await gate.WaitAsync(ct); try { return await Task.Run(()=>RunOwnedOperationAsync(action,ct),ct); } finally { gate.Release(); } }
    private Task RunTransferAsync(Func<Task> action,CancellationToken ct)=>RunAsync(action,ct);
    private async Task<T> RunOwnedOperationAsync<T>(Func<Task<T>> action,CancellationToken ct)
    {
        // Give normal cancellation/temporary-file cleanup a short grace period.
        // SSH.NET can otherwise wait for an unanswered packet even after its
        // transfer token is cancelled. Abort only this session if it stays stuck.
        using var finished=new CancellationTokenSource(); var completed=finished.Token; Task abort=Task.CompletedTask;
        var idleBudget=client.OperationTimeout;
        if(idleBudget<=TimeSpan.Zero || idleBudget==Timeout.InfiniteTimeSpan)idleBudget=TimeSpan.FromSeconds(30);
        using var idle=new CancellationTokenSource(idleBudget);
        transferActivity=()=> { try { idle.CancelAfter(idleBudget); } catch(ObjectDisposedException) { } };
        var timedOut=false;
        async Task WatchIdleAsync()
        {
            using var watching=CancellationTokenSource.CreateLinkedTokenSource(completed,idle.Token);
            try { await Task.Delay(Timeout.InfiniteTimeSpan,watching.Token).ConfigureAwait(false); }
            catch(OperationCanceledException) { }
            if(completed.IsCancellationRequested || ct.IsCancellationRequested)return;
            Volatile.Write(ref timedOut,true);
            Volatile.Read(ref transferFailure)?.Invoke(new Renci.SshNet.Common.SshOperationTimeoutException("SFTP transfer idle timeout."));
            // Own transfer connection only. Disposing it releases outstanding
            // packet waits, including a silent peer that never acknowledges.
            try { Dispose(); } catch(Exception ex) { Trace.TraceWarning("SFTP idle-timeout cleanup: {0}",ex.GetType().Name); }
        }
        var watchdog=WatchIdleAsync();
        using var registration=ct.Register(()=>abort=AbortStalledAsync());
        async Task AbortStalledAsync()
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(1),completed).ConfigureAwait(false);
                if(completed.IsCancellationRequested)return;
                Trace.WriteLine("SFTP cancellation: aborting stalled client");
                client.ConnectionInfo.ChannelCloseTimeout=TimeSpan.FromMilliseconds(500);
                client.OperationTimeout=TimeSpan.FromSeconds(1); client.Dispose();
                Trace.WriteLine("SFTP cancellation: stalled client disposed");
            }
            catch(OperationCanceledException) { }
            catch(ObjectDisposedException) { }
        }
        try { ct.ThrowIfCancellationRequested(); var result=await action().ConfigureAwait(false); if(Volatile.Read(ref timedOut))throw new Renci.SshNet.Common.SshOperationTimeoutException("SFTP operation idle timeout."); return result; }
        catch(Exception) when(Volatile.Read(ref timedOut) && !ct.IsCancellationRequested) { throw new Renci.SshNet.Common.SshOperationTimeoutException("SFTP transfer idle timeout."); }
        finally { transferActivity=null; transferFailure=null; registration.Dispose(); finished.Cancel(); await Task.WhenAll(abort,watchdog).ConfigureAwait(false); }
    }
    private sealed class ProgressBridge<T>(Action<T> report) : IProgress<T> { public void Report(T value)=>report(value); }
    public Task<List<FileEntry>> ListAsync(string path,CancellationToken ct)=>RunAsync(async()=>
    {
        var entries=new List<FileEntry>();
        await foreach(var entry in client.ListDirectoryAsync(path,ct))
        {
            transferActivity?.Invoke();
            ct.ThrowIfCancellationRequested();
            if(entry.Name is not ("." or ".."))entries.Add(new(entry.Name,entry.FullName,entry.IsDirectory,entry.IsSymbolicLink,entry.Length,entry.LastWriteTime,Permissions(entry.Attributes)));
        }
        return entries;
    },ct);
    private static string Permissions(Renci.SshNet.Sftp.SftpFileAttributes a) => $"{(a.OwnerCanRead?4:0)+(a.OwnerCanWrite?2:0)+(a.OwnerCanExecute?1:0)}{(a.GroupCanRead?4:0)+(a.GroupCanWrite?2:0)+(a.GroupCanExecute?1:0)}{(a.OthersCanRead?4:0)+(a.OthersCanWrite?2:0)+(a.OthersCanExecute?1:0)}";
    public Task CreateDirectoryAsync(string path, CancellationToken ct) => Run(() => { client.CreateDirectory(path); return true; },ct);
    public Task RenameAsync(string path, string target, CancellationToken ct) => RunAsync(async () =>
    {
        var file=await ReadDeletionFileAsync(path,ct);
        target=FileDeletionService.Normalize(target,false);
        var parent=await client.GetAsync(RemotePath.Parent(target),ct);
        if(parent.FullName!=RemotePath.Parent(target) || !parent.IsDirectory || parent.IsSymbolicLink)throw Changed();
        if(client.Exists(target))throw new IOException(L.T("Cel już istnieje."));
        ct.ThrowIfCancellationRequested(); file.MoveTo(target);
    },ct);
    public Task DeleteAsync(FileEntry entry, CancellationToken ct) => RunAsync(async () =>
    {
        var file=await ReadDeletionFileAsync(entry.FullPath,ct);
        if(file.IsDirectory!=entry.IsDirectory || file.IsSymbolicLink!=entry.IsSymbolicLink)throw Changed();
        ct.ThrowIfCancellationRequested(); await file.DeleteAsync(ct);
    },ct);
    public Task ChmodAsync(string path, short mode, CancellationToken ct) => RunAsync(async () =>
    {
        var file=await ReadDeletionFileAsync(path,ct);
        if(file.IsSymbolicLink)throw new IOException(L.T("Zmień uprawnienia w rzeczywistej lokalizacji, nie na dowiązaniu."));
        ct.ThrowIfCancellationRequested(); file.SetPermissions(mode);
    },ct);
    public Task<bool> ExistsAsync(string path,CancellationToken ct)=>RunAsync(()=>client.ExistsAsync(path,ct),ct);
    public Task UploadAsync(string local, string remote, bool overwrite, IProgress<TransferProgress> progress, CancellationToken ct) => RunTransferAsync(async () =>
    {
        if(new FileInfo(local).LinkTarget!=null)throw new IOException(L.T("Źródło transferu zmieniło typ. Odśwież listę i wybierz je ponownie."));
        await ValidateTransferTargetAsync(remote,false,ct);
        using var file = File.OpenRead(local); var clock = Stopwatch.StartNew(); var length=file.Length;
        var source=new FileInfo(local); source.Refresh(); var sourceWriteTime=source.LastWriteTimeUtc;
        if(!overwrite && await client.ExistsAsync(remote,ct)) throw new IOException(L.T("Zdalny plik już istnieje — nadpisanie nie zostało potwierdzone."));
        var temporary=RemotePath.Join(RemotePath.Parent(remote),".serverdesk-"+Guid.NewGuid().ToString("N")+".part");
        var attempted=false; var published=false;
        try
        {
            using var input=new AcknowledgedReadStream(file);
            transferFailure=input.Fail;
            var report=new ProgressBridge<UploadFileProgressReport>(p=> { transferActivity?.Invoke(); input.Acknowledge((long)p.TotalBytesUploaded); if(!ct.IsCancellationRequested)progress.Report(new((long)p.TotalBytesUploaded,length,p.TotalBytesUploaded/Math.Max(.01,clock.Elapsed.TotalSeconds))); });
            EventHandler<Renci.SshNet.Common.ExceptionEventArgs> failure=(_,e)=>input.Fail(e.Exception);
            client.ErrorOccurred+=failure;
            try { attempted=true; await client.UploadFileAsync(input, temporary, false, report, ct); }
            finally { transferFailure=null; client.ErrorOccurred-=failure; }
            ct.ThrowIfCancellationRequested();
            source.Refresh();
            if(!source.Exists || source.LinkTarget!=null || source.Length!=length || file.Length!=length || source.LastWriteTimeUtc!=sourceWriteTime)
                throw new IOException(L.T("Źródło wysyłanego pliku zmieniło się podczas transferu. Plik docelowy pozostał bez zmian. Poczekaj na zakończenie zapisu źródła i ponów zadanie."));
            if(file.Position!=length || (await client.GetAttributesAsync(temporary,ct)).Size!=length)
                throw new IOException(L.T("Rozmiar wysłanego pliku nie zgadza się ze źródłem. Plik docelowy pozostał bez zmian."));
            // OpenSSH POSIX rename replaces atomically. Other servers may refuse the extension;
            // failure leaves the original file intact rather than deleting it first.
            await ValidateTransferTargetAsync(remote,false,ct);
            client.RenameFile(temporary,remote,overwrite);
            published=true;
        }
        finally
        {
            if(attempted && !published)
            {
                using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try
                {
                    if(IsConnected)
                    {
                        try
                        {
                            var partial=await ReadDeletionFileAsync(temporary,cleanup.Token,new Renci.SshNet.Common.SftpPathNotFoundException());
                            if(partial.IsDirectory)throw InvalidTransferTarget();
                            // Delete the exact leaf entry, including a replaced
                            // symlink; SftpClient.DeleteFileAsync resolves its target.
                            await partial.DeleteAsync(cleanup.Token);
                        }
                        catch(Renci.SshNet.Common.SftpPathNotFoundException) { /* Already absent. */ }
                    }
                    else cleanupWarnings.Add(new(temporary,true));
                }
                catch(Exception ex) when(ex is Renci.SshNet.Common.SshException or IOException or ObjectDisposedException or OperationCanceledException)
                { cleanupWarnings.Add(new(temporary,true)); Trace.TraceWarning("SFTP temporary upload cleanup failed: {0}",ex.GetType().Name); }
            }
        }
    },ct);
    public Task DownloadAsync(string remote, string local, bool overwrite, IProgress<TransferProgress> progress, CancellationToken ct) => RunTransferAsync(async () =>
    {
        if(new FileInfo(local).LinkTarget!=null)throw InvalidTransferTarget();
        var source=await client.GetAttributesAsync(remote,ct); var length=source.Size; var sourceWriteTime=source.LastWriteTimeUtc; var clock=Stopwatch.StartNew();
        // Download to a sibling file, publish only after a complete successful transfer.
        var temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(local))!,".serverdesk-"+Guid.NewGuid().ToString("N")+".part");
        var created=false; var published=false;
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,FileShare.None,65536,true))
            {
                created=true;
                var input=await client.OpenAsync(remote,FileMode.Open,FileAccess.Read,ct);
                try
                {
                    var buffer=new byte[65536]; long received=0; int count;
                    while((count=await input.ReadAsync(buffer,ct))>0)
                    {
                        transferActivity?.Invoke(); await file.WriteAsync(buffer.AsMemory(0,count),ct); received+=count;
                        if(!ct.IsCancellationRequested)progress.Report(new(received,length,received/Math.Max(.01,clock.Elapsed.TotalSeconds)));
                    }
                }
                finally
                {
                    // The library's async stream disposal closes with a non-cancellable
                    // request. Its synchronous close honors OperationTimeout instead.
                    var timeout=client.OperationTimeout;
                    if(ct.IsCancellationRequested)client.OperationTimeout=TimeSpan.FromSeconds(1);
                    try { input.Dispose(); }
                    finally { try { client.OperationTimeout=timeout; } catch(ObjectDisposedException) { } }
                }
                ct.ThrowIfCancellationRequested();
                if(file.Length!=length)throw new IOException(L.T("Rozmiar pobranego pliku zmienił się podczas transferu. Plik docelowy pozostał bez zmian."));
            }
            var current=await client.GetAttributesAsync(remote,ct);
            if(current.Size!=length || current.LastWriteTimeUtc!=sourceWriteTime || current.IsDirectory!=source.IsDirectory || current.IsSymbolicLink!=source.IsSymbolicLink)
                throw new IOException(L.T("Źródło pobieranego pliku zmieniło się podczas transferu. Plik docelowy pozostał bez zmian. Poczekaj na zakończenie zapisu źródła i ponów zadanie."));
            ct.ThrowIfCancellationRequested(); File.Move(temporary,local,overwrite); published=true;
        }
        finally
        {
            if(created && !published)
            {
                try { File.Delete(temporary); }
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
                { cleanupWarnings.Add(new(temporary,false)); Trace.TraceWarning("SFTP temporary download cleanup failed: {0}",ex.GetType().Name); }
            }
        }
    },ct);
    public void Dispose()
    {
        if(Interlocked.Exchange(ref disposed,1)!=0)return;
        connectionInfo.ChannelCloseTimeout=TimeSpan.FromMilliseconds(500);
        try { client.OperationTimeout=TimeSpan.FromSeconds(1); } catch(ObjectDisposedException) { }
        client.Dispose();
    }
    public ValueTask DisposeAsync()=>new(Task.Run(Dispose));
}
