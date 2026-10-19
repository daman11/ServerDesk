using Renci.SshNet.Sftp;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed partial class SftpSession
{
    public FileDeletionService CreateDeletionService()=>new(new DeletionFileSystem(this));
    private sealed class DeletionFileSystem(SftpSession owner) : IDeletionFileSystem
    {
        public bool IsLocal=>false;
        public Task<FileEntry> ReadAsync(string path,CancellationToken ct)=>owner.RunAsync(async()=>Entry(await owner.ReadDeletionFileAsync(path,ct)),ct);
        public Task<IReadOnlyList<FileEntry>> ListAsync(string path,CancellationToken ct)=>owner.RunAsync<IReadOnlyList<FileEntry>>(async()=>
        {
            var directory=await owner.ReadDeletionFileAsync(path,ct);
            if(!directory.IsDirectory || directory.IsSymbolicLink)throw Changed();
            var entries=new List<FileEntry>();
            await foreach(var item in owner.DeletionListAsync(path,ct))
            { if(entries.Count>=FileDeletionService.MaximumEntries)throw new IOException(L.T("Katalog przekracza limit podglądu: 20 000 elementów lub 64 poziomy. Usuń mniejsze grupy.")); entries.Add(Entry(Supported(item))); }
            return entries;
        },ct);
        public Task DeleteAsync(FileEntry expected,CancellationToken ct)=>owner.RunAsync(async()=>
        {
            var file=await owner.ReadDeletionFileAsync(expected.FullPath,ct); FileDeletionService.RequireUnchanged(expected,Entry(file)); ct.ThrowIfCancellationRequested();
            // ISftpFile.DeleteAsync sends the exact listed path. SftpClient.DeleteFile
            // canonicalizes the leaf and can resolve a symlink to its target.
            await file.DeleteAsync(ct);
        },ct);
    }
    private static IOException Changed()=>new(L.T("Zawartość zmieniła się od przygotowania podglądu. Odśwież listę i przygotuj usuwanie ponownie."));
    private static FileEntry Entry(ISftpFile file)=>new(file.Name,file.FullName,file.IsDirectory,file.IsSymbolicLink,file.IsSymbolicLink?0:file.Length,file.LastWriteTime,Permissions(file.Attributes));
    private async IAsyncEnumerable<ISftpFile> DeletionListAsync(string path,[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach(var entry in client.ListDirectoryAsync(path,ct).WhereDeletionEntries(ct)) { transferActivity?.Invoke(); yield return entry; }
    }
    private async Task<ISftpFile> ReadDeletionFileAsync(string path,CancellationToken ct,Exception? missingSource=null)
    {
        path=FileDeletionService.Normalize(path,false);
        var parent=RemotePath.Parent(path);
        var container=await client.GetAsync(parent,ct);
        transferActivity?.Invoke();
        if(container.FullName!=parent || !container.IsDirectory || container.IsSymbolicLink)
            throw new IOException(L.T("Katalog nadrzędny jest dowiązaniem. Otwórz rzeczywistą lokalizację i spróbuj ponownie."));
        try
        {
            var file=await client.GetAsync(path,ct);
            transferActivity?.Invoke();
            if(file.FullName==path && !file.IsSymbolicLink)return Supported(file);
        }
        catch(Exception ex) when(ex is Renci.SshNet.Common.SftpPathNotFoundException or Renci.SshNet.Common.SftpPermissionDeniedException)
        { /* A dangling/inaccessible link target must not hide the actual link entry. */ }
        // Canonicalization may have resolved the selected symlink. Recover the
        // actual directory entry (also works for a dangling link), never its target.
        await foreach(var candidate in DeletionListAsync(parent,ct))
            if(candidate.FullName==path)return Supported(candidate);
        // A missing transfer source needs path guidance, while deletion keeps
        // its existing stale-preview guard. Never bypass parent/link checks.
        throw missingSource??Changed();
    }
    private static ISftpFile Supported(ISftpFile file)
    {
        if(!file.IsDirectory && !file.IsSymbolicLink && !file.IsRegularFile)throw new IOException(L.T("Usuwanie tego rodzaju pliku nie jest obsługiwane."));
        return file;
    }
}

internal static class DeletionListing
{
    internal static async IAsyncEnumerable<ISftpFile> WhereDeletionEntries(this IAsyncEnumerable<ISftpFile> source,[System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    { await foreach(var entry in source.WithCancellation(ct))if(entry.Name is not ("." or ".."))yield return entry; }
}
