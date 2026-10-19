using ServerDesk.Core;
using ServerDesk.Infrastructure;
using Serilog;

namespace ServerDesk.App;

public sealed partial class MainWindow
{
    // The caller has confirmed this particular saved profile. No remote filesystem
    // operation, recursive directory cleanup or live-session teardown belongs here.
    internal async Task DeleteSavedProfileAsync(ServerProfile profile)
    {
        // Commit removal before touching the vault. A failed database DELETE
        // must not destroy the still-saved profile's password.
        try { await Task.Run(()=>repository.DeleteAsync(profile.Id,lifetime.Token)); }
        catch(Exception ex) when(ex is not OperationCanceledException)
        {
            Log.Error(ex,"Database {DatabasePath}: deletion failed for saved profile {ProfileId}",repository.DatabasePath,profile.Id);
            throw new InvalidOperationException(L.T("Nie udało się potwierdzić zapisu usunięcia połączenia w lokalnej bazie danych. Sprawdź uprawnienia, wolne miejsce i blokadę bazy. Odśwież listę i spróbuj ponownie. Szczegóły zapisano w dzienniku."),ex);
        }
        var cleanupFailed=false;
        try
        {
            foreach(var id in CredentialReference.AllForProfile(profile))await credentials.DeleteAsync(id,lifetime.Token);
        }
        catch(InvalidOperationException ex)
        {
            Log.Warning("Credential backend unavailable while deleting saved profile {ProfileId}: {ExceptionDetails}",profile.Id,ExceptionDiagnostics.Describe(ex));
            cleanupFailed=true;
        }
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException)
        {
            // This is a local File.Delete failure; include its path and full stack.
            // Generic/remote operations use redacted diagnostics instead.
            Log.Error(ex,"Credential file deletion failed for saved profile {ProfileId}",profile.Id);
            cleanupFailed=true;
        }
        catch(Exception ex)
        {
            cleanupFailed=true;
            Log.Warning("Profile deleted; credential cleanup not confirmed: {ExceptionDetails}",ExceptionDiagnostics.Describe(ex));
        }
        // DELETE has completed in SQLite autocommit before any UI refresh.
        Log.Information("Saved profile {ProfileId} deleted from the database",profile.Id);
        try { await vm.LoadAsync(); }
        catch(Exception ex)
        {
            Log.Error("Profile {ProfileId} was deleted, but list refresh failed: {ExceptionDetails}",profile.Id,ExceptionDiagnostics.Describe(ex));
            throw new InvalidOperationException(L.T("Połączenie zostało trwale usunięte, ale nie udało się odświeżyć listy. Uruchom ponownie aplikację. Pozostałe połączenia nie zostały usunięte."),ex);
        }
        vm.Status=L.T(cleanupFailed?"Połączenie zostało trwale usunięte. Nie potwierdzono usunięcia poświadczenia; może pozostać w magazynie systemowym. Szczegóły zapisano w logach.":"Usunięto zapisane połączenie.");
    }
}
