using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using ServerDesk.Core;
using ServerDesk.Infrastructure;

namespace ServerDesk.App;

internal sealed class DatabaseBackupBrowser:StackPanel,IDisposable
{
    private readonly string directory;
    private readonly CancellationTokenSource lifetime=new();
    private readonly ListBox list=new() { Name="DatabaseBackupList",MaxHeight=190,HorizontalAlignment=HorizontalAlignment.Stretch,IsVisible=false };
    private readonly TextBlock status=DesignSystem.Hint(""),selectionLabel=DesignSystem.Hint("");
    private readonly SelectableTextBlock path=new() { Name="DatabaseBackupPath",TextWrapping=TextWrapping.Wrap };
    private readonly TextBlock verification=DesignSystem.Hint("");
    private readonly Button copy,open,refresh,verify;
    private bool disposed,busy,folderExists,opening,checking;
    private Task loading=Task.CompletedTask,verifying=Task.CompletedTask;
    internal Task Shutdown=>Task.WhenAll(loading,verifying);
    internal Func<DirectoryInfo,Task<bool>> LaunchFolder { get; set; }
    internal DatabaseBackupBrowser(string directory)
    {
        this.directory=System.IO.Path.GetFullPath(directory); Name="DatabaseBackupBrowser"; Spacing=8;
        status.Name="DatabaseBackupListStatus"; AutomationProperties.SetName(list,L.T("Zapisane kopie bazy"));
        list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty,Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled);
        list.Styles.Add(new Style(s=>s.OfType<ListBoxItem>()) { Setters={new Setter(ContentControl.HorizontalContentAlignmentProperty,HorizontalAlignment.Stretch)} });
        DesignSystem.Resource(path,SelectableTextBlock.ForegroundProperty,"ForegroundPrimary"); DesignSystem.Resource(path,SelectableTextBlock.FontSizeProperty,"FontCaption");
        list.ItemTemplate=new FuncDataTemplate<DatabaseBackupEntry>((entry,_)=>
        {
            if(entry==null)return null;
            var title=DesignSystem.Label(entry.CreatedUtc.ToLocalTime().ToString("g")); title.FontWeight=FontWeight.SemiBold;
            var kind=L.T(entry.BeforeMigration?"Przed migracją":"Ręczna kopia");
            var info=DesignSystem.Hint(kind+" · "+L.F($"Schemat {entry.SchemaVersion}")+" · "+(entry.Bytes/1024d).ToString("N1")+" KiB");
            var name=DesignSystem.Hint(System.IO.Path.GetFileName(entry.Path)); name.TextWrapping=TextWrapping.NoWrap; name.TextTrimming=TextTrimming.CharacterEllipsis;
            var row=new StackPanel { Spacing=3,Margin=new Thickness(4,5),Children={title,info,name} }; ToolTip.SetTip(row,entry.Path); return row;
        });
        list.SelectionChanged+=(_,_)=> { if(!checking)verification.IsVisible=false; UpdateSelection(); };
        copy=Dialogs.Button("Kopiuj ścieżkę",async()=>await CopyAsync()); copy.Name="DatabaseBackupCopyPath";
        open=Dialogs.Button("Otwórz folder kopii",async()=>await OpenAsync()); open.Name="DatabaseBackupOpenFolder";
        refresh=DeskIcons.Button("refresh","Odśwież listę kopii",()=>_=RefreshAsync()); refresh.Name="DatabaseBackupRefresh";
        verify=Dialogs.Button("Sprawdź kopię",()=>_=VerifySelectedAsync()); verify.Name="DatabaseBackupVerify";
        verification.Name="DatabaseBackupVerificationStatus"; verification.IsVisible=false;
        var heading=new Grid { ColumnDefinitions=new("*,Auto"),ColumnSpacing=8,Children={DesignSystem.SectionHeading("archive",L.T("Zapisane kopie bazy")),refresh} }; Grid.SetColumn(refresh,1);
        var actions=new WrapPanel { Orientation=Orientation.Horizontal,Children={verify,copy,open} }; foreach(var action in actions.Children)action.Margin=new Thickness(0,2,8,2);
        Children.Add(heading); Children.Add(status); Children.Add(list); Children.Add(selectionLabel); Children.Add(path); Children.Add(actions); Children.Add(verification);
        Children.Add(DesignSystem.Hint(L.T("Lista pokazuje metadane. Sprawdź kopię wykonuje kontrolę SQLite, bez przywracania danych ani sprawdzania haseł i kolekcji w magazynie systemowym. Przed przywróceniem przetestuj kopię w osobnym katalogu danych.")));
        LaunchFolder=folder=>TopLevel.GetTopLevel(this)?.Launcher.LaunchDirectoryInfoAsync(folder)??Task.FromResult(false);
        Loaded+=(_,_)=>_=RefreshAsync(); UpdateSelection();
    }
    internal Task RefreshAsync(string? preferred=null)
    {
        if(disposed)return Task.CompletedTask;
        if(checking)return AfterVerification();
        if(busy)return preferred==null?loading:AfterCurrent();
        return loading=Read();
        async Task AfterCurrent() { await loading; if(!disposed)await RefreshAsync(preferred); }
        async Task AfterVerification() { await verifying; if(!disposed)await RefreshAsync(preferred); }
        async Task Read()
        {
            busy=true; verification.IsVisible=false; UpdateSelection(); refresh.IsEnabled=false; status.Text=L.T("Wczytywanie kopii bazy…"); var keep=preferred??(list.SelectedItem as DatabaseBackupEntry)?.Path;
            try
            {
                var result=await DatabaseBackupCatalog.ReadAsync(directory,lifetime.Token); if(disposed)return;
                folderExists=result.DirectoryExists; list.ItemsSource=result.Entries;
                list.SelectedItem=result.Entries.FirstOrDefault(e=>e.Path==keep)??result.Entries.FirstOrDefault(); list.IsVisible=result.Entries.Count>0;
                status.Text=result.TotalCount==0?L.T("Brak zapisanych kopii bazy."):L.F($"Najnowsze kopie: {result.Entries.Count} z {result.TotalCount}.");
            }
            catch(OperationCanceledException) when(disposed) { }
            catch(Exception ex)
            {
                if(disposed)return; Serilog.Log.Warning("Backup listing failed: {Type}",ex.GetType().Name); folderExists=false; list.ItemsSource=null; list.IsVisible=false;
                status.Text=L.T("Nie udało się odczytać listy kopii. Sprawdź folder i uprawnienia, a następnie odśwież listę.");
            }
            finally { busy=false; if(!disposed) { refresh.IsEnabled=true; UpdateSelection(); } }
        }
    }
    private void UpdateSelection()
    {
        var selected=list.SelectedItem as DatabaseBackupEntry; path.Text=selected?.Path??directory;
        selectionLabel.Text=L.T(selected==null?"Folder kopii":"Wybrana kopia");
        copy.IsEnabled=!busy&&!checking&&!disposed; open.IsEnabled=!busy&&!checking&&!disposed&&!opening&&folderExists;
        verify.IsEnabled=!busy&&!checking&&!disposed&&selected!=null; list.IsEnabled=!busy&&!checking&&!disposed;
    }
    internal Task VerifySelectedAsync()
    {
        if(disposed || busy)return Task.CompletedTask;
        if(checking)return verifying;
        return list.SelectedItem is DatabaseBackupEntry entry?verifying=CheckAsync(entry):Task.CompletedTask;
    }
    private async Task CheckAsync(DatabaseBackupEntry entry)
    {
        checking=true; UpdateSelection(); refresh.IsEnabled=false; verification.IsVisible=true;
        verification.Text=L.T("Sprawdzanie wybranej kopii…"); DesignSystem.Resource(verification,TextBlock.ForegroundProperty,"ForegroundSecondary");
        try
        {
            var schema=await DatabaseBackupCatalog.VerifyAsync(entry,lifetime.Token); if(disposed)return;
            verification.Text=L.F($"Kontrola SQLite poprawna · schemat {schema}. Przed przywróceniem sprawdź dane w osobnym katalogu."); DesignSystem.Resource(verification,TextBlock.ForegroundProperty,"Success");
        }
        catch(OperationCanceledException) when(disposed) { }
        catch(Exception ex)
        {
            if(disposed)return; Serilog.Log.Warning("Backup verification failed: {Type}",ex.GetType().Name);
            verification.Text=ex is InvalidDataException?ex.Message:L.T("Nie udało się sprawdzić kopii. Plik mógł zostać usunięty, zablokowany lub jest niedostępny. Odśwież listę i spróbuj ponownie."); DesignSystem.Resource(verification,TextBlock.ForegroundProperty,"Warning");
        }
        finally { checking=false; if(!disposed) { refresh.IsEnabled=true; UpdateSelection(); } }
    }
    private async Task CopyAsync()
    {
        if(busy||checking||disposed)return;
        try { var clipboard=TopLevel.GetTopLevel(this)?.Clipboard??throw new InvalidOperationException(); await clipboard.SetTextAsync(path.Text??directory); if(!disposed)status.Text=L.T("Skopiowano ścieżkę."); }
        catch(Exception) { if(!disposed)status.Text=L.T("Nie udało się skopiować ścieżki do schowka."); }
    }
    private async Task OpenAsync()
    {
        if(busy||checking||disposed||opening||!folderExists)return; opening=true; UpdateSelection();
        try { if(!await LaunchFolder(new(directory)) && !disposed)status.Text=L.T("Nie udało się otworzyć folderu kopii. Skopiuj ścieżkę i otwórz ją w menedżerze plików."); }
        catch(Exception) { if(!disposed)status.Text=L.T("Nie udało się otworzyć folderu kopii. Skopiuj ścieżkę i otwórz ją w menedżerze plików."); }
        finally { opening=false; if(!disposed)UpdateSelection(); }
    }
    public void Dispose() { if(disposed)return; disposed=true; lifetime.Cancel(); lifetime.Dispose(); }
}
