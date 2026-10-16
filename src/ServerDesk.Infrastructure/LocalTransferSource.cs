using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public static class LocalTransferSource
{
    public static FileEntry Read(string path,bool resolveLink=false)
    {
        var info=Info(Path.GetFullPath(path));
        if(resolveLink)
        {
            info=info.ResolveLinkTarget(true)??info;
            // Resolve directory aliases in the path as well as the selected leaf.
            var full=info.FullName; var root=Path.GetPathRoot(full)!; var current=root;
            foreach(var part in full[root.Length..].Split(new[]{Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar},StringSplitOptions.RemoveEmptyEntries))
            {
                var component=Info(Path.Combine(current,part));
                current=(component.LinkTarget!=null?component.ResolveLinkTarget(true)??throw Missing():component).FullName;
            }
            info=Info(current);
        }
        info.Refresh();
        var link=info.LinkTarget!=null;
        if(!info.Exists && !link)throw new FileNotFoundException(L.T("Plik lub katalog źródłowy jest niedostępny."));
        if(resolveLink && (link || !info.Exists))throw Missing();
        return new(info.Name,info.FullName,info is DirectoryInfo,link,info is FileInfo file && !link?file.Length:0,info.LastWriteTime,"");
    }
    private static FileSystemInfo Info(string path)=>Directory.Exists(path)?new DirectoryInfo(path):new FileInfo(path);
    private static IOException Missing()=>new(L.T("Cel dowiązania jest niedostępny lub tworzy pętlę. Sprawdź ścieżkę i uprawnienia."));
}
