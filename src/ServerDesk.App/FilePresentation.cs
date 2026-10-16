using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using ServerDesk.Core;

namespace ServerDesk.App;
internal sealed partial class SessionPane
{
    private MenuItem OperationMenu(string label,Func<Task> action)
    { var item=new MenuItem { Header=L.T(label),Icon=DeskIcons.ForAction(label) is {} glyph?DeskIcons.Create(glyph,16):null }; item.Click+=(_,_)=>_ = owner.Safe(action); return item; }
    private async Task MoveFileAsync()
    {
        var remote=RemoteSelected;
        if(SingleFile(remote) is not {} entry || sftp==null)return;
        var directory=await Dialogs.Prompt(owner,remote?L.T("Zdalny katalog docelowy (ścieżka absolutna)"):L.T("Lokalny katalog docelowy"),remote?remotePath.Text??"/":localPath.Text??"");
        if(directory==null)return;
        if(string.IsNullOrWhiteSpace(directory))throw new ArgumentException(L.T("Podaj katalog docelowy."));
        if(remote)
        {
            if(!directory.StartsWith('/'))throw new ArgumentException(L.T("Podaj absolutną ścieżkę zdalną, np. /home/admin."));
            await sftp.RenameAsync(entry.FullPath,RemotePath.Join(directory,entry.Name),connectionLifetime.Token);
        }
        else
        {
            var destination=Path.GetFullPath(directory); if(!Directory.Exists(destination))throw new ArgumentException(L.T("Katalog docelowy nie istnieje."));
            var target=Path.Combine(destination,entry.Name);
            if(File.Exists(target)||Directory.Exists(target))throw new IOException(L.T("Cel już istnieje. Przenoszenie nie nadpisuje plików."));
            if(entry.IsDirectory)Directory.Move(entry.FullPath,target); else File.Move(entry.FullPath,target);
        }
        await RefreshAsync();
    }
    private async Task FileDetailsAsync()
    {
        var remote=RemoteSelected;
        if(SingleFile(remote) is not {} entry)return;
        var detail=L.F($"{entry.FullPath}\nTyp: {FileType(entry)}\nRozmiar: {(entry.IsDirectory || entry.IsSymbolicLink?"—":FormatBytes(entry.Size))}\nUprawnienia: {(entry.Permissions.Length>0?entry.Permissions:L.T("niedostępne"))}\nModyfikacja: {(entry.Modified==DateTime.MinValue?"—":entry.Modified.ToString("yyyy-MM-dd HH:mm:ss"))}");
        if(entry.IsSymbolicLink)
        {
            FileEntry target;
            using(BeginBusy(L.T("Sprawdzanie celu dowiązania…")))target=await ResolveLinkAsync(remote,entry,connectionLifetime.Token);
            detail+=L.F($"\nRzeczywisty cel: {target.FullPath}\nTyp celu: {FileType(target)}");
        }
        await Dialogs.Message(owner,entry.Name,detail);
    }
    private static string FileType(FileEntry entry)=>entry.IsSymbolicLink?L.T("Dowiązanie"):entry.IsDirectory?L.T("Katalog"):L.T("Plik");
    private static Grid FileColumns()
    {
        var columns=new Grid { ColumnDefinitions=new ColumnDefinitions("20,*,72,72,54,126"),ColumnSpacing=DesignTokens.IconGap };
        DesignSystem.Resource(columns,Grid.MinHeightProperty,"FileRowHeight"); return columns;
    }
    private static void AdaptColumns(Grid grid,Control type,Control permissions,Control date)
    {
        void Column(int index,Control control,double width,double threshold) { var visible=grid.Bounds.Width>=threshold; grid.ColumnDefinitions[index].Width=new GridLength(visible?width:0); control.IsVisible=visible; }
        Column(3,type,72,590); Column(4,permissions,54,450); Column(5,date,126,390);
    }
    private void BuildFileRows()
    {
        foreach(var column in new[]{0,2})
        {
            var header=FileColumns(); header.Margin=new Thickness(DesignTokens.IconGap,0);
            var remote=column==2;
            var name=FileSortHeader(remote,FileSort.Name,L.T("Nazwa")); Grid.SetColumnSpan(name,2); header.Children.Add(name);
            var size=FileSortHeader(remote,FileSort.Size,L.T("Rozmiar"),true); Grid.SetColumn(size,2); header.Children.Add(size);
            var type=FileSortHeader(remote,FileSort.Type,L.T("Typ")); Grid.SetColumn(type,3); header.Children.Add(type);
            var permissions=DesignSystem.Label(L.T("Prawa"),true,DesignTokens.Micro); permissions.TextTrimming=TextTrimming.CharacterEllipsis; ToolTip.SetTip(permissions,L.T("Prawa")); Grid.SetColumn(permissions,4); header.Children.Add(permissions);
            var date=FileSortHeader(remote,FileSort.Modified,L.T("Modyfikacja")); Grid.SetColumn(date,5); header.Children.Add(date);
            header.SizeChanged+=(_,_)=>AdaptColumns(header,type,permissions,date); Grid.SetRow(header,3); Grid.SetColumn(header,column); files.Children.Add(header);
        }
        localList.ItemTemplate=remoteList.ItemTemplate=new FuncDataTemplate<FileEntry>((entry,_)=>
        {
            if(entry==null)return new TextBlock(); var row=FileColumns();
            row.Children.Add(DeskIcons.Create(FileIcons.Resolve(entry),16,"ForegroundSecondary"));
            var name=DesignSystem.Label(entry.Name); name.TextTrimming=TextTrimming.CharacterEllipsis; Grid.SetColumn(name,1); row.Children.Add(name);
            var size=DesignSystem.Label(entry.IsDirectory?"—":FormatBytes(entry.Size),true,DesignTokens.Caption); size.HorizontalAlignment=HorizontalAlignment.Right; Grid.SetColumn(size,2); row.Children.Add(size);
            var extension=Path.GetExtension(entry.Name); var type=DesignSystem.Label(!entry.IsDirectory && !entry.IsSymbolicLink && extension.Length>1?extension[1..].ToUpperInvariant():FileType(entry),true,DesignTokens.Caption); type.TextTrimming=TextTrimming.CharacterEllipsis; ToolTip.SetTip(type,type.Text); Grid.SetColumn(type,3); row.Children.Add(type);
            var permissions=DesignSystem.Label(entry.Permissions.Length>0?entry.Permissions:"—",true,DesignTokens.Caption); Grid.SetColumn(permissions,4); row.Children.Add(permissions);
            var date=DesignSystem.Label(entry.Modified==DateTime.MinValue?"—":entry.Modified.ToString("yyyy-MM-dd HH:mm"),true,DesignTokens.Caption); Grid.SetColumn(date,5); row.Children.Add(date);
            row.SizeChanged+=(_,_)=>AdaptColumns(row,type,permissions,date); ToolTip.SetTip(row,L.F($"{entry.FullPath}\n{FileType(entry)} · prawa: {entry.Permissions}")); return row;
        });
    }
}


