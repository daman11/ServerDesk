using System.Net.Sockets;
using Renci.SshNet.Common;
using Renci.SshNet.Messages.Transport;
using ServerDesk.Core;

namespace ServerDesk.Infrastructure;

public sealed class SshLoginException: SshAuthenticationException
{
    public Authentication Method { get; }
    public IReadOnlyList<string> OfferedMethods { get; }
    public SshLoginException(Authentication method,IEnumerable<string> offeredMethods,SshAuthenticationException inner):base("SSH authentication failed.",inner)
    {
        Method=method;
        OfferedMethods=Array.AsReadOnly(offeredMethods.Where(m=>m is "password" or "publickey" or "keyboard-interactive" or "hostbased" or "gssapi-with-mic").Distinct(StringComparer.Ordinal).Take(5).ToArray());
    }
}
public enum SshPrivateKeyFailure { Missing,AccessDenied,PassphraseRequired,InvalidOrPassphrase,ReadFailure }
public sealed class SshPrivateKeyException(SshPrivateKeyFailure reason,Exception inner):IOException("Cannot load the SSH private key.",inner)
{
    public SshPrivateKeyFailure Reason { get; }=reason;
}

public static class SshErrorMessages
{
    // Only known codes and application-owned text are displayed. Remote exception
    // messages may contain secrets, escape sequences or misleading account claims.
    public static string? Describe(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        var causes=new List<Exception>();
        for(var cause=error;cause!=null && causes.Count<8;cause=cause.InnerException)causes.Add(cause);
        if(causes.OfType<SshPrivateKeyException>().FirstOrDefault() is {} key)return key.Reason switch
        {
            SshPrivateKeyFailure.Missing=>L.T("Nie znaleziono pliku klucza prywatnego SSH. Sprawdź ścieżkę klucza w ustawieniach połączenia."),
            SshPrivateKeyFailure.AccessDenied=>L.T("Brak uprawnień do odczytu klucza prywatnego SSH. Sprawdź dostęp aplikacji do wybranego pliku."),
            SshPrivateKeyFailure.PassphraseRequired=>L.T("Klucz prywatny SSH jest zaszyfrowany i wymaga hasła klucza. Podaj hasło klucza, a nie hasło konta na serwerze."),
            SshPrivateKeyFailure.InvalidOrPassphrase=>L.T("Nie udało się odczytać klucza prywatnego SSH. Plik może być uszkodzony, mieć nieobsługiwany format albo podano niepoprawne hasło klucza. Serwer nie odrzucił jeszcze logowania — błąd wystąpił lokalnie."),
            _=>L.T("Nie udało się odczytać pliku klucza prywatnego SSH. Sprawdź ścieżkę, dostępność i blokadę pliku na komputerze.")
        };
        if(causes.Any(e=>e is SshPassPhraseNullOrEmptyException))return L.T("Klucz prywatny SSH jest zaszyfrowany i wymaga hasła klucza. Podaj hasło klucza, a nie hasło konta na serwerze.");
        if(causes.Any(e=>e is SshAuthenticationException))
        {
            var login=causes.OfType<SshLoginException>().FirstOrDefault();
            var heading=login?.Method switch {
                Authentication.Password=>L.T("Serwer SSH odrzucił logowanie hasłem."),
                Authentication.PrivateKey=>L.T("Serwer SSH odrzucił logowanie kluczem prywatnym."),
                _=>L.T("Serwer SSH odrzucił uwierzytelnianie.") };
            var hint=login?.Method==Authentication.PrivateKey?
                L.T("Sprawdź login, wybrany klucz, wpis klucza publicznego na serwerze oraz uprawnienia i zasady dostępu konta."):
                L.T("Sprawdź login i hasło oraz czy konto i logowanie wybraną metodą są dozwolone na serwerze.");
            var methods=login is { OfferedMethods.Count:>0 }?"\n"+L.T("Metody wskazane przez serwer:")+" "+string.Join(", ",login.OfferedMethods):"";
            return heading+"\n\n"+hint+methods+"\n\n"+L.T("SSH nie podał dokładnej przyczyny odmowy. Ten komunikat nie rozstrzyga, czy poświadczenia są błędne, konto jest zablokowane czy obowiązuje ograniczenie dostępu. Dokładny powód należy sprawdzić w logach SSH serwera.");
        }
        if(causes.OfType<SocketException>().FirstOrDefault() is {} socket)
        {
        var network=socket.SocketErrorCode switch {
            SocketError.ConnectionRefused=>L.T("Odmowa połączenia z portem serwera. Usługa może nie działać, port może być błędny albo zapora aktywnie odrzuca połączenie. Logowanie nie zostało rozpoczęte."),
            SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain=>L.T("Nie udało się ustalić adresu serwera przez DNS. Sprawdź nazwę hosta, DNS i połączenie z VPN."),
            SocketError.HostUnreachable or SocketError.NetworkUnreachable or SocketError.NetworkDown=>L.T("Brak dostępnej trasy do serwera. Sprawdź sieć, routing i połączenie z VPN."),
            SocketError.TimedOut=>L.T("Serwer nie odpowiedział w wyznaczonym czasie. Możliwe przyczyny to niedostępny serwer, filtrowanie portu przez zaporę lub problem sieci/VPN."),
            SocketError.ConnectionReset or SocketError.ConnectionAborted or SocketError.Shutdown=>L.T("Połączenie z serwerem zostało przerwane. Sprawdź działanie usługi i sieci; ten błąd nie określa poprawności hasła."),
            SocketError.AccessDenied=>L.T("System odmówił aplikacji dostępu do połączenia sieciowego. Sprawdź lokalną zaporę i zasady bezpieczeństwa."),
            _=>null
        };
        if(network!=null)return network;
        }
        if(causes.Any(e=>e is SshOperationTimeoutException || e is TimeoutException && causes.Any(c=>c is SshException)))return L.T("Przekroczono czas oczekiwania na odpowiedź SSH. Sprawdź adres, port, sieć/VPN i obciążenie serwera. Nie potwierdza to błędnego hasła.");
        if(causes.OfType<SshConnectionException>().FirstOrDefault() is {} connection)return connection.DisconnectReason switch
        {
            DisconnectReason.HostNotAllowedToConnect=>L.T("Serwer SSH zgłosił zakaz połączenia z tego hosta. Sprawdź reguły dostępu, blokady adresu i konfigurację serwera."),
            DisconnectReason.KeyExchangeFailed=>L.T("Nie udało się uzgodnić algorytmów bezpiecznego połączenia SSH. Sprawdź zgodność konfiguracji klienta i serwera; zabezpieczenia nie zostały obniżone."),
            DisconnectReason.HostKeyNotVerifiable=>L.T("Nie potwierdzono tożsamości serwera SSH. Zweryfikuj odcisk klucza hosta. Zaufaniem zarządzisz w Ustawienia → Bezpieczeństwo → Zaufane klucze SSH."),
            DisconnectReason.TooManyConnections=>L.T("Serwer SSH zgłosił przekroczenie limitu połączeń. Zamknij zbędne sesje lub sprawdź limity po stronie serwera."),
            DisconnectReason.ServiceNotAvailable=>L.T("Serwer zgłosił niedostępność żądanej usługi SSH. Sprawdź konfigurację i stan usługi na serwerze."),
            DisconnectReason.ProtocolVersionNotSupported=>L.T("Serwer zgłosił niezgodną wersję protokołu SSH. Sprawdź, czy wskazany port obsługuje SSH 2."),
            DisconnectReason.IllegalUserName=>L.T("Serwer SSH zgłosił niedozwoloną nazwę użytkownika. Sprawdź login i zasady dostępu konta."),
            DisconnectReason.NoMoreAuthenticationMethodsAvailable=>L.T("Serwer SSH zgłosił brak dalszych metod uwierzytelniania. Sprawdź, czy wybrana metoda i ewentualne dodatkowe składniki logowania są obsługiwane."),
            DisconnectReason.AuthenticationCanceledByUser=>L.T("Uwierzytelnianie SSH zostało anulowane. Ponów połączenie, jeśli chcesz kontynuować logowanie."),
            DisconnectReason.ProtocolError or DisconnectReason.MacError or DisconnectReason.CompressionError=>L.T("Połączenie SSH zakończono z powodu błędu protokołu lub integralności danych. Sprawdź usługę na wskazanym porcie i logi serwera."),
            _=>L.T("Serwer zamknął połączenie SSH lub zostało ono utracone. Kod rozłączenia nie wskazuje dokładnej przyczyny. Sprawdź sieć i logi SSH serwera.")
        };
        return null;
    }
}
