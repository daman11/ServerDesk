using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Serilog;
using ServerDesk.Core;

namespace ServerDesk.App;

internal static class RdpClipboardExportDialog
{
    internal static async Task ShowAsync(Window owner,Func<bool> available,CancellationToken ct,Func<WindowsRdpClipboardExport>? capture=null)
    {
        WindowsRdpClipboardExport? selection=null;var dragging=false;PointerPressedEventArgs? press=null;Point origin=default;
        var title=L.T("Pliki z RDP na ten komputer");
        var status=DesignSystem.Label("",true);status.TextWrapping=TextWrapping.Wrap;
        var listing=new ListBox {Name="RdpExportFiles",MaxHeight=230,MinHeight=70,ItemTemplate=new FuncDataTemplate<RdpClipboardFile>((file,_)=>DeskIcons.Labelled(file?.IsDirectory==true?"folder":"file",file?.Name??""),true)};
        var handle=new Border {Name="RdpExportDrag",Background=Brushes.Transparent,Padding=new Thickness(14),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Focusable=true,Child=DeskIcons.Labelled("download",L.T("Przeciągnij pliki do lokalnego folderu"))};
        DesignSystem.Resource(handle,Border.BorderBrushProperty,"AccentPrimary");AutomationProperties.SetName(handle,L.T("Przeciągnij pliki do lokalnego folderu"));
        var refresh=Dialogs.Button("Odśwież",()=>Load());
        Window? dialog=null;var close=Dialogs.Button("Zamknij",()=> {if(!dragging)dialog?.Close();});
        // The clipboard is shared by Windows, including other RDP sessions.
        // Show the actual names rather than claiming these belong to this tab.
        var content=new StackPanel {Spacing=14,Children={DesignSystem.PageHeading("download",title),Dialogs.Text(L.T("Funkcja eksperymentalna. Zrzut do rzeczywistego Eksploratora wymaga dalszych testów. Alternatywnie użyj Ctrl+V w lokalnym folderze.")),Dialogs.Text(L.T("1. Zaznacz pliki lub foldery w zdalnym Eksploratorze i naciśnij Ctrl+C.\n2. Otwórz to okno i sprawdź listę.\n3. Przeciągnij poniższy uchwyt do folderu lub na pulpit tego komputera.")),Dialogs.Text(L.T("Lista pochodzi ze wspólnego schowka Windows. Sprawdź nazwy, zwłaszcza przy kilku sesjach RDP. Kopiowanie nie usuwa plików z serwera.")),listing,status,handle,new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Spacing=8,Children={refresh,close}}}};
        dialog=new Window {Title=title,Width=640,MinWidth=340,MaxHeight=760,SizeToContent=SizeToContent.Height,CanResize=true,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=new Border {Padding=new Thickness(20),Child=new ScrollViewer {Content=content,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled}}};
        void Load()
        {
            selection?.Dispose();selection=null;listing.ItemsSource=null;handle.IsEnabled=false;
            try
            {
                ct.ThrowIfCancellationRequested();if(!available())throw new InvalidOperationException(L.T("Połącz osadzony pulpit RDP i włącz schowek przed przygotowaniem plików."));
                selection=capture?.Invoke()??WindowsRdpClipboardExport.Capture(()=>!ct.IsCancellationRequested && available());
                listing.ItemsSource=selection.Files.Entries;
                status.Text=L.F($"{selection.Files.Entries.Count} elementów · {selection.Files.Bytes} B · gotowe do przeciągnięcia");
                handle.IsEnabled=true;
            }
            catch(Exception error)
            {
                status.Text=error is InvalidOperationException or InvalidDataException?error.Message:L.T("Nie można odczytać plików ze schowka. Skopiuj je ponownie w RDP i wybierz Odśwież.");
                if(error is not OperationCanceledException)ApplicationDiagnostics.Report("RDP.ReadClipboardFiles",error,Serilog.Events.LogEventLevel.Warning);
            }
        }
        handle.PointerPressed+=(_,e)=> {if(e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed){press=e;origin=e.GetPosition(handle);}};
        handle.PointerReleased+=(_,_)=>press=null;handle.PointerCaptureLost+=(_,_)=>press=null;
        handle.PointerMoved+=(_,e)=>
        {
            if(press==null || dragging || selection==null || !e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed)return;
            var delta=e.GetPosition(handle)-origin;if(Math.Abs(delta.X)+Math.Abs(delta.Y)<8)return;
            press=null;dragging=true;refresh.IsEnabled=close.IsEnabled=false;
            try
            {
                var submitted=selection.BeginDrag();
                status.Text=L.T(submitted?"Przekazano zrzut do systemu. Sprawdź postęp i wynik kopiowania w lokalnym Eksploratorze.":"Przeciąganie anulowane. Pliki na serwerze pozostają bez zmian.");
                Log.Information("RDP clipboard file drag ended: {Count} entries, target accepted={Accepted}",selection.Files.Entries.Count,submitted);
            }
            catch(Exception error)
            {
                ApplicationDiagnostics.Report("RDP.ExportClipboardDrag",error);
                status.Text=L.T("Nie udało się przekazać plików. Sprawdź sesję, skopiuj pliki ponownie i odśwież listę.");
            }
            finally {dragging=false;press=null;refresh.IsEnabled=close.IsEnabled=true;handle.IsEnabled=selection.IsCurrent;}
        };
        // Keyboard users can use Windows Explorer's ordinary Ctrl+V after the
        // same remote Ctrl+C; no forced download or hidden destination.
        ToolTip.SetTip(handle,L.T("Przeciągnij lewym przyciskiem myszy. Możesz też wkleić pliki w lokalnym Eksploratorze przez Ctrl+V."));
        var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(300)};
        timer.Tick+=(_,_)=> {if(selection!=null && !selection.IsCurrent){handle.IsEnabled=false;if(!dragging)status.Text=L.T("Schowek lub sesja zmieniły się. Skopiuj pliki ponownie i odśwież listę.");}};
        dialog.Opened+=(_,_)=> {Dialogs.FitToOwnerScreen(dialog,owner);Load();timer.Start();};
        dialog.Closed+=(_,_)=> {timer.Stop();selection?.Dispose();};
        dialog.KeyDown+=(_,e)=> {if(!dragging && e.Key==Key.Escape){dialog.Close();e.Handled=true;}};
        if(ct.IsCancellationRequested)return;
        using var cancelled=ct.Register(()=>Dispatcher.UIThread.Post(()=>dialog.Close()));
        await dialog.ShowDialog(owner);
    }
}
