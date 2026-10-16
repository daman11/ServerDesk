using Avalonia.Controls;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private readonly List<(MenuItem Menu,bool? Remote)> linkFileMenus=[];
    private MenuItem LinkFileMenu(bool? remote)
    {
        var menu=OperationMenu("Kopiuj zawartość dowiązania…",()=>
        { if(remote.HasValue)fileTarget.SelectedIndex=remote.Value?1:0; return CopyLinkContentsAsync(); });
        linkFileMenus.Add((menu,remote)); return menu;
    }
    private async Task<FileEntry> ResolveLinkAsync(bool remote,FileEntry entry,CancellationToken ct)
    {
        if(!entry.IsSymbolicLink)throw new InvalidOperationException(L.T("Wybierz jedno dowiązanie symboliczne."));
        if(remote)return await (sftp??throw new InvalidOperationException(L.T("Otwórz panel SFTP przed przesłaniem pliku."))).ReadTransferSourceAsync(entry,true,ct);
        return await Task.Run(()=>
        {
            ct.ThrowIfCancellationRequested();
            if(!LocalTransferSource.Read(entry.FullPath).IsSymbolicLink)throw new IOException(L.T("Źródło transferu zmieniło typ. Odśwież listę i wybierz je ponownie."));
            return LocalTransferSource.Read(entry.FullPath,true);
        },ct);
    }
    private async Task CopyLinkContentsAsync()
    {
        var remote=RemoteSelected; var entry=SingleFile(remote);
        if(entry==null || sftp==null || !IsConnected)return;
        var ct=connectionLifetime.Token;
        FileEntry resolved;
        using(BeginBusy(L.T("Sprawdzanie celu dowiązania…")))resolved=await ResolveLinkAsync(remote,entry,ct);
        // Queue the approved resolved source and destination, not the live link.
        var target=remote?LocalFileName.InDirectory(FileDirectory(false),entry.Name):RemotePath.Join(FileDirectory(true),entry.Name);
        var message=L.F($"Serwer: {Profile.Endpoint}\nDowiązanie: {entry.FullPath}\nRzeczywisty cel: {resolved.FullPath}\nKopiuj do: {target}\n\nZostanie skopiowana zawartość celu jako zwykły plik lub katalog. Cel może znajdować się poza katalogiem dowiązania. Dowiązania wewnątrz katalogu będą pomijane; obiekty dowiązań nie są tworzone.");
        if(!await Dialogs.Confirm(owner,L.T("Kopiować zawartość dowiązania?"),message,ct))return;
        ct.ThrowIfCancellationRequested(); if(!IsConnected || disposed!=0)return;
        if(remote)await TransferAsync(entry.Name+L.T(" → komputer"),resolved.FullPath,target,(connection,report,token)=>DownloadTree(connection,resolved,target,report,token,new(false)));
        else await TransferAsync(entry.Name+L.T(" → serwer"),resolved.FullPath,target,(connection,report,token)=>UploadTree(connection,resolved.FullPath,target,report,token,new(true)));
    }
    private async Task CreateRemoteLinkAsync()
    {
        if(SingleFile(true) is not {} entry || sftp==null || !IsConnected)return;
        var ct=connectionLifetime.Token; var connection=sftp; var directory=FileDirectory(true);
        FileEntry target;
        using(BeginBusy(L.T("Sprawdzanie celu dowiązania…")))target=await connection.ReadTransferSourceAsync(entry,true,ct);
        var name=await Dialogs.Prompt(owner,L.F($"Nazwa nowego dowiązania do {target.FullPath}"),entry.Name+".link");
        if(name==null || !ReferenceEquals(sftp,connection) || !IsConnected)return;
        await connection.CreateSymbolicLinkAsync(target.FullPath,RemotePath.Join(directory,name),ct); await RefreshAsync();
    }
}
