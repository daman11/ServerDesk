using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using ServerDesk.Core;
using ServerDesk.Infrastructure;
using System.Diagnostics;
using System.Net;

namespace ServerDesk.App;

internal static partial class Dialogs
{
    internal static async Task<PreparedApplicationUpdate?> ApplicationUpdates(Window owner,ProfileRepository repository,ApplicationUpdate? available=null,GitHubUpdateClient? updateClient=null,string? installation=null)
    {
        using var client=updateClient??new GitHubUpdateClient(); using var lifetime=new CancellationTokenSource();
        CancellationTokenSource? work=null; PreparedApplicationUpdate? prepared=null; var current=typeof(App).Assembly.GetName().Version!.ToString(3);
        var body=new StackPanel { Spacing=16,Margin=new Thickness(0,0,12,0) };
        var heading=DesignSystem.PageHeading("download",L.T("Aktualizacje ServerDesk"),L.F($"Zainstalowana wersja: {current}"));
        var status=DesignSystem.Label("",false,16); status.Name="UpdateStatus"; status.TextWrapping=Avalonia.Media.TextWrapping.Wrap;
        var details=DesignSystem.Hint(""); details.Name="UpdateDetails";
        body.Children.Add(DesignSystem.Card(new StackPanel { Spacing=8,Children={status,details} }));
        var progress=new ProgressBar { Name="UpdateProgress",IsVisible=false,Minimum=0,Maximum=100,Height=6 }; body.Children.Add(progress);
        body.Children.Add(DesignSystem.Hint(L.T("Źródło: github.com/daman11/ServerDesk. GitHub otrzyma adres IP i informacje wymagane do pobrania. Profile i hasła nie są wysyłane. Instalator jest sprawdzany za pomocą SHA-256 z API GitHuba; nie ma podpisu Authenticode.")));
        installation??=AppContext.BaseDirectory;
        var installed=ApplicationUpdateInstaller.IsSupported(installation);
        if(!installed)body.Children.Add(DesignSystem.Hint(L.T("Instalacja z programu jest dostępna dla wersji Windows x64 z instalatora. Wersję przenośną i Linux zaktualizuj ręcznie z GitHub Releases.")));
        var actions=new WrapPanel { Orientation=Orientation.Horizontal };
        var check=Button("Sprawdź aktualizacje",()=>{}); check.Name="CheckUpdates";
        var download=Button("Pobierz aktualizację",()=>{}); download.Name="DownloadUpdate";
        var install=Button("Zainstaluj i uruchom ponownie",()=>{}); install.Name="InstallUpdate"; install.Classes.Add("primary");
        var cancel=Button("Anuluj pobieranie",()=>work?.Cancel()); cancel.Name="CancelUpdate";
        var releases=Button("Wydania na GitHub",()=>{});
        var close=Button("Zamknij",()=>{}); close.IsCancel=true;
        foreach(var button in new[]{check,download,install,cancel,releases,close}) { button.Margin=new Thickness(0,0,8,8); actions.Children.Add(button); }
        var viewport=new ScrollViewer { Content=body,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var shell=new Grid { RowDefinitions=new("Auto,*,Auto"),RowSpacing=16,Children={heading,viewport,actions} }; Grid.SetRow(viewport,1); Grid.SetRow(actions,2);
        var dialog=Window(L.T("Aktualizacje ServerDesk"),shell);
        dialog.Name="ApplicationUpdatesDialog"; dialog.Width=650; dialog.Height=500; dialog.MinWidth=430; dialog.MinHeight=350; dialog.SizeToContent=SizeToContent.Manual;
        var busy=false; string? downloaded=null;
        void Refresh()
        {
            check.IsEnabled=!busy; download.IsVisible=available!=null && downloaded==null && installed; download.IsEnabled=!busy;
            install.IsVisible=downloaded!=null && installed; install.IsEnabled=!busy; cancel.IsVisible=busy;
            if(!busy)progress.IsVisible=false;
        }
        void ShowAvailable()
        {
            status.Text=available==null?L.T("Sprawdź dostępność nowej wersji."):L.F($"Dostępna wersja: {available.Version}");
            details.Text=available==null?"":L.F($"Rozmiar instalatora: {Math.Ceiling(available.Size/1048576d)} MB"); Refresh();
        }
        async Task Run(Func<CancellationToken,Task> action)
        {
            if(busy)return; busy=true; work=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); work.CancelAfter(TimeSpan.FromMinutes(10)); Refresh();
            try { await action(work.Token); }
            catch(OperationCanceledException) { status.Text=L.T("Operacja anulowana. Możesz spróbować ponownie."); }
            catch(Exception ex)
            {
                var id=ApplicationDiagnostics.Report("ApplicationUpdate",ex);
                status.Text=ex is HttpRequestException { StatusCode:HttpStatusCode.NotFound }?L.T("Repozytorium nie ma jeszcze opublikowanych wydań. Spróbuj ponownie później."):L.T("Nie udało się sprawdzić lub pobrać aktualizacji. Sprawdź sieć i logi aplikacji.");
                details.Text=L.F($"Identyfikator błędu: {id}");
            }
            finally { busy=false; work.Dispose(); work=null; Refresh(); }
        }
        check.Click+=async(_,_)=>await Run(async ct=>
        {
            status.Text=L.T("Sprawdzanie aktualizacji…");
            available=await client.CheckAsync(ApplicationUpdate.ParseVersion(current),ct); downloaded=null; ShowAvailable();
            if(available==null)status.Text=L.T("Używasz najnowszej dostępnej wersji.");
        });
        releases.Click+=async(_,_)=>await Run(_=> { Process.Start(new ProcessStartInfo(ApplicationUpdate.ReleasesUrl) { UseShellExecute=true })?.Dispose(); return Task.CompletedTask; });
        download.Click+=async(_,_)=>await Run(async ct=>
        {
            if(available==null)return; var package=available;
            status.Text=L.T("Pobieranie i sprawdzanie instalatora…"); progress.IsVisible=true; progress.Value=0;
            var directory=Path.Combine(new AppPaths().Data,"updates",Guid.NewGuid().ToString("N"));
            downloaded=await client.DownloadAsync(package,directory,new Progress<long>(bytes=>progress.Value=100d*bytes/package.Size),ct);
            status.Text=L.T("Instalator pobrany. Weryfikacja SHA-256 zakończona."); details.Text=L.T("Przed instalacją powstanie kopia bazy danych. Sesje zostaną zamknięte po potwierdzeniu.");
        });
        install.Click+=async(_,_)=>await Run(async ct=>
        {
            if(available==null || downloaded==null)return;
            status.Text=L.T("Przygotowanie aktualizacji i kopii bazy…");
            var candidate=new PreparedApplicationUpdate(Path.GetDirectoryName(downloaded)!,available);
            await repository.CreateDatabaseBackupAsync(ct);
            await ApplicationUpdateInstaller.PrepareAsync(candidate,installation,current,ct);
            prepared=candidate; dialog.Close();
        });
        close.Click+=(_,_)=>dialog.Close();
        dialog.Closing+=(_,_)=>lifetime.Cancel();
        ShowAvailable();
        await dialog.ShowDialog(owner);
        // Event callbacks observe cancellation before the HTTP client is disposed.
        while(busy)await Task.Delay(10);
        return prepared;
    }
}
