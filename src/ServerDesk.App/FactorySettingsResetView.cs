using Avalonia.Controls;
using Avalonia.Layout;
using ServerDesk.Infrastructure;
using Serilog;

namespace ServerDesk.App;

// Reset requests are independent of the unsaved preferences draft. They are
// reversible until the next startup and never operate on credential stores.
internal sealed class FactorySettingsResetView:StackPanel,IDisposable
{
    private readonly ProfileRepository repository;
    private readonly Func<Window?> owner;
    private readonly Func<DatabaseBackup,Task> backedUp;
    private readonly CancellationTokenSource lifetime=new();
    private readonly Button schedule,cancel,restart;
    private readonly TextBlock status=DesignSystem.Hint("");
    private bool pending,disposed;
    private Task operation=Task.CompletedTask;
    internal bool IsBusy { get; private set; }
    internal event Action? BusyChanged;
    internal FactorySettingsResetView(ProfileRepository repository,Func<Window?> owner,bool pending,Action? restartAction,Func<DatabaseBackup,Task> backedUp)
    {
        this.repository=repository; this.owner=owner; this.pending=pending; this.backedUp=backedUp;
        Name="FactorySettingsReset"; Spacing=10;
        Children.Add(DesignSystem.Hint(L.T("Ustawienia domyślne zmieniają tylko formularz. Zastosuj je przyciskiem Zapisz; Anuluj przywraca poprzednie preferencje.")));
        Children.Add(DesignSystem.Hint(L.T("Ustawienia fabryczne przywracają preferencje, język, układ paneli, pozycję okna i sortowanie przy następnym uruchomieniu. Połączenia, hasła, historia, zaufane klucze SSH, kolory tagów i dane pluginów pozostają zachowane.")));
        Children.Add(DesignSystem.Hint(L.T("Przed zaplanowaniem resetu powstaje sprawdzona kopia bazy. Zaplanowanie i anulowanie resetu działają niezależnie od Zapisz i Anuluj w ustawieniach. Zamknij pozostałe instancje ServerDesk przed ponownym uruchomieniem.")));
        schedule=Dialogs.Button("Przywróć ustawienia fabryczne",()=>Start(ScheduleAsync)); schedule.Name="ScheduleFactorySettingsReset";
        cancel=Dialogs.Button("Anuluj zaplanowany reset",()=>Start(CancelAsync)); cancel.Name="CancelFactorySettingsReset";
        restart=Dialogs.Button("Uruchom ponownie",()=> { if(!IsBusy && pending)restartAction?.Invoke(); }); restart.Name="RestartFactorySettingsReset";
        status.Name="FactorySettingsResetStatus";
        var actions=new WrapPanel { ItemSpacing=8,LineSpacing=8,Children={schedule,cancel} };
        if(restartAction!=null)actions.Children.Add(restart);
        Children.Add(actions); Children.Add(status); Refresh();
    }
    private void Refresh()
    {
        schedule.IsEnabled=!pending && !IsBusy; cancel.IsEnabled=restart.IsEnabled=!IsBusy;
        cancel.IsVisible=restart.IsVisible=pending;
        status.Text=pending?L.T("Reset ustawień fabrycznych jest zaplanowany na następne uruchomienie. Do tego czasu możesz go anulować."):L.T("Brak zaplanowanego resetu ustawień.");
    }
    private void Start(Func<Task> action)
    {
        if(IsBusy || disposed)return;
        IsBusy=true; Refresh(); BusyChanged?.Invoke(); operation=RunAsync(action);
    }
    private async Task RunAsync(Func<Task> action)
    {
        try { await action(); }
        catch(OperationCanceledException) when(lifetime.IsCancellationRequested) { }
        catch(Exception ex)
        {
            Log.Error("Factory settings reset request failed: {ExceptionDetails}",ExceptionDiagnostics.Describe(ex));
            if(!disposed)status.Text=L.T("Nie udało się zmienić planu resetu. Sprawdź wolne miejsce, uprawnienia i logi aplikacji. Żadne ustawienia nie zostały przywrócone.");
        }
        finally { IsBusy=false; if(!disposed) { schedule.IsEnabled=!pending; cancel.IsEnabled=restart.IsEnabled=true; BusyChanged?.Invoke(); } }
    }
    private async Task ScheduleAsync()
    {
        if(owner() is not {} window)return;
        var confirmed=await Dialogs.Confirm(window,L.T("Przywrócić ustawienia fabryczne?"),L.T("Po następnym uruchomieniu ServerDesk przywróci domyślne preferencje i zapomni układ okien oraz kart. Zapisane połączenia i dane użytkownika pozostaną zachowane. Najpierw utworzymy kopię bazy; jeśli kopia się nie powiedzie, reset nie zostanie zaplanowany."));
        if(!confirmed || disposed)return;
        status.Text=L.T("Tworzenie kopii bazy i planowanie resetu…");
        var copy=await repository.ScheduleFactorySettingsResetAsync(lifetime.Token); pending=true;
        Log.Information("Factory settings reset scheduled; user data preserved, database backup created");
        if(disposed)return;
        Refresh(); status.Text+="\n"+L.T("Kopia bazy:")+" "+System.IO.Path.GetFileName(copy.Path);
        if(window.Owner?.DataContext is MainViewModel model)model.Status=L.T("Reset ustawień fabrycznych jest zaplanowany na następne uruchomienie. Do tego czasu możesz go anulować.");
        try { await backedUp(copy); }
        catch(Exception ex) { Log.Warning("Reset backup catalog refresh failed: {ExceptionDetails}",ExceptionDiagnostics.Describe(ex)); }
    }
    private async Task CancelAsync()
    {
        await repository.CancelFactorySettingsResetAsync(lifetime.Token); pending=false;
        Log.Information("Pending factory settings reset cancelled");
        if(!disposed) { Refresh(); if(owner()?.Owner?.DataContext is MainViewModel model)model.Status=L.T("Zaplanowany reset został anulowany. Kopia bazy pozostaje zachowana."); }
    }
    public void Dispose() { if(disposed)return; disposed=true; lifetime.Cancel(); }
    internal async Task ShutdownAsync() { try { await operation; } finally { lifetime.Dispose(); } }
}
