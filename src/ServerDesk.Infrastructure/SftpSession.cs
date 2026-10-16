using Renci.SshNet;
using ServerDesk.Core;
using System.Diagnostics;

namespace ServerDesk.Infrastructure;

public sealed partial class SftpSession(SftpClient client) : IDisposable,IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly ConnectionInfo connectionInfo=client.ConnectionInfo;
    private int disposed;
    public string WorkingDirectory => client.WorkingDirectory;
    private async Task<T> Run<T>(Func<T> action, CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try { return await Task.Run(() => { ct.ThrowIfCancellationRequested(); return action(); }, ct); }
        finally { gate.Release(); }
    }
    private async Task RunAsync(Func<Task> action,CancellationToken ct)
    { await gate.WaitAsync(ct); try { await Task.Run(action,ct); } finally { gate.Release(); } }
    private async Task<T> RunAsync<T>(Func<Task<T>> action,CancellationToken ct)
    { await gate.WaitAsync(ct); try { return await Task.Run(action,ct); } finally { gate.Release(); } }
    private Task RunTransferAsync(Func<Task> action,CancellationToken ct)=>RunAsync(async()=>
    {
        // Give normal cancellation/temporary-file cleanup a short grace period.
        // SSH.NET can otherwise wait for an unanswered packet even after its
        // transfer token is cancelled. Abort only this session if it stays stuck.
        using var finished=new CancellationTokenSource(); var completed=finished.Token; Task abort=Task.CompletedTask;
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
        try { await action().ConfigureAwait(false); }
        finally { registration.Dispose(); finished.Cancel(); await abort.ConfigureAwait(false); }
    },ct);
    private sealed class ProgressBridge<T>(Action<T> report) : IProgress<T> { public void Report(T value)=>report(value); }
    public Task<List<FileEntry>> ListAsync(string path,CancellationToken ct)=>RunAsync(async()=>
    {
        var entries=new List<FileEntry>();
        await foreach(var entry in client.ListDirectoryAsync(path,ct))
        {
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
        if(!overwrite && await client.ExistsAsync(remote,ct)) throw new IOException(L.T("Zdalny plik już istnieje — nadpisanie nie zostało potwierdzone."));
        var temporary=RemotePath.Join(RemotePath.Parent(remote),".serverdesk-"+Guid.NewGuid().ToString("N")+".part");
        try
        {
            using var input=new AcknowledgedReadStream(file);
            var report=new ProgressBridge<UploadFileProgressReport>(p=> { input.Acknowledge((long)p.TotalBytesUploaded); if(!ct.IsCancellationRequested)progress.Report(new((long)p.TotalBytesUploaded,length,p.TotalBytesUploaded/Math.Max(.01,clock.Elapsed.TotalSeconds))); });
            EventHandler<Renci.SshNet.Common.ExceptionEventArgs> failure=(_,e)=>input.Fail(e.Exception);
            client.ErrorOccurred+=failure;
            try { await client.UploadFileAsync(input, temporary, false, report, ct); }
            finally { client.ErrorOccurred-=failure; }
            ct.ThrowIfCancellationRequested();
            if(file.Position!=length || (await client.GetAttributesAsync(temporary,ct)).Size!=length)
                throw new IOException(L.T("Rozmiar wysłanego pliku nie zgadza się ze źródłem. Plik docelowy pozostał bez zmian."));
            // OpenSSH POSIX rename replaces atomically. Other servers may refuse the extension;
            // failure leaves the original file intact rather than deleting it first.
            await ValidateTransferTargetAsync(remote,false,ct);
            client.RenameFile(temporary,remote,overwrite);
        }
        finally
        {
            using var cleanup=new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { if(client.IsConnected && await client.ExistsAsync(temporary,cleanup.Token))await client.DeleteFileAsync(temporary,cleanup.Token); }
            catch(Exception ex) when(ex is Renci.SshNet.Common.SshException or IOException or ObjectDisposedException or OperationCanceledException)
            { Trace.TraceWarning("SFTP temporary upload cleanup failed: {0}",ex.GetType().Name); }
        }
    },ct);
    public Task DownloadAsync(string remote, string local, bool overwrite, IProgress<TransferProgress> progress, CancellationToken ct) => RunTransferAsync(async () =>
    {
        if(new FileInfo(local).LinkTarget!=null)throw InvalidTransferTarget();
        var length=(await client.GetAttributesAsync(remote,ct)).Size; var clock=Stopwatch.StartNew();
        // Download to a sibling file, publish only after a complete successful transfer.
        var temporary = local + ".serverdesk-" + Guid.NewGuid().ToString("N") + ".part";
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,FileShare.None,65536,true))
            {
                var input=await client.OpenAsync(remote,FileMode.Open,FileAccess.Read,ct);
                try
                {
                    var buffer=new byte[65536]; long received=0; int count;
                    while((count=await input.ReadAsync(buffer,ct))>0)
                    {
                        await file.WriteAsync(buffer.AsMemory(0,count),ct); received+=count;
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
            ct.ThrowIfCancellationRequested(); File.Move(temporary,local,overwrite);
        }
        finally
        {
            try { if(File.Exists(temporary))File.Delete(temporary); }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
            { Trace.TraceWarning("SFTP temporary download cleanup failed: {0}",ex.GetType().Name); }
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
