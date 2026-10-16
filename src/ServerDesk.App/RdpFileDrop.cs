using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private Border? rdpFileDrop;
    private readonly TextBlock rdpFilesStatus=DesignSystem.Label(L.T("Pliki → schowek RDP"),true,12);
    private void ResetRdpFileStatus() { rdpFilesStatus.Text=L.T("Pliki → schowek RDP"); ToolTip.SetTip(rdpFilesStatus,null); }
    private void BuildRdpFileDrop(WrapPanel actions)
    {
        var files=Button("Wybierz pliki",PickRdpFilesAsync); files.Content=DeskIcons.Labelled("folder",L.T("Wybierz pliki"));
        var row=new Grid { ColumnDefinitions=new ColumnDefinitions("16,*,Auto"),ColumnSpacing=8,ClipToBounds=true };
        rdpFilesStatus.MaxWidth=190; rdpFilesStatus.TextTrimming=Avalonia.Media.TextTrimming.CharacterEllipsis; rdpFilesStatus.VerticalAlignment=VerticalAlignment.Center;
        row.Children.Add(DeskIcons.Create("upload",16)); Grid.SetColumn(rdpFilesStatus,1); row.Children.Add(rdpFilesStatus); Grid.SetColumn(files,2); row.Children.Add(files);
        rdpFileDrop=new Border { Name="RdpFileDrop",Child=row,Padding=new Thickness(8,3),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),ClipToBounds=true };
        SizeChanged+=(_,_)=>rdpFileDrop.MaxWidth=Math.Max(0,Bounds.Width-16);
        DesignSystem.Resource(rdpFileDrop,Border.BorderBrushProperty,"BorderDefault");
        ToolTip.SetTip(rdpFileDrop,L.T("Upuść pliki lub foldery tutaj. Następnie wybierz folder w Eksploratorze na serwerze i wklej Ctrl+V. Schowek musi być włączony."));
        Avalonia.Automation.AutomationProperties.SetName(rdpFileDrop,L.T("Upuść pliki do schowka RDP"));
        DragDrop.SetAllowDrop(rdpFileDrop,true);
        DragDrop.AddDragOverHandler(rdpFileDrop,(_,e)=> { e.DragEffects=CanPrepareRdpFiles && e.DataTransfer.Contains(DataFormat.File)?DragDropEffects.Copy:DragDropEffects.None; e.Handled=true; });
        DragDrop.AddDropHandler(rdpFileDrop,(_,e)=>
        {
            if(CanPrepareRdpFiles && e.DataTransfer.Contains(DataFormat.File))
            {
                var paths=(e.DataTransfer.TryGetFiles()??[]).Select(f=>f.TryGetLocalPath()).Where(p=>p!=null).Cast<string>().ToArray();
                _=owner.Safe(()=>PrepareRdpFilesAsync(paths));
            }
            e.Handled=true;
        });
        actions.Children.Add(rdpFileDrop);
    }
    private bool CanPrepareRdpFiles=>UseEmbeddedRdp && IsRdpLoggedIn && clipboard.IsChecked==true && !IsBusy && !stoppingRdp;
    private async Task PickRdpFilesAsync()
    {
        if(!CanPrepareRdpFiles)return;
        var root=TopLevel.GetTopLevel(this); if(root==null)return;
        var files=await root.StorageProvider.OpenFilePickerAsync(new() { Title=L.T("Pliki do schowka RDP"),AllowMultiple=true });
        if(files.Count>0)await PrepareRdpFilesAsync(files.Select(f=>f.TryGetLocalPath()??throw new ArgumentException(L.T("Wybierz pliki z lokalnego dysku."))));
    }
    internal async Task PrepareRdpFilesAsync(IEnumerable<string> paths)
    {
        if(!CanPrepareRdpFiles)throw new InvalidOperationException(L.T("Połącz osadzony pulpit RDP i włącz schowek przed przygotowaniem plików."));
        var host=embeddedRdp; var ct=connectionLifetime.Token; var root=TopLevel.GetTopLevel(this)??throw new InvalidOperationException(L.T("Okno sesji jest zamknięte."));
        using var busy=BeginBusy(L.T("Sprawdzanie plików do RDP…"));
        var selected=await Task.Run(()=>RdpFileSelection.Inspect(paths,ct),ct);
        var storage=new List<IStorageItem>();
        foreach(var path in selected.Paths)
        {
            ct.ThrowIfCancellationRequested();
            IStorageItem? item=Directory.Exists(path)?await root.StorageProvider.TryGetFolderFromPathAsync(new Uri(path)):await root.StorageProvider.TryGetFileFromPathAsync(new Uri(path));
            if(item==null)throw new IOException(L.T("Wybrany plik zniknął lub nie jest dostępny.")); storage.Add(item);
        }
        ct.ThrowIfCancellationRequested();
        if(!ReferenceEquals(embeddedRdp,host) || !IsRdpLoggedIn || clipboard.IsChecked!=true)throw new InvalidOperationException(L.T("Sesja RDP została rozłączona. Przygotuj pliki ponownie po połączeniu."));
        var clip=root.Clipboard??throw new NotSupportedException(L.T("Schowek systemowy nie jest dostępny."));
        await clip.SetFilesAsync(storage);
        rdpFilesStatus.Text=L.F($"{selected.Paths.Count} elementów w schowku · wklej na serwerze Ctrl+V");
        ToolTip.SetTip(rdpFilesStatus,L.T("Pliki przygotowane do kopiowania. Wybierz folder docelowy w zdalnym Eksploratorze i wklej. Utrata sieci może przerwać kopiowanie; ponów je po reconnect."));
    }
}
