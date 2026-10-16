namespace ServerDesk.Core;

// Limits apply to one queued tree. Only active ancestors are tracked: two aliases
// may copy the same directory independently, but a recursive path may never loop.
public sealed class TransferTraversal(bool local)
{
    public const int MaximumEntries=20000;
    public const int MaximumDepth=64;
    private readonly HashSet<string> ancestors=new(local && OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
    private int entries;
    public void Visit(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(++entries>MaximumEntries)throw new IOException(L.T("Transfer przekracza limit 20 000 elementów lub 64 poziomów. Prześlij mniejsze grupy."));
    }
    public IDisposable EnterDirectory(string resolvedPath)
    {
        if(ancestors.Count>=MaximumDepth)throw new IOException(L.T("Transfer przekracza limit 20 000 elementów lub 64 poziomów. Prześlij mniejsze grupy."));
        var path=local?Path.TrimEndingDirectorySeparator(Path.GetFullPath(resolvedPath)):resolvedPath.TrimEnd('/');
        if(!ancestors.Add(path))throw new IOException(L.T("Wykryto pętlę katalogów. Transfer został zatrzymany."));
        return new Scope(()=>ancestors.Remove(path));
    }
    private sealed class Scope(Action leave):IDisposable
    {
        private Action? action=leave;
        public void Dispose()=>Interlocked.Exchange(ref action,null)?.Invoke();
    }
}
