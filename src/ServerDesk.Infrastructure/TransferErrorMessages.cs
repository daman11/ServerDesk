using System.Net.Sockets;
using Renci.SshNet.Common;
using Renci.SshNet.Messages.Transport;
using Renci.SshNet.Sftp;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public static class TransferErrorMessages
{
    public static string Describe(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var causes=new List<Exception>();
        for(var cause=error;cause!=null && causes.Count<8;cause=cause.InnerException)causes.Add(cause);
        // Application validation and host-key warnings contain actionable details
        // (including fingerprints). Do not hide them behind a transport wrapper.
        if(causes.FirstOrDefault(e=>e is InvalidOperationException and not ObjectDisposedException) is {} validation)return validation.Message;
        if(causes.Any(e=>e is SshConnectionException { DisconnectReason:DisconnectReason.HostKeyNotVerifiable }))
            return L.T("Nie potwierdzono tożsamości serwera SSH. Sprawdź klucz hosta w ustawieniach bezpieczeństwa przed ponowieniem.");
        if(causes.Any(e=>e is SshAuthenticationException))
            return L.T("Serwer odrzucił logowanie SFTP. Sprawdź poświadczenia i połącz ponownie sesję SSH przed ponowieniem.");
        if(causes.Any(e=>e is SftpPermissionDeniedException or UnauthorizedAccessException or SftpException { StatusCode:StatusCode.PermissionDenied }))
            return L.T("Brak uprawnień do odczytu lub zapisu pliku. Sprawdź uprawnienia pliku i katalogu docelowego, a następnie ponów zadanie.");
        if(causes.Any(e=>e is SftpPathNotFoundException or FileNotFoundException or DirectoryNotFoundException or SftpException { StatusCode:StatusCode.NoSuchFile }))
            return L.T("Plik lub katalog jest niedostępny. Odśwież listę i sprawdź ścieżkę przed ponowieniem zadania.");
        if(causes.Any(e=>e is IOException && e.HResult is unchecked((int)0x80070070) or unchecked((int)0x80070027)))
            return L.T("Brak miejsca na lokalnym dysku. Zwolnij miejsce i ponów zadanie. Nieukończony plik nie zastępuje pliku docelowego.");
        if(causes.Any(e=>e is SshOperationTimeoutException or TimeoutException or SocketException { SocketErrorCode:SocketError.TimedOut }))
            return L.T("Przekroczono czas oczekiwania na odpowiedź SFTP. Sprawdź sieć i dostępność serwera. Ponawiany plik jest przesyłany od początku.");
        if(causes.Any(e=>e is SocketException { SocketErrorCode:SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData }))
            return L.T("Nie udało się rozwiązać nazwy serwera. Sprawdź adres i ustawienia DNS przed ponowieniem.");
        if(causes.Any(e=>e is SocketException { SocketErrorCode:SocketError.ConnectionRefused }))
            return L.T("Serwer nie przyjmuje połączenia SFTP. Sprawdź port SSH i działanie usługi, a następnie ponów zadanie.");
        if(causes.Any(e=>e is SshConnectionException { DisconnectReason:DisconnectReason.KeyExchangeFailed }))
            return L.T("Nie udało się uzgodnić bezpiecznego połączenia SSH. Sprawdź zgodność ustawień serwera i jego tożsamość.");
        if(causes.Any(e=>e is SshConnectionException or SocketException or ObjectDisposedException or SftpException { StatusCode:StatusCode.NoConnection or StatusCode.ConnectionLost }))
            return L.T("Połączenie SFTP zostało przerwane. Sprawdź sieć i ponów zadanie. Jeśli terminal także jest rozłączony, najpierw połącz sesję SSH.");
        if(causes.Any(e=>e is SftpException { StatusCode:StatusCode.OperationUnsupported }))
            return L.T("Serwer nie obsługuje wymaganej operacji SFTP. Sprawdź możliwości serwera i wybierz obsługiwany sposób wykonania zadania. Samo ponowienie może nie pomóc.");
        if(causes.Any(e=>e is SftpException))
            return L.T("Serwer SFTP odrzucił operację. Sprawdź uprawnienia, ścieżkę i wolne miejsce na serwerze przed ponowieniem. Serwer nie podał jednoznacznej przyczyny.");
        // Keep the application's specific integrity/overwrite/link protections.
        if(error is IOException or InvalidDataException or ArgumentException)return error.Message;
        return L.T("Nie udało się ukończyć transferu. Sprawdź ścieżki, połączenie i uprawnienia przed ponowieniem.")+" ("+error.GetType().Name+")";
    }
}
