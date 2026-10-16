using Avalonia.Controls;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private readonly List<(MenuItem Menu,bool? Remote)> deleteFileMenus=[];
    private bool fileDeletionOpen;
    private MenuItem DeleteSelectionMenu(bool? remote)
    { var item=OperationMenu("Usuń zaznaczone…",DeleteFileAsync); deleteFileMenus.Add((item,remote)); return item; }
    private async Task DeleteFileAsync()
    {
        var source=sftp; var remote=RemoteSelected; var selected=SelectedFiles(remote);
        if(source==null || fileDeletionOpen || selected.Count==0)return;
        fileDeletionOpen=true; UpdateFileSelection();
        try
        {
            var service=remote?source.CreateDeletionService():new FileDeletionService(new LocalDeletionFileSystem());
            var result=await FileDeletionDialog.Show(owner,service,selected,connectionLifetime.Token,remote?Profile.Name+" · "+Profile.Endpoint:null);
            if(result?.Error is {} error)owner.ReportBackgroundError(L.T("Usuwanie plików zatrzymało się po błędzie."),error);
            if(disposed==0 && ReferenceEquals(source,sftp) && !connectionLifetime.IsCancellationRequested && result!=null)await RefreshAsync();
        }
        finally { fileDeletionOpen=false; if(disposed==0)UpdateFileSelection(); }
    }
}
