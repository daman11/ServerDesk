namespace ServerDesk.Core;

public sealed record RdpFileSelection(IReadOnlyList<string> Paths,long Bytes,int Files)
{
    public const long MaxBytes=2L*1024*1024*1024-1;
    public static RdpFileSelection Inspect(IEnumerable<string> paths,CancellationToken ct=default)
    {
        var selected=paths.Select(Path.GetFullPath).Distinct(OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal).ToArray();
        if(selected.Length is 0 or >256)throw new ArgumentException(L.T("Wybierz od 1 do 256 plików lub folderów."));
        long bytes=0; var files=0; var entries=0; var visited=new HashSet<string>(OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal);
        var pending=new Stack<string>(selected);
        while(pending.TryPop(out var path))
        {
            ct.ThrowIfCancellationRequested(); if(!visited.Add(path))continue;
            if(++entries>10000)throw new ArgumentException(L.T("Wybór ma ponad 10 000 elementów. Podziel go na mniejsze części."));
            var attributes=File.GetAttributes(path);
            if((attributes&FileAttributes.ReparsePoint)!=0)throw new ArgumentException(L.T("Przekazywanie dowiązań i folderów połączonych jest niedostępne. Wybierz zwykłe pliki."));
            if((attributes&FileAttributes.Directory)!=0) { foreach(var item in Directory.EnumerateFileSystemEntries(path)) { ct.ThrowIfCancellationRequested(); pending.Push(item); if(pending.Count+entries>10000)throw new ArgumentException(L.T("Wybór ma ponad 10 000 elementów. Podziel go na mniejsze części.")); } }
            else { bytes=checked(bytes+new FileInfo(path).Length); files++; if(bytes>MaxBytes)throw new ArgumentException(L.T("Schowek RDP obsługuje wybory mniejsze niż 2 GiB. Wybierz mniejszy zestaw plików.")); }
        }
        return new(selected,bytes,files);
    }
}
