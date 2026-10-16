namespace ServerDesk.Core;

public sealed record RdpExitStatus(bool IsError,string Message)
{
    public static RdpExitStatus FromClient(int code,bool freeRdp)
    {
        if(!freeRdp)return new(code!=0,code==0?L.T("Klient RDP zamknięty. Możesz połączyć ponownie."):L.F($"Klient Windows RDP zakończył się z kodem {code}. Sprawdź komunikat w oknie klienta."));
        var message=code switch
        {
            0 or 1 or 2 or 11=>L.T("Klient RDP zamknięty. Możesz połączyć ponownie."),
            3 or 4=>L.T("Serwer zakończył sesję z powodu limitu czasu. Połącz ponownie."),
            5=>L.T("Sesję zastąpiło inne połączenie RDP."),
            9 or 144 or 155 or 158=>L.T("Konto nie ma dostępu do RDP. Sprawdź uprawnienia logowania do Pulpitu zdalnego na serwerze."),
            132 or 134 or 154=>L.T("Logowanie RDP odrzucone. Sprawdź hasło oraz pełny login konta lokalnego, domenowego lub Microsoft."),
            135=>L.T("Konto jest zablokowane. Sprawdź jego stan na serwerze przed kolejną próbą."),
            148 or 149 or 152=>L.T("Hasło wygasło lub wymaga zmiany. Zaktualizuj je na serwerze przed połączeniem RDP."),
            151=>L.T("Konto jest wyłączone na serwerze."),
            131 or 139 or 140 or 141 or 147=>L.T("Nie udało się połączyć z serwerem RDP. Sprawdź adres, port i sieć/VPN."),
            133 or 143=>L.T("Nie udało się uzgodnić TLS/NLA lub zweryfikować certyfikatu. Sprawdź szczegóły w Konsoli RDP i porównaj odcisk certyfikatu z zaufanym źródłem."),
            _=>L.F($"Klient FreeRDP zakończył się z kodem {code}. Sprawdź komunikat w konsoli klienta.")
        };
        return new(code is not (0 or 1 or 2 or 3 or 4 or 5 or 11),message);
    }
}
