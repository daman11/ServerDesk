namespace ServerDesk.Core;

public sealed record FileTransferItem(FileEntry Entry,string Target);

public static class FileSelection
{
    public const int ClipboardCharacters=1024*1024;
    private static StringComparer Comparer(bool local)=>local && OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;

    public static IReadOnlyList<FileEntry> Capture(IEnumerable<FileEntry> entries,bool local)
        =>entries.Where(e=>e.Name!="..").DistinctBy(e=>e.FullPath,Comparer(local)).ToArray();

    // Plain text only: keep remote POSIX paths verbatim even on Windows, and do
    // not resolve links or turn copied names into shell commands/file transfers.
    public static string ClipboardText(IEnumerable<FileEntry> entries,bool local,bool fullPaths)
    {
        var text=new System.Text.StringBuilder();
        foreach(var entry in Capture(entries,local))
        {
            var value=fullPaths?entry.FullPath:entry.Name;
            var separator=text.Length==0?"":Environment.NewLine;
            if((long)text.Length+separator.Length+value.Length>ClipboardCharacters)throw new InvalidOperationException(L.T("Zaznaczenie jest zbyt duże do skopiowania. Wybierz mniej elementów."));
            text.Append(separator).Append(value);
        }
        return text.ToString();
    }

    public static IReadOnlyList<FileEntry> Retain(IEnumerable<FileEntry> visible,IEnumerable<FileEntry> selected,bool local)
    {
        var current=new Dictionary<string,FileEntry>(Comparer(local));
        foreach(var entry in visible)current.TryAdd(entry.FullPath,entry);
        return selected.Select(e=>current.GetValueOrDefault(e.FullPath)).Where(e=>e!=null).Cast<FileEntry>().DistinctBy(e=>e.FullPath,Comparer(local)).ToArray();
    }

    public static IReadOnlyList<FileTransferItem> Upload(IEnumerable<FileEntry> entries,string directory)
        =>Plan(entries,true,directory,false,e=>RemotePath.Join(directory,e.Name));

    public static IReadOnlyList<FileTransferItem> Download(IEnumerable<FileEntry> entries,string directory)
        =>Plan(entries,false,directory,true,e=>
        {
            return LocalFileName.InDirectory(directory,e.Name);
        });

    private static IReadOnlyList<FileTransferItem> Plan(IEnumerable<FileEntry> entries,bool localSource,string directory,bool localTarget,Func<FileEntry,string> target)
    {
        if(string.IsNullOrWhiteSpace(directory))throw new ArgumentException(L.T("Podaj katalog docelowy."));
        var selection=Capture(entries,localSource);
        if(selection.Any(e=>e.IsSymbolicLink))throw new InvalidOperationException(L.T("Zaznaczenie zawiera dowiązanie. Dla jednego dowiązania użyj „Kopiuj zawartość dowiązania…” z menu Operacje; zwykły transfer go nie śledzi."));
        var destinations=new HashSet<string>(Comparer(localTarget));
        var plan=new List<FileTransferItem>(selection.Count);
        foreach(var entry in selection)
        {
            var destination=target(entry);
            if(!destinations.Add(destination))throw new InvalidOperationException(L.T("Wybrane elementy mają tę samą nazwę docelową. Prześlij je osobno lub zmień nazwy."));
            plan.Add(new(localSource?entry with { FullPath=Path.GetFullPath(entry.FullPath) }:entry,destination));
        }
        return plan;
    }
}
