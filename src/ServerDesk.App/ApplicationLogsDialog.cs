using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal static partial class Dialogs
{
    internal static async Task ApplicationLogs(Window owner,string directory)
    {
        var view=new ApplicationLogsView(new(directory));
        var dialog=Window(L.T("Logi aplikacji"),view); dialog.Name="ApplicationLogsDialog"; dialog.Width=920; dialog.Height=680; dialog.MinWidth=600; dialog.MinHeight=480; dialog.SizeToContent=SizeToContent.Manual;
        view.CloseRequested=dialog.Close; dialog.Closed+=(_,_)=>view.Dispose();
        try { await dialog.ShowDialog(owner); }
        finally { view.Dispose(); await view.Shutdown; }
    }
}

internal sealed class ApplicationLogsView:Grid,IDisposable
{
    private readonly ApplicationLogFiles store;
    private readonly CancellationTokenSource lifetime=new();
    private CancellationTokenSource? reading;
    private readonly HashSet<Task> pending=[];
    private readonly ComboBox files=new() { Name="ApplicationLogFiles",HorizontalAlignment=HorizontalAlignment.Stretch,MaxDropDownHeight=260 };
    private readonly TextBox preview=new() { Name="ApplicationLogPreview",IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.NoWrap,FontFamily=new FontFamily("Cascadia Mono,Consolas,monospace"),FontSize=12 };
    private readonly TextBox search=new() { Name="ApplicationLogSearch",PlaceholderText=L.T("Szukaj w podglądzie…"),MaxLength=200 };
    private readonly TextBlock status=DesignSystem.Hint("");
    private readonly Button refresh,export,open;
    private bool disposed,refreshing,loading,exporting,folderExists,opening;
    private long revision;
    internal Action CloseRequested { get; set; }=()=> { };
    internal Func<ApplicationLogEntry,Task<string?>> ChooseExportDestination { get; set; }
    internal Func<DirectoryInfo,Task<bool>> LaunchFolder { get; set; }
    internal Task Shutdown=>Task.WhenAll(pending.ToArray());
    internal ApplicationLogsView(ApplicationLogFiles store)
    {
        this.store=store; Name="ApplicationLogsView"; RowDefinitions=new("Auto,Auto,Auto,*,Auto,Auto"); RowSpacing=10;
        var path=new SelectableTextBlock { Text=store.DirectoryPath,TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis }; ToolTip.SetTip(path,store.DirectoryPath);
        var explanation=DesignSystem.Hint(L.T("Podgląd pokazuje końcówkę dużego pliku (maks. 128 KiB). Zapisz kopię eksportuje cały plik z chwili rozpoczęcia zapisu."));
        var header=new StackPanel { Spacing=6,Children={DesignSystem.PageHeading("file",L.T("Logi aplikacji")),path,explanation} }; Children.Add(header);
        SizeChanged+=(_,_)=>explanation.IsVisible=Bounds.Height>=500;
        files.ItemTemplate=new FuncDataTemplate<ApplicationLogEntry>((entry,_)=>entry==null?null:new StackPanel { Spacing=2,Children={DesignSystem.Label(entry.Name),DesignSystem.Hint(entry.ModifiedUtc.ToLocalTime().ToString("g")+" · "+(entry.Bytes/1024d).ToString("N1")+" KiB")} });
        AutomationProperties.SetName(files,L.T("Plik dziennika")); AutomationProperties.SetName(preview,L.T("Treść dziennika")); AutomationProperties.SetName(search,L.T("Szukaj w podglądzie"));
        refresh=DeskIcons.Button("refresh","Odśwież logi",()=>_=RefreshAsync()); refresh.Name="ApplicationLogsRefresh";
        var selection=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=8,Children={files,refresh} }; Grid.SetColumn(refresh,1); Grid.SetRow(selection,1); Children.Add(selection);
        var next=DeskIcons.Button("search","Znajdź następne",FindNext); next.Name="ApplicationLogsFindNext";
        var find=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=8,Children={search,next} }; Grid.SetColumn(next,1); Grid.SetRow(find,2); Children.Add(find);
        preview.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,Avalonia.Controls.Primitives.ScrollBarVisibility.Auto); preview.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty,Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        preview.SizeChanged+=(_,_)=> { if(preview.IsKeyboardFocusWithin)Dispatcher.UIThread.Post(EnsureCaretVisible); };
        Grid.SetRow(preview,3); Children.Add(preview); status.Name="ApplicationLogsStatus"; Grid.SetRow(status,4); Children.Add(status);
        export=Dialogs.Button("Zapisz kopię logu",()=>_=Track(ExportAsync)); export.Name="ApplicationLogsExport";
        open=Dialogs.Button("Otwórz folder logów",()=>_=Track(OpenFolderAsync)); open.Name="ApplicationLogsOpenFolder";
        var close=Dialogs.Button("Zamknij",()=>CloseRequested()); close.Name="ApplicationLogsClose";
        var actions=new WrapPanel { Orientation=Orientation.Horizontal,Children={open,export,close} }; foreach(var action in actions.Children)action.Margin=new Thickness(0,2,8,2);
        var footer=new StackPanel { Spacing=6,Children={DesignSystem.Hint(L.T("Logi mogą zawierać ścieżki i identyfikatory profili. Przejrzyj plik przed udostępnieniem.")),actions} }; Grid.SetRow(footer,5); Children.Add(footer);
        files.SelectionChanged+=(_,_)=> { if(!refreshing)_=ReadSelectedAsync(); };
        search.KeyDown+=(_,e)=> { if(e.Key==Key.Enter) { FindNext(); e.Handled=true; } };
        AddHandler(KeyDownEvent,(_,e)=>
        {
            if(e.Key==Key.F && e.KeyModifiers==KeyModifiers.Control) { search.Focus(); search.SelectAll(); e.Handled=true; }
            else if(e.Key==Key.F5 && e.KeyModifiers==KeyModifiers.None) { _=RefreshAsync(); e.Handled=true; }
        },Avalonia.Interactivity.RoutingStrategies.Tunnel);
        LaunchFolder=directory=>TopLevel.GetTopLevel(this)?.Launcher.LaunchDirectoryInfoAsync(directory)??Task.FromResult(false);
        ChooseExportDestination=async entry=>
        {
            var top=TopLevel.GetTopLevel(this)??throw new InvalidOperationException();
            using var file=await top.StorageProvider.SaveFilePickerAsync(new() { Title=L.T("Zapisz kopię logu"),SuggestedFileName=entry.Name,DefaultExtension="log",FileTypeChoices=[new FilePickerFileType(L.T("Plik dziennika")) { Patterns=["*.log"] }] });
            if(file==null)return null;
            return file.TryGetLocalPath()??throw new NotSupportedException();
        };
        Loaded+=(_,_)=>_=RefreshAsync(); UpdateControls();
    }
    private Task Track(Func<Task> action)
    {
        var task=action(); pending.Add(task); _=Remove(); return task;
        async Task Remove() { try { await task; } finally { pending.Remove(task); } }
    }
    private void SetStatus(string text,bool error=false)
    { if(disposed)return; status.Text=text; DesignSystem.Resource(status,TextBlock.ForegroundProperty,error?"Danger":"ForegroundSecondary"); }
    private void UpdateControls()
    {
        if(disposed)return;
        files.IsEnabled=!refreshing&&!exporting; refresh.IsEnabled=!refreshing&&!exporting;
        export.IsEnabled=!refreshing&&!loading&&!exporting&&files.SelectedItem is ApplicationLogEntry;
        open.IsEnabled=folderExists&&!opening; search.IsEnabled=!loading&&preview.Text?.Length>0;
    }
    internal Task RefreshAsync()
    {
        if(disposed||refreshing||exporting)return Task.CompletedTask;
        return Track(async()=>
        {
            refreshing=true; reading?.Cancel(); revision++; loading=false; preview.Text=""; UpdateControls(); SetStatus(L.T("Wczytywanie logów…")); var keep=(files.SelectedItem as ApplicationLogEntry)?.Name; var success=false;
            try
            {
                var listing=await store.ListAsync(lifetime.Token); if(disposed)return;
                folderExists=listing.DirectoryExists; files.ItemsSource=listing.Entries; files.SelectedItem=listing.Entries.FirstOrDefault(e=>e.Name==keep)??listing.Entries.FirstOrDefault(); success=true;
                if(listing.Entries.Count==0)SetStatus(L.T("Brak plików dziennika. Możesz odświeżyć listę po wystąpieniu zdarzenia w aplikacji."));
            }
            catch(OperationCanceledException) when(disposed) { }
            catch(Exception ex) { Report("list",ex); files.ItemsSource=null; folderExists=false; SetStatus(L.T("Nie udało się odczytać listy logów. Sprawdź lokalny folder i uprawnienia, a następnie odśwież listę."),true); }
            finally { refreshing=false; UpdateControls(); }
            if(success && !disposed && files.SelectedItem!=null)await ReadSelectedAsync();
        });
    }
    private Task ReadSelectedAsync()=>Track(async()=>
    {
        reading?.Cancel(); var current=++revision; preview.Text="";
        if(disposed || files.SelectedItem is not ApplicationLogEntry entry) { loading=false; UpdateControls(); return; }
        using var source=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); reading=source; loading=true; UpdateControls(); SetStatus(L.T("Wczytywanie logów…"));
        try
        {
            var content=await store.ReadTailAsync(entry.Name,source.Token); if(disposed||current!=revision)return;
            preview.Text=content.Text; preview.CaretIndex=preview.Text.Length;
            Dispatcher.UIThread.Post(EnsureCaretVisible);
            SetStatus(content.Truncated?L.T("Podgląd ograniczony do końcówki pliku. Zapisz kopię, aby otrzymać pełny log."):content.Text.Length==0?L.T("Wybrany plik dziennika jest pusty."):L.F($"Wczytano plik: {entry.Name}"));
        }
        catch(OperationCanceledException) when(source.IsCancellationRequested) { }
        catch(Exception ex) { if(current==revision && !disposed) { Report("preview",ex); SetStatus(L.T("Nie udało się odczytać logu. Plik mógł zostać usunięty, obrócony lub zablokowany. Odśwież listę i spróbuj ponownie."),true); } }
        finally { if(ReferenceEquals(reading,source))reading=null; if(current==revision) { loading=false; UpdateControls(); } }
    });
    private async Task ExportAsync()
    {
        if(disposed||loading||refreshing||exporting||files.SelectedItem is not ApplicationLogEntry entry)return;
        exporting=true; UpdateControls();
        try
        {
            var destination=await ChooseExportDestination(entry); if(disposed||destination==null)return;
            SetStatus(L.T("Zapisywanie kopii logu…")); await store.ExportAsync(entry.Name,destination,lifetime.Token); SetStatus(L.T("Zapisano pełną kopię wybranego logu."));
        }
        catch(OperationCanceledException) when(disposed) { }
        catch(Exception ex) { Report("export",ex); SetStatus(L.T("Nie udało się zapisać kopii logu. Wybierz plik poza folderem logów i sprawdź uprawnienia oraz wolne miejsce. Istniejący plik docelowy nie został zastąpiony."),true); }
        finally { exporting=false; UpdateControls(); }
    }
    private async Task OpenFolderAsync()
    {
        if(disposed||opening||!folderExists)return; opening=true; UpdateControls();
        try { if(!await LaunchFolder(new(store.DirectoryPath)))SetStatus(L.T("Nie udało się otworzyć folderu logów. Skopiuj widoczną ścieżkę i otwórz ją w menedżerze plików."),true); }
        catch(Exception ex) { Report("folder",ex); SetStatus(L.T("Nie udało się otworzyć folderu logów. Skopiuj widoczną ścieżkę i otwórz ją w menedżerze plików."),true); }
        finally { opening=false; UpdateControls(); }
    }
    private void FindNext()
    {
        if(loading || string.IsNullOrEmpty(search.Text) || string.IsNullOrEmpty(preview.Text))return;
        var start=Math.Max(preview.SelectionStart,preview.SelectionEnd); var index=preview.Text.IndexOf(search.Text,Math.Min(start,preview.Text.Length),StringComparison.OrdinalIgnoreCase);
        if(index<0)index=preview.Text.IndexOf(search.Text,StringComparison.OrdinalIgnoreCase);
        if(index<0) { SetStatus(L.T("Nie znaleziono tekstu w bieżącym podglądzie.")); return; }
        preview.CaretIndex=index+search.Text.Length; preview.SelectionStart=index; preview.SelectionEnd=index+search.Text.Length; preview.Focus();
        Dispatcher.UIThread.Post(EnsureCaretVisible);
    }
    private void EnsureCaretVisible()
    {
        if(disposed || preview.Text?.Length is not >0)return;
        var presenter=preview.GetVisualDescendants().OfType<TextPresenter>().FirstOrDefault(); if(presenter==null)return;
        var caret=presenter.TextLayout.HitTestTextPosition(Math.Min(preview.CaretIndex,preview.Text.Length));
        presenter.BringIntoView(new Rect(caret.X,Math.Max(0,caret.Y-caret.Height),Math.Max(2,caret.Width),caret.Height*4));
    }
    private static void Report(string operation,Exception error)=>Serilog.Log.Warning("Log viewer {Operation} failed: {ExceptionDetails}",operation,ExceptionDiagnostics.Describe(error));
    public void Dispose() { if(disposed)return; disposed=true; revision++; lifetime.Cancel(); lifetime.Dispose(); }
}
