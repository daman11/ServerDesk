namespace ServerDesk.Core;

public enum FileSort { Name, Size, Modified, Type }

public static class FileListing
{
    public static IReadOnlyList<FileEntry> Apply(IEnumerable<FileEntry> entries,string? query,bool showHidden,FileSort sort=FileSort.Name,bool descending=false)
    {
        var needle=query?.Trim()??"";
        var visible=entries.Where(e=>e.Name==".." || (showHidden || !e.Name.StartsWith('.')) && e.Name.Contains(needle,StringComparison.OrdinalIgnoreCase));
        var groups=visible.OrderBy(e=>e.Name==".."?0:e.IsDirectory?1:2);
        IOrderedEnumerable<FileEntry> ordered=sort switch
        {
            FileSort.Size=>descending?groups.ThenByDescending(e=>e.IsDirectory?0:e.Size):groups.ThenBy(e=>e.IsDirectory?0:e.Size),
            FileSort.Modified=>descending?groups.ThenBy(e=>e.Modified==DateTime.MinValue).ThenByDescending(e=>e.Modified):groups.ThenBy(e=>e.Modified==DateTime.MinValue).ThenBy(e=>e.Modified),
            FileSort.Type=>descending?groups.ThenByDescending(TypeKey,StringComparer.OrdinalIgnoreCase):groups.ThenBy(TypeKey,StringComparer.OrdinalIgnoreCase),
            _=>descending?groups.ThenByDescending(e=>e.Name,NaturalStringComparer.OrdinalIgnoreCase):groups.ThenBy(e=>e.Name,NaturalStringComparer.OrdinalIgnoreCase)
        };
        return ordered.ThenBy(e=>e.Name,NaturalStringComparer.OrdinalIgnoreCase).ThenBy(e=>e.Name,StringComparer.Ordinal).ThenBy(e=>e.FullPath,StringComparer.Ordinal).ToArray();
    }
    private static string TypeKey(FileEntry entry)=>entry.IsSymbolicLink?"link":entry.IsDirectory?"directory":Path.GetExtension(entry.Name);
}
