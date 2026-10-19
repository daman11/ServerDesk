using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using Serilog;
using ServerDesk.Core;

namespace ServerDesk.App;

internal sealed partial class SessionPane
{
    private Border? rdpFileDrop;
    private Button? rdpReceive;
    private bool preparingRdpFiles;
    private readonly TextBlock rdpFilesStatus=DesignSystem.Label(L.T("Pliki → schowek RDP"),true,12);
    private void ResetRdpFileStatus() { rdpFilesStatus.Text=L.T("Pliki → schowek RDP"); ToolTip.SetTip(rdpFilesStatus,null); }
    private void BuildRdpFileDrop(WrapPanel actions)
    {
        var files=Button("Wybierz pliki",PickRdpFilesAsync); files.Content=DeskIcons.Labelled("folder",L.T("Wybierz pliki"));
        var row=new Grid { ColumnDefinitions=new ColumnDefinitions("16,*,Auto"),ColumnSpacing=8,ClipToBounds=true };
        rdpFilesStatus.MaxWidth=190; rdpFilesStatus.TextTrimming=Avalonia.Media.TextTrimming.CharacterEllipsis; rdpFilesStatus.VerticalAlignment=VerticalAlignment.Center;
        row.Children.Add(DeskIcons.Create("upload",16)); Grid.SetColumn(rdpFilesStatus,1); row.Children.Add(rdpFilesStatus); Grid.SetColumn(files,2); row.Children.Add(files);
        // A null background leaves the padding and gaps outside hit testing;
        // Explorer drops there never reach this routed handler.
        rdpFileDrop=new Border { Name="RdpFileDrop",Width=360,Background=Brushes.Transparent,Child=row,Padding=new Thickness(8,3),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),ClipToBounds=true };
        SizeChanged+=(_,_)=>rdpFileDrop.MaxWidth=Math.Max(0,Bounds.Width-16);
        DesignSystem.Resource(rdpFileDrop,Border.BorderBrushProperty,"BorderDefault");
        ToolTip.SetTip(rdpFileDrop,L.T("Upuść pliki lub foldery tutaj. Następnie wybierz folder w Eksploratorze na serwerze i wklej Ctrl+V. Schowek musi być włączony."));
        Avalonia.Automation.AutomationProperties.SetName(rdpFileDrop,L.T("Upuść pliki do schowka RDP"));
        DragDrop.SetAllowDrop(rdpFileDrop,true);
        DragDrop.AddDragOverHandler(rdpFileDrop,(_,e)=> { e.DragEffects=CanPrepareRdpFiles && e.DataTransfer.Contains(DataFormat.File)?DragDropEffects.Copy:DragDropEffects.None; e.Handled=true; });
        DragDrop.AddDropHandler(rdpFileDrop,(_,e)=>
        {
            e.Handled=true; e.DragEffects=DragDropEffects.None;
            if(!CanPrepareRdpFiles || !e.DataTransfer.Contains(DataFormat.File))return;
            try
            {
                // Snapshot native OLE items before the Drop event returns. Do
                // not silently discard a virtual/unavailable item in a group.
                var paths=(e.DataTransfer.TryGetFiles()??[]).Select(f=>f.TryGetLocalPath()??throw new ArgumentException(L.T("Wybierz pliki z lokalnego dysku."))).ToArray();
                if(paths.Length==0)return;
                e.DragEffects=DragDropEffects.Copy;
                Log.Information("RDP file drop accepted: {Count} local items",paths.Length);
                _=owner.Safe(()=>PrepareRdpFilesAsync(paths));
            }
            catch(Exception ex) { _=owner.Safe(()=>Task.FromException(ex)); }
        });
        actions.Children.Add(rdpFileDrop);
        rdpReceive=Button("Pliki z RDP",ReceiveRdpFilesAsync);rdpReceive.Content=DeskIcons.Labelled("download",L.T("Pliki z RDP"));
        ToolTip.SetTip(rdpReceive,L.T("Skopiuj pliki w RDP przez Ctrl+C. Otwórz listę i przeciągnij je do lokalnego folderu."));actions.Children.Add(rdpReceive);
    }
    private async Task ReceiveRdpFilesAsync()
    {
        if(!CanPrepareRdpFiles || embeddedRdp==null)return;
        var host=embeddedRdp;var root=TopLevel.GetTopLevel(this) as Window??owner;
        var token=connectionLifetime.Token;
        // Shell may read IStream on a COM worker. This guard must not access
        // Avalonia controls/properties from that apartment.
        await RdpClipboardExportDialog.ShowAsync(root,()=>ReferenceEquals(embeddedRdp,host) && host.IsLoggedIn && !token.IsCancellationRequested,token);
    }
    private bool CanPrepareRdpFiles=>UseEmbeddedRdp && IsRdpLoggedIn && clipboard.IsChecked==true && !IsBusy && !preparingRdpFiles && !stoppingRdp;
    internal async Task DropFilesOnRdpAsync(WindowsRdpHost host,string[] paths,PixelPoint point)
    {
        var bounds=host.CaptureDropBounds(point);
        owner.FocusSession(this);(TopLevel.GetTopLevel(this) as Window)?.Activate();host.FocusDesktop();
        var sequence=await PrepareRdpFilesAsync(paths);
        if(!ReferenceEquals(embeddedRdp,host) || !IsActive || owner.Workspace.Focused!=WorkspaceId || TopLevel.GetTopLevel(this) is not Window { IsActive:true })
            throw new InvalidOperationException(L.T("Cel zrzutu lub schowek zmienił się. Upuść pliki ponownie."));
        await host.PasteFilesAtAsync(point,bounds,sequence,connectionLifetime.Token);
        rdpFilesStatus.Text=L.T("Wysłano wklejenie plików do RDP · sprawdź okno kopiowania na serwerze");
        ToolTip.SetTip(rdpFilesStatus,L.T("Upuszczaj na widoczny pulpit lub otwarty folder zdalnego Eksploratora. Zakończenie i konflikty kopiowania obsługuje Windows na serwerze."));
        Log.Information("RDP desktop drop submitted a paste for {Count} local items",paths.Length);
    }
    private async Task PickRdpFilesAsync()
    {
        if(!CanPrepareRdpFiles)return;
        var root=TopLevel.GetTopLevel(this); if(root==null)return;
        var files=await root.StorageProvider.OpenFilePickerAsync(new() { Title=L.T("Pliki do schowka RDP"),AllowMultiple=true });
        if(files.Count>0)await PrepareRdpFilesAsync(files.Select(f=>f.TryGetLocalPath()??throw new ArgumentException(L.T("Wybierz pliki z lokalnego dysku."))));
    }
    internal async Task<uint> PrepareRdpFilesAsync(IEnumerable<string> paths)
    {
        if(!CanPrepareRdpFiles)throw new InvalidOperationException(L.T("Połącz osadzony pulpit RDP i włącz schowek przed przygotowaniem plików."));
        var host=embeddedRdp; var ct=connectionLifetime.Token; var root=TopLevel.GetTopLevel(this)??throw new InvalidOperationException(L.T("Okno sesji jest zamknięte."));
        // Keep the native viewport stationary while inspecting a direct drop.
        // The general busy row would resize it and invalidate the drop point.
        preparingRdpFiles=true;rdpFilesStatus.Text=L.T("Sprawdzanie plików do RDP…");UpdateRdpActions();
        var prepared=false;
        try
        {
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
        prepared=true;
        return OperatingSystem.IsWindows()?WindowsRdpHost.GetClipboardSequenceNumber():0;
        }
        finally {preparingRdpFiles=false;if(!prepared)ResetRdpFileStatus();if(disposed==0)UpdateRdpActions();}
    }
}
