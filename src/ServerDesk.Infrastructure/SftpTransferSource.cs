using Renci.SshNet.Common;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed partial class SftpSession
{
    public Task<string> ResolveDirectoryAsync(string path,CancellationToken ct)=>RunAsync(async()=>
    {
        if(string.IsNullOrWhiteSpace(path))throw new ArgumentException(L.T("Podaj katalog docelowy."));
        var directory=await client.GetAsync(path,ct);
        if(!directory.IsDirectory || directory.IsSymbolicLink)throw InvalidTransferTarget();
        return directory.FullName;
    },ct);
    public Task<IReadOnlyList<FileEntry>> ListTransferChildrenAsync(string path,CancellationToken ct)=>RunAsync<IReadOnlyList<FileEntry>>(async()=>
    {
        var directory=await ReadDeletionFileAsync(path,ct);
        if(!directory.IsDirectory || directory.IsSymbolicLink)throw new IOException(L.T("Źródło transferu zmieniło typ. Odśwież listę i wybierz je ponownie."));
        var entries=new List<FileEntry>();
        await foreach(var entry in DeletionListAsync(path,ct))
        {
            if(entries.Count>=TransferTraversal.MaximumEntries)throw new IOException(L.T("Transfer przekracza limit 20 000 elementów lub 64 poziomów. Prześlij mniejsze grupy."));
            entries.Add(Entry(Supported(entry)));
        }
        return entries;
    },ct);
    public Task<FileEntry> ReadTransferSourceAsync(FileEntry expected,bool resolveLink,CancellationToken ct)=>RunAsync(async()=>
    {
        var actual=await ReadDeletionFileAsync(expected.FullPath,ct,new SftpPathNotFoundException(L.T("Plik lub katalog źródłowy jest niedostępny.")));
        if(actual.IsSymbolicLink!=expected.IsSymbolicLink || actual.IsDirectory!=expected.IsDirectory)throw new IOException(L.T("Źródło transferu zmieniło typ. Odśwież listę i wybierz je ponownie."));
        if(!actual.IsSymbolicLink || !resolveLink)return Entry(actual);
        try
        {
            var target=Supported(await client.GetAsync(actual.FullName,ct));
            if(target.IsSymbolicLink)throw new IOException(L.T("Cel dowiązania jest niedostępny lub tworzy pętlę. Sprawdź ścieżkę i uprawnienia."));
            return Entry(target);
        }
        catch(SftpPathNotFoundException ex) { throw new IOException(L.T("Cel dowiązania jest niedostępny lub tworzy pętlę. Sprawdź ścieżkę i uprawnienia."),ex); }
    },ct);

    public Task CreateSymbolicLinkAsync(string target,string link,CancellationToken ct)=>RunAsync(async()=>
    {
        link=FileDeletionService.Normalize(link,false);
        var source=await client.GetAsync(target,ct); Supported(source);
        var parent=await client.GetAsync(RemotePath.Parent(link),ct);
        if(parent.FullName!=RemotePath.Parent(link) || !parent.IsDirectory || parent.IsSymbolicLink)throw InvalidTransferTarget();
        // Exists resolves links and misses dangling links; inspect the leaf entry.
        await foreach(var entry in DeletionListAsync(parent.FullName,ct))if(entry.FullName==link)throw new IOException(L.T("Cel już istnieje."));
        ct.ThrowIfCancellationRequested(); client.SymbolicLink(source.FullName,link);
    },ct);

    public Task EnsureTransferDirectoryAsync(string path,CancellationToken ct)=>RunAsync(async()=>
    {
        await ValidateTransferTargetAsync(path,true,ct);
        if(!client.Exists(path)) { ct.ThrowIfCancellationRequested(); client.CreateDirectory(path); }
    },ct);

    private static IOException InvalidTransferTarget()=>new(L.T("Cel transferu jest dowiązaniem lub ma inny typ. Wybierz zwykły plik albo katalog we właściwej lokalizacji."));
    private async Task ValidateTransferTargetAsync(string path,bool directory,CancellationToken ct)
    {
        path=FileDeletionService.Normalize(path,false);
        var parent=RemotePath.Parent(path); var container=await client.GetAsync(parent,ct);
        if(container.FullName!=parent || !container.IsDirectory || container.IsSymbolicLink)throw InvalidTransferTarget();
        if(!await client.ExistsAsync(path,ct))return;
        var target=await client.GetAsync(path,ct);
        if(target.FullName!=path || target.IsSymbolicLink || directory!=target.IsDirectory || !directory && !target.IsRegularFile)throw InvalidTransferTarget();
    }
}
