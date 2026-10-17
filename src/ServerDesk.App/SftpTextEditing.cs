using Avalonia.Controls;
using Avalonia.Input;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private readonly List<(MenuItem Menu,bool? Remote)> textEditingMenus=[];
    private MenuItem TextEditingMenu(bool? remote)
    {
        var menu=OperationMenu(remote==true?"Pobierz i edytuj":"Edytuj plik",()=>EditTextFileAsync(remote??RemoteSelected));
        menu.InputGesture=new KeyGesture(Key.F4);
        ToolTip.SetTip(menu,L.T("Plik zdalny zostanie pobrany do otwartego katalogu lokalnego. Zapisane zmiany wyślij na serwer przez Wyślij."));
        textEditingMenus.Add((menu,remote)); return menu;
    }
    private async Task EditTextFileAsync(bool remote)
    {
        if(SingleFile(remote) is not {} entry)return;
        if(entry.IsDirectory || entry.IsSymbolicLink)throw new InvalidOperationException(L.T("Wybierz jeden zwykły plik do edycji. Katalogi i dowiązania nie są obsługiwane."));
        var ct=connectionLifetime.Token;
        // Read saved preferences: an unsaved settings preview must not launch a different program.
        var preferences=await repository.PreferencesAsync(ct);
        await Task.Run(()=>TextEditorLauncher.EnsureAvailable(preferences),ct);
        var path=entry.FullPath;
        if(remote)
        {
            if(!IsConnected || sftp==null || !FilePathConfirmed(false))throw new InvalidOperationException(L.T("Otwórz i potwierdź lokalny katalog SFTP przed pobraniem pliku do edycji."));
            path=LocalFileName.InDirectory(FileDirectory(false),entry.Name);
            var downloaded=false;
            CancellationToken downloadToken=default;
            await TransferAsync(entry.Name+L.T(" → edytor"),entry.FullPath,path,async(connection,report,cancel)=>
            {
                downloadToken=cancel;
                var outcome=await DownloadTree(connection,entry,path,report,cancel,new(false));
                downloaded=outcome.Files==1 && outcome.Skipped==0;
                return outcome;
            });
            if(!downloaded || downloadToken.IsCancellationRequested || ct.IsCancellationRequested || disposed!=0)return;
        }
        ct.ThrowIfCancellationRequested();
        var local=await Task.Run(()=>LocalTransferSource.Read(path),ct);
        if(local.IsDirectory || local.IsSymbolicLink)throw new InvalidOperationException(L.T("Wybierz jeden zwykły plik do edycji. Katalogi i dowiązania nie są obsługiwane."));
        await Task.Run(()=>TextEditorLauncher.Open(preferences,local.FullPath),ct);
    }
}
