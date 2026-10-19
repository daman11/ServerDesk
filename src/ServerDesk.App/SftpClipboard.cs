using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private enum FileCopyKind { Names,Paths,Directory }
    private readonly List<(MenuItem Item,bool? Remote,FileCopyKind? Kind)> fileCopyMenus=[];
    private bool CanCopyFileText(bool remote,FileCopyKind? kind,int? selectedCount=null)
    {
        if(disposed!=0 || fileRefreshes>0 || fileViewTasks.Count>0 || !FilePathConfirmed(remote))return false;
        return kind==FileCopyKind.Directory || kind==null || (selectedCount??SelectedFiles(remote).Count)>0;
    }
    private MenuItem FileClipboardMenu(bool? remote)
    {
        var group=DeskIcons.ActionMenu("Kopiuj do schowka");
        MenuItem Item(string label,FileCopyKind kind)
        {
            var item=OperationMenu(label,()=>CopyFileTextAsync(remote??RemoteSelected,kind));
            if(kind==FileCopyKind.Paths) { item.InputGesture=KeyGesture.Parse("Ctrl+Shift+C"); Avalonia.Automation.AutomationProperties.SetName(item,L.T(label)+" · Ctrl+Shift+C"); }
            ToolTip.SetTip(item,L.T("Kopiuje zwykły tekst. Nie przesyła plików ani nie uruchamia poleceń."));
            fileCopyMenus.Add((item,remote,kind)); return item;
        }
        group.ItemsSource=new[]{Item("Kopiuj nazwy",FileCopyKind.Names),Item("Kopiuj pełne ścieżki",FileCopyKind.Paths),Item("Kopiuj ścieżkę katalogu",FileCopyKind.Directory)};
        fileCopyMenus.Add((group,remote,null)); return group;
    }
    private async Task CopyFileTextAsync(bool remote,FileCopyKind kind)
    {
        if(!CanCopyFileText(remote,kind))return;
        // Capture the chosen side and text before the asynchronous clipboard call.
        var text=kind==FileCopyKind.Directory?remote?loadedRemotePath:loadedLocalPath:
            FileSelection.ClipboardText(SelectedFiles(remote),!remote,kind==FileCopyKind.Paths);
        if(text.Length==0)return;
        if(text.Length>FileSelection.ClipboardCharacters)throw new InvalidOperationException(L.T("Zaznaczenie jest zbyt duże do skopiowania. Wybierz mniej elementów."));
        var clipboard=TopLevel.GetTopLevel(this)?.Clipboard??throw new InvalidOperationException(L.T("Schowek systemowy jest niedostępny."));
        await clipboard.SetTextAsync(text);
    }
}
