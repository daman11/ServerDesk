using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private readonly TextBlock localSelection=DesignSystem.Label("",true,12),remoteSelection=DesignSystem.Label("",true,12);
    private Button uploadSelection=null!,downloadSelection=null!;
    private readonly List<(MenuItem Menu,bool? Remote)> singleFileMenus=[];
    private readonly List<(MenuItem Menu,bool? Remote,bool Transfer)> fileActionMenus=[];
    private bool changingFileSelection;

    private IReadOnlyList<FileEntry> SelectedFiles(bool remote)=>disposed!=0 || fileRefreshes>0 || fileViewTasks.Count>0 || (remote?loadedRemotePath:loadedLocalPath).Length>0 && !FilePathConfirmed(remote)?[]:FileSelection.Capture((remote?remoteList:localList).SelectedItems?.OfType<FileEntry>()??[],!remote);
    private FileEntry? SingleFile(bool remote)
    {
        var selected=SelectedFiles(remote);
        if(selected.Count>1)throw new InvalidOperationException(L.T("Dla tej operacji zaznacz jeden element."));
        return selected.SingleOrDefault();
    }
    private MenuItem SingleFileMenu(bool? remote,string title,Func<Task> action)
    {
        var item=OperationMenu(title,action); singleFileMenus.Add((item,remote)); return item;
    }
    private MenuItem FileActionMenu(bool? remote,string title,Func<Task> action,bool transfer=false)
    { var item=OperationMenu(title,action); fileActionMenus.Add((item,remote,transfer)); return item; }
    private FileDrag? CaptureFileDrag(bool remote)
    {
        var selected=SelectedFiles(remote);
        return selected.Count==0 || selected.Any(e=>e.IsSymbolicLink)?null:new(this,remote,selected);
    }
    private void BuildFileSelection()
    {
        foreach(var (list,label,column) in new[]{(localList,localSelection,0),(remoteList,remoteSelection,2)})
        {
            list.SelectionMode=SelectionMode.Multiple;
            list.Classes.Add("sftp-list");
            list.SelectionChanged+=(_,_)=> { if(!changingFileSelection)UpdateFileSelection(); };
            label.Name=column==0?"LocalSelectionSummary":"RemoteSelectionSummary";
            label.TextTrimming=TextTrimming.CharacterEllipsis;
            var footer=new Border { Child=label,Padding=new Thickness(8,5),BorderThickness=new Thickness(0,1,0,0) };
            DesignSystem.Resource(footer,Border.BackgroundProperty,"BackgroundSecondary"); DesignSystem.Resource(footer,Border.BorderBrushProperty,"BorderDefault");
            Grid.SetRow(footer,5); Grid.SetColumn(footer,column); files.Children.Add(footer);
            ToolTip.SetTip(label,L.T("Ctrl+klik: dodaj do zaznaczenia · Shift+klik: zakres · Ctrl+A: zaznacz widoczne · Escape: wyczyść. Transfer obejmuje całe zaznaczenie."));
        }
        UpdateFileSelection();
    }
    private void UpdateFileSelection()
    {
        var local=SelectedFiles(false); var remote=SelectedFiles(true);
        var viewReady=disposed==0 && fileRefreshes==0 && fileViewTasks.Count==0;
        var canOperate=viewReady && IsConnected && sftp?.IsConnected==true;
        localSelection.Text=L.F($"Zaznaczone: {local.Count} / {localList.Items.OfType<FileEntry>().Count(e=>e.Name!="..")}");
        remoteSelection.Text=L.F($"Zaznaczone: {remote.Count} / {remoteList.Items.OfType<FileEntry>().Count(e=>e.Name!="..")}");
        if(uploadSelection!=null) { uploadSelection.IsEnabled=canOperate && FilePathConfirmed(true) && local.Count>0; ToolTip.SetTip(uploadSelection,L.F($"Wyślij zaznaczone elementy: {local.Count}")); }
        if(downloadSelection!=null) { downloadSelection.IsEnabled=canOperate && FilePathConfirmed(false) && remote.Count>0; ToolTip.SetTip(downloadSelection,L.F($"Pobierz zaznaczone elementy: {remote.Count}")); }
        foreach(var (menu,isRemote) in singleFileMenus)menu.IsEnabled=canOperate && SelectedFiles(isRemote??RemoteSelected).Count==1;
        foreach(var (menu,isRemote,transfer) in fileActionMenus) { var side=isRemote??RemoteSelected; menu.IsEnabled=transfer?(side?downloadSelection:uploadSelection)?.IsEnabled==true:canOperate && FilePathConfirmed(side); }
        foreach(var (menu,isRemote) in textEditingMenus)
        {
            var remoteSide=isRemote??RemoteSelected; var selected=SelectedFiles(remoteSide);
            menu.IsEnabled=viewReady && selected is { Count:1 } && !selected[0].IsDirectory && !selected[0].IsSymbolicLink && (!remoteSide || canOperate && FilePathConfirmed(false));
        }
        foreach(var (menu,isRemote) in linkFileMenus)menu.IsEnabled=canOperate && SelectedFiles(isRemote??RemoteSelected) is { Count:1 } selected && selected[0].IsSymbolicLink;
        foreach(var (menu,isRemote) in deleteFileMenus)menu.IsEnabled=!fileDeletionOpen && canOperate && SelectedFiles(isRemote??RemoteSelected).Count>0;
        foreach(var (item,isRemote,kind) in fileCopyMenus) { var side=isRemote??RemoteSelected; item.IsEnabled=CanCopyFileText(side,kind,side?remote.Count:local.Count); }
    }
    private Task UploadFilesAsync(IReadOnlyList<FileEntry> entries)
    {
        if(sftp==null)throw new InvalidOperationException(L.T("Otwórz panel SFTP przed przesłaniem pliku."));
        var plan=FileSelection.Upload(entries,FileDirectory(true));
        return Task.WhenAll(plan.Select(item=>TransferAsync(item.Entry.Name+L.T(" → serwer"),item.Entry.FullPath,item.Target,(connection,report,ct)=>UploadTree(connection,item.Entry.FullPath,item.Target,report,ct,new(true)))));
    }
    private Task DownloadFilesAsync(IReadOnlyList<FileEntry> entries)
    {
        var plan=FileSelection.Download(entries,FileDirectory(false));
        return Task.WhenAll(plan.Select(item=>TransferAsync(item.Entry.Name+L.T(" → komputer"),item.Entry.FullPath,item.Target,(connection,report,ct)=>DownloadTree(connection,item.Entry,item.Target,report,ct,new(false)))));
    }
}
