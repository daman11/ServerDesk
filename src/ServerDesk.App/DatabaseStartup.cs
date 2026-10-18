using Microsoft.Data.Sqlite;
using Serilog;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    private async Task<bool> InitializeDatabaseForStartupAsync()
    {
        try { await repository.InitializeAsync(lifetime.Token); if(await repository.ApplyPendingFactorySettingsResetAsync(lifetime.Token))Log.Information("Factory settings reset applied; saved connections and user data preserved"); return true; }
        catch(Exception ex) when(ex is SqliteException or IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Log.Error("Database initialization failed: {Type}",ex.GetType().Name);
            var reason=ex switch
            {
                ServerDesk.Infrastructure.DatabaseBackupException=>ex.Message,
                InvalidDataException=>ex.Message,
                SqliteException { SqliteErrorCode:11 or 26 }=>L.T("Nie można odczytać bazy danych. Plik może być uszkodzony lub mieć nieprawidłowy format."),
                SqliteException { SqliteErrorCode:5 or 6 }=>L.T("Baza danych jest zajęta. Zamknij inne instancje ServerDesk i spróbuj ponownie."),
                _=>L.T("Nie można otworzyć lub zapisać bazy danych. Sprawdź wolne miejsce i uprawnienia katalogu danych.")
            };
            await Dialogs.Message(this,L.T("Nie można uruchomić ServerDesk"),reason+"\n\n"+
                L.T("Oryginalne dane nie zostaną usunięte ani zastąpione pustą bazą. Po zamknięciu aplikacji możesz zabezpieczyć katalog danych i przywrócić sprawdzoną kopię. Instrukcja: docs/database-backups.md.")+"\n\n"+
                L.T("Katalog kopii bazy:")+"\n"+repository.DatabaseBackupDirectory);
            // Do not run the ordinary close path: it writes workspace/preferences.
            closing=true; readyToClose=true; statusTimer.Stop(); lifetime.Cancel(); Close();
            return false;
        }
    }
}
