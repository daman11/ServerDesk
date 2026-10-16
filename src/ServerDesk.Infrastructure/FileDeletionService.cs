using ServerDesk.Core;
using System.Diagnostics;

namespace ServerDesk.Infrastructure;

public interface IDeletionFileSystem
{
    bool IsLocal { get; }
    Task<FileEntry> ReadAsync(string path,CancellationToken ct);
    Task<IReadOnlyList<FileEntry>> ListAsync(string path,CancellationToken ct);
    Task DeleteAsync(FileEntry expected,CancellationToken ct);
}

public sealed record FileDeletionProgress(int Completed,int Total,string Path);
public sealed record FileDeletionResult(int Deleted,int Total,bool Cancelled,Exception? Error)
{
    public bool Succeeded=>!Cancelled && Error==null && Deleted==Total;
}

public sealed class FileDeletionPlan
{
    internal FileDeletionService Owner { get; }
    public IReadOnlyList<FileEntry> Roots { get; }
    public IReadOnlyList<FileEntry> Entries { get; }
    public int Files { get; }
    public int Directories { get; }
    public int Links { get; }
    internal FileDeletionPlan(FileDeletionService owner,IEnumerable<FileEntry> roots,IEnumerable<FileEntry> entries)
    {
        Owner=owner; Roots=Array.AsReadOnly(roots.ToArray()); Entries=Array.AsReadOnly(entries.ToArray());
        Links=Entries.Count(e=>e.IsSymbolicLink); Directories=Entries.Count(e=>e.IsDirectory && !e.IsSymbolicLink); Files=Entries.Count-Links-Directories;
    }
}

// One plan belongs to one filesystem/session. Preparation is read-only. Execution
// visits only that bounded snapshot, never a newly discovered descendant.
public sealed class FileDeletionService(IDeletionFileSystem fileSystem)
{
    public const int MaximumEntries=20000;
    public const int MaximumDepth=64;
    private readonly StringComparer comparer=fileSystem.IsLocal && OperatingSystem.IsWindows()?StringComparer.OrdinalIgnoreCase:StringComparer.Ordinal;
    public bool IsLocal=>fileSystem.IsLocal;

    public static string Normalize(string path,bool local,bool allowRoot=false)
    {
        if(string.IsNullOrWhiteSpace(path) || path.Contains('\0'))throw new ArgumentException(L.T("Niepoprawna ścieżka usuwania."));
        var separators=local && OperatingSystem.IsWindows()?new[]{'\\','/'}:new[]{'/'};
        if(path.Split(separators).Any(p=>p is "." or ".."))throw new ArgumentException(L.T("Niepoprawna ścieżka usuwania."));
        if(local)
        {
            if(!Path.IsPathFullyQualified(path))throw new ArgumentException(L.T("Niepoprawna ścieżka usuwania."));
            var full=Path.GetFullPath(path); var root=Path.GetPathRoot(full)!;
            if(!allowRoot && string.Equals(Path.TrimEndingDirectorySeparator(full),Path.TrimEndingDirectorySeparator(root),OperatingSystem.IsWindows()?StringComparison.OrdinalIgnoreCase:StringComparison.Ordinal))
                throw new ArgumentException(L.T("Nie można usuwać katalogu głównego ani wpisu nadrzędnego."));
            return Path.TrimEndingDirectorySeparator(full);
        }
        if(!path.StartsWith('/') || path.Contains("//"))throw new ArgumentException(L.T("Niepoprawna ścieżka usuwania."));
        var remote=path.Length==1?path:path.TrimEnd('/');
        if(!allowRoot && remote=="/")throw new ArgumentException(L.T("Nie można usuwać katalogu głównego ani wpisu nadrzędnego."));
        return remote;
    }
    internal static string Parent(string path,bool local)=>local?Path.GetDirectoryName(path)??Path.GetPathRoot(path)!:RemotePath.Parent(path);
    private string Child(string parent,string name)
    {
        if(fileSystem.IsLocal)
        {
            if(OperatingSystem.IsWindows())LocalFileName.Validate(name); else _=RemotePath.Join(parent,name);
            return Normalize(Path.Combine(parent,name),true);
        }
        return Normalize(RemotePath.Join(parent,name),false);
    }
    public static void RequireUnchanged(FileEntry expected,FileEntry current)
    {
        if(expected.IsDirectory!=current.IsDirectory || expected.IsSymbolicLink!=current.IsSymbolicLink ||
           !expected.IsDirectory && !expected.IsSymbolicLink && (expected.Size!=current.Size || expected.Modified!=current.Modified))
            throw new IOException(L.T("Zawartość zmieniła się od przygotowania podglądu. Odśwież listę i przygotuj usuwanie ponownie."));
    }
    public async Task<FileDeletionPlan> PrepareAsync(IEnumerable<FileEntry> selection,IProgress<FileDeletionProgress>? progress,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var roots=selection.Take(MaximumEntries+1).ToArray();
        if(roots.Length==0 || roots.Length>MaximumEntries)throw new ArgumentException(L.T("Wybierz elementy do usunięcia."));
        var paths=new HashSet<string>(comparer); string? parent=null;
        foreach(var entry in roots)
        {
            var path=Normalize(entry.FullPath,fileSystem.IsLocal);
            var directory=Parent(path,fileSystem.IsLocal);
            if(parent!=null && !comparer.Equals(parent,directory) || !comparer.Equals(Child(directory,entry.Name),path) || !paths.Add(path))
                throw new ArgumentException(L.T("Wybierz różne elementy z jednego katalogu."));
            parent=directory;
        }
        var entries=new List<FileEntry>(); var visited=new HashSet<string>(comparer); var previewClock=Stopwatch.StartNew();
        async Task Visit(FileEntry current,int depth)
        {
            ct.ThrowIfCancellationRequested();
            var path=Normalize(current.FullPath,fileSystem.IsLocal);
            if(depth>=MaximumDepth || !visited.Add(path) || visited.Count>MaximumEntries)
                throw new IOException(L.T("Katalog przekracza limit podglądu: 20 000 elementów lub 64 poziomy. Usuń mniejsze grupy."));
            if(visited.Count==1 || previewClock.ElapsedMilliseconds>=50) { progress?.Report(new(visited.Count,0,path)); previewClock.Restart(); }
            if(current.IsDirectory && !current.IsSymbolicLink)
            {
                foreach(var child in await fileSystem.ListAsync(path,ct).ConfigureAwait(false))
                {
                    if(!comparer.Equals(Child(path,child.Name),Normalize(child.FullPath,fileSystem.IsLocal)))
                        throw new IOException(L.T("Lista katalogu zawiera ścieżkę poza wybranym obszarem."));
                    await Visit(child,depth+1).ConfigureAwait(false);
                }
            }
            entries.Add(current with { FullPath=path });
        }
        var currentRoots=new List<FileEntry>();
        foreach(var root in roots)
        {
            var requested=Normalize(root.FullPath,fileSystem.IsLocal);
            var current=await fileSystem.ReadAsync(requested,ct).ConfigureAwait(false);
            if(!comparer.Equals(requested,Normalize(current.FullPath,fileSystem.IsLocal)) || !comparer.Equals(Child(Parent(requested,fileSystem.IsLocal),current.Name),requested))
                throw new IOException(L.T("Lista katalogu zawiera ścieżkę poza wybranym obszarem."));
            RequireUnchanged(root,current); currentRoots.Add(current); await Visit(current,0).ConfigureAwait(false);
        }
        return new(this,currentRoots,entries);
    }
    public async Task<FileDeletionResult> ExecuteAsync(FileDeletionPlan plan,IProgress<FileDeletionProgress>? progress,CancellationToken ct)
    {
        if(!ReferenceEquals(plan.Owner,this))throw new ArgumentException("The deletion plan belongs to another session.",nameof(plan));
        var deleted=0;
        try
        {
            // Fail before changing anything if a target's type or file metadata changed.
            foreach(var entry in plan.Entries) { ct.ThrowIfCancellationRequested(); RequireUnchanged(entry,await fileSystem.ReadAsync(entry.FullPath,ct).ConfigureAwait(false)); }
            var progressClock=Stopwatch.StartNew();
            foreach(var entry in plan.Entries)
            {
                ct.ThrowIfCancellationRequested(); await fileSystem.DeleteAsync(entry,ct).ConfigureAwait(false); deleted++;
                if(deleted==1 || deleted==plan.Entries.Count || progressClock.ElapsedMilliseconds>=50)
                { progress?.Report(new(deleted,plan.Entries.Count,entry.FullPath)); progressClock.Restart(); }
            }
            return new(deleted,plan.Entries.Count,false,null);
        }
        catch(Exception) when(ct.IsCancellationRequested) { return new(deleted,plan.Entries.Count,true,null); }
        catch(Exception ex) { return new(deleted,plan.Entries.Count,false,ex); }
    }
}
