namespace ServerDesk.Core;

public enum FileSort { Name, Size, Modified, Type }

public static class FileListing
{
    public static IReadOnlyList<FileEntry> Apply(IEnumerable<FileEntry> entries,string? query,bool showHidden,FileSort sort=FileSort.Name,bool descending=false,CancellationToken ct=default)
    {
        ct.ThrowIfCancellationRequested();
        var needle=query?.Trim()??"";
        var visible=entries.Where(e=> { ct.ThrowIfCancellationRequested(); return e.Name==".." || (showHidden || !e.Name.StartsWith('.')) && e.Name.Contains(needle,StringComparison.OrdinalIgnoreCase); });
        // The group comparison precedes every comparison of the later sort keys.
        // Check cancellation there as well as during enumeration.
        var groups=visible.OrderBy(e=>e.Name==".."?0:e.IsDirectory?1:2,ct.CanBeCanceled?Comparer<int>.Create((a,b)=> { ct.ThrowIfCancellationRequested(); return a.CompareTo(b); }):Comparer<int>.Default);
        IOrderedEnumerable<FileEntry> ordered=sort switch
        {
            FileSort.Size=>descending?groups.ThenByDescending(e=>e.IsDirectory?0:e.Size):groups.ThenBy(e=>e.IsDirectory?0:e.Size),
            FileSort.Modified=>descending?groups.ThenBy(e=>e.Modified==DateTime.MinValue).ThenByDescending(e=>e.Modified):groups.ThenBy(e=>e.Modified==DateTime.MinValue).ThenBy(e=>e.Modified),
            FileSort.Type=>descending?groups.ThenByDescending(TypeKey,StringComparer.OrdinalIgnoreCase):groups.ThenBy(TypeKey,StringComparer.OrdinalIgnoreCase),
            _=>descending?groups.ThenByDescending(e=>e.Name,NaturalStringComparer.OrdinalIgnoreCase):groups.ThenBy(e=>e.Name,NaturalStringComparer.OrdinalIgnoreCase)
        };
        try
        {
            var result=ordered.ThenBy(e=>e.Name,NaturalStringComparer.OrdinalIgnoreCase).ThenBy(e=>e.Name,StringComparer.Ordinal).ThenBy(e=>e.FullPath,StringComparer.Ordinal).ToArray();
            ct.ThrowIfCancellationRequested(); return result;
        }
        catch(InvalidOperationException) when(ct.IsCancellationRequested)
        {
            // Array sorting can wrap a cancellation thrown by its comparer.
            throw new OperationCanceledException(ct);
        }
    }
    private static string TypeKey(FileEntry entry)=>entry.IsSymbolicLink?"link":entry.IsDirectory?"directory":Path.GetExtension(entry.Name);
}
