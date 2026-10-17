namespace ServerDesk.Infrastructure;

public static class ExportFile
{
    public static Task WriteAsync(string path,Func<Stream,CancellationToken,Task> write,CancellationToken ct=default)
    {
        ArgumentNullException.ThrowIfNull(write); var target=Path.GetFullPath(path);
        return Task.Run(()=>AtomicFile.ReplaceAsync(target,write,ct),ct);
    }
}
