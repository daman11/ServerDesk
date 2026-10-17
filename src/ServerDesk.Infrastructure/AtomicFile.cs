using System.Diagnostics;

namespace ServerDesk.Infrastructure;

internal static class AtomicFile
{
    internal static async Task ReplaceAsync(string target,Func<Stream,CancellationToken,Task> write,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var temporary=target+"."+Guid.NewGuid().ToString("N")+".part";
        try
        {
            var options=new FileStreamOptions { Mode=FileMode.CreateNew,Access=FileAccess.Write,Share=FileShare.None,Options=FileOptions.Asynchronous };
            if(!OperatingSystem.IsWindows())options.UnixCreateMode=UnixFileMode.UserRead|UnixFileMode.UserWrite;
            await using(var stream=new FileStream(temporary,options))
            {
                await write(stream,ct).ConfigureAwait(false);
                await stream.FlushAsync(ct).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested(); stream.Flush(flushToDisk:true);
            }
            ct.ThrowIfCancellationRequested();
            File.Move(temporary,target,overwrite:true);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { Trace.TraceWarning("Temporary file cleanup failed: {0}",ex.GetType().Name); }
        }
    }
}
