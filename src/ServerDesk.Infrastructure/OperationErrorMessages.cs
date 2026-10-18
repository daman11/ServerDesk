using Microsoft.Data.Sqlite;
using Renci.SshNet.Common;
using ServerDesk.Core;
using System.Net.Sockets;

namespace ServerDesk.Infrastructure;

public static class OperationErrorMessages
{
    public static string Describe(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if(error is SqliteException)return L.T("Operacja na lokalnej bazie danych nie powiodła się. Sprawdź uprawnienia, wolne miejsce i blokadę bazy. Szczegóły zapisano w dzienniku aplikacji.");
        if(SshErrorMessages.Describe(error) is {} ssh)return ssh;
        if(error is SshException)return L.T("Operacja SSH/SFTP nie powiodła się. Sprawdź stan sesji i dostępność serwera.");
        if(error is SocketException or TimeoutException)return L.T("Nie udało się ukończyć operacji sieciowej. Sprawdź dostępność serwera i połączenie z siecią.");
        // Keep application validation and missing external-tool guidance actionable.
        if(error is ArgumentException or InvalidDataException or InvalidOperationException or NotSupportedException or FileNotFoundException { FileName:null })return error.Message;
        if(error is DirectoryNotFoundException)return L.T("Nie znaleziono lokalnego katalogu. Sprawdź ścieżkę danych na komputerze. Szczegóły zapisano w dzienniku aplikacji.");
        if(error is FileNotFoundException)return L.T("Nie znaleziono lokalnego pliku. Sprawdź ścieżkę na komputerze. Szczegóły zapisano w dzienniku aplikacji.");
        if(error is UnauthorizedAccessException)return L.T("Brak uprawnień do lokalnego pliku lub katalogu. Sprawdź uprawnienia aplikacji. Szczegóły zapisano w dzienniku aplikacji.");
        if(error is IOException)return L.T("Błąd lokalnego systemu plików. Sprawdź ścieżkę, wolne miejsce i blokadę pliku. Szczegóły zapisano w dzienniku aplikacji.");
        return L.F($"Operacja nie powiodła się ({error.GetType().Name}). Szczegóły zapisano w dzienniku aplikacji.");
    }
}
