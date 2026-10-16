using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed class LocalDeletionFileSystem : IDeletionFileSystem
{
    public bool IsLocal=>true;
    private static string SafePath(string path)
    {
        path=FileDeletionService.Normalize(path,true);
        // Reject a junction/symlink in every ancestor; the selected link itself
        // may be removed, but its target is never enumerated.
        for(var parent=Directory.GetParent(path);parent!=null;parent=parent.Parent)
            if((File.GetAttributes(parent.FullName)&FileAttributes.ReparsePoint)!=0)
                throw new IOException(L.T("Katalog nadrzędny jest dowiązaniem. Otwórz rzeczywistą lokalizację i spróbuj ponownie."));
        return path;
    }
    private static FileEntry Read(string path)
    {
        path=SafePath(path); var attributes=File.GetAttributes(path);
        var directory=(attributes&FileAttributes.Directory)!=0; var link=(attributes&FileAttributes.ReparsePoint)!=0;
        FileSystemInfo info=directory?new DirectoryInfo(path):new FileInfo(path);
        return new(info.Name,path,directory,link,directory || link?0:((FileInfo)info).Length,info.LastWriteTime,"");
    }
    public Task<FileEntry> ReadAsync(string path,CancellationToken ct)=>Task.Run(()=> { ct.ThrowIfCancellationRequested(); return Read(path); },ct);
    public Task<IReadOnlyList<FileEntry>> ListAsync(string path,CancellationToken ct)=>Task.Run<IReadOnlyList<FileEntry>>(()=>
    {
        var directory=Read(path);
        if(!directory.IsDirectory || directory.IsSymbolicLink)throw new IOException(L.T("Zawartość zmieniła się od przygotowania podglądu. Odśwież listę i przygotuj usuwanie ponownie."));
        var entries=new List<FileEntry>();
        foreach(var item in new DirectoryInfo(directory.FullPath).EnumerateFileSystemInfos())
        { ct.ThrowIfCancellationRequested(); if(entries.Count>=FileDeletionService.MaximumEntries)throw new IOException(L.T("Katalog przekracza limit podglądu: 20 000 elementów lub 64 poziomy. Usuń mniejsze grupy.")); entries.Add(Read(item.FullName)); }
        return entries;
    },ct);
    public Task DeleteAsync(FileEntry expected,CancellationToken ct)=>Task.Run(()=>
    {
        ct.ThrowIfCancellationRequested(); var current=Read(expected.FullPath); FileDeletionService.RequireUnchanged(expected,current);
        ct.ThrowIfCancellationRequested();
        if(current.IsDirectory)Directory.Delete(current.FullPath,false); else File.Delete(current.FullPath);
    },ct);
}
