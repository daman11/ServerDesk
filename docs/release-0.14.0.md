# ServerDesk 0.14.0 — diagnostyka i sterowanie RDP

Przed uruchomieniem mstsc/FreeRDP aplikacja sprawdza połączenie z wybranym adresem i portem oraz negocjację NLA/CredSSP. Nie wysyła wtedy loginu ani hasła. Niedostępny serwer, timeout, inna usługa na porcie i odrzucona negocjacja mają czytelny komunikat. Anulowanie/zamknięcie sesji przerywa sprawdzanie. Klient nie uruchamia się po nieudanej diagnostyce.

Zakładka RDP ma panel z adresem, loginem, rozdzielczością i wynikiem sprawdzenia. „Sprawdź serwer” dodatkowo odczytuje certyfikat TLS: podmiot, SHA-256, termin ważności i wynik walidacji przez system. Niezaufany certyfikat jest prezentowany, lecz diagnostyka go nie akceptuje. Decyzja w kliencie nadal należy do użytkownika. Nie wyłączono TLS ani NLA.

Usunięto drugi przycisk uruchamiania RDP, który wcześniej omijał zwykłą obsługę łączenia. „Pokaż okno” przywraca/aktywuje okno należące do klienta Windows; „Rozłącz RDP” zamyka tylko klienta danej sesji, po potwierdzeniu. Anulowanie potwierdzenia zachowuje klienta. Menu „Opcje” pozwala uruchomić sesję ponownie. Po zakończeniu procesu wraca możliwość połączenia; szybkie zakończenie klienta nie jest nadpisywane późniejszym statusem uruchomienia.

Zamknięcie całej aplikacji czeka na zakończenie jej klientów RDP przed wyjściem z procesu. Kolejne kliknięcie zamknięcia nie omija sprzątania. Ustawienia pełnego ekranu/schowka w zakładce są edytowalne przed uruchomieniem klienta.

Kod wyjścia FreeRDP jest tłumaczony na wskazówki dotyczące logowania, zablokowanego/wyłączonego konta, uprawnień, wygasłego hasła, sieci i TLS/NLA. Stan procesu pozostaje odrębny od potwierdzenia zalogowania do pulpitu. Mapowanie pochodzi z [oficjalnych kodów FreeRDP](https://pub.freerdp.com/api/xfreerdp_8h_source.html).

## Weryfikacja

- 160/160 xUnit: dotychczasowe funkcje, parsowanie negocjacji, rozpoznanie NLA bez przesłania poświadczeń, timeout/anulowanie, błędna usługa, niezaufany certyfikat i opisy kodów klienta. Wynik: `artifacts/test-results/unit.trx`.
- UI: uruchomienie, anulowanie rozłączenia, pełne rozłączenie, ponowne uruchomienie, błąd preflight i brak fałszywego statusu zalogowania. Kontrolowany proces testowy zastępuje klienta wyłącznie w testach. Szerokości 520/1100 oraz tekst 13/16. Wynik: `artifacts/rdp-ui-checks.txt`; obrazy: `artifacts/ui-rdp-final`.
- Rzeczywisty prywatny serwer Windows: negocjacja HYBRID_EX, certyfikat TLS, logowanie lokalnego konta przez NLA oraz rzeczywisty obraz pulpitu 1280×720 przez FreeRDP w Ubuntu/WSLg. Certyfikat był przypięty do konkretnego SHA-256 po potwierdzeniu właściciela. Hasło przekazano przez stdin, bez argumentu `/p:`, pliku z hasłem i zapisu do kodu aplikacji. Schowek i foldery były wyłączone w tym teście. Okno zamknięto kontrolowanym limitem czasu; SIGTERM/exit 124 w logu jest zakończeniem testu, a nie dowodem błędu uwierzytelnienia. Obraz sprawdzono przez Computer Use; nie jest dołączany do dystrybucji. Lokalne wyniki: `artifacts/rdp-desktop-session.txt`, `artifacts/rdp-real-checks.txt`.

Pierwsze próby narzędzia testowego błędnie przekazywały CRLF z Windows jako część hasła czytanego przez klienta Linux. Po użyciu LF uwierzytelnienie przeszło z tymi samymi danymi. Nie zmieniano hasła ani konfiguracji bezpieczeństwa serwera.

Pulpit nadal działa w osobnym oknie. To nie jest PoC osadzonego obrazu RDP w Avalonia. Rzeczywiste logowanie zweryfikowano przez FreeRDP/WSLg; test procesu mstsc nie stanowi dowodu jego uwierzytelnienia. Konta AD/Microsoft, synchronizacja schowka, przekazywanie folderów, osadzanie obrazu i pełna macierz input/fullscreen pozostają do weryfikacji.
