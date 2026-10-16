# ServerDesk 0.18.0 — VNC i Serial

Nowe profile **VNC** i **Serial** są dostępne w edytorze, filtrach listy, wyszukiwaniu, imporcie/eksporcie oraz zapisie i przywracaniu kart. Przywrócone karty czekają na Połącz. Zachowano numery istniejących protokołów SSH/RDP i zgodność starych profili.

## VNC

Pulpit działa wewnątrz zakładki na Windows i Linux, z dopasowaniem obrazu lub rozmiarem rzeczywistym i przewijaniem. Obsługuje mysz, przyciski i przewijanie, tekst Unicode, klawisze sterujące i przycisk Ctrl+Alt+Del. Ctrl+Alt+← / → oddaje fokus aplikacji i zwalnia wciśnięte klawisze. Tryb Tylko podgląd blokuje wysyłanie wejścia. Sesja pozostaje aktywna przy podziale widoku, odpięciu i ponownym dokowaniu. Rozłącz zamyka własny transport, a po utracie sieci karta pozwala połączyć się ponownie.

Domyślny port to 5900. Profil wybiera standardowe hasło VNC albo jawny brak uwierzytelniania. Negocjacja jest ograniczona do wybranej metody; hasło nie jest automatycznie zastępowane trybem None. Poświadczenie można zapisać w istniejącym magazynie systemowym, z osobnym identyfikatorem VNC, aby nie użyć hasła SSH/RDP po zmianie protokołu. Hasła nie wchodzą do eksportu.

Ta implementacja nie obsługuje TLS/VeNCrypt, kont systemowych RealVNC, schowka ani transferów plików VNC. Standardowe VNC nie szyfruje transmisji; formularz wskazuje zastosowanie zaufanej sieci, VPN lub istniejącego tunelu. Hasło standardowego VNC wykorzystuje pierwsze 8 bajtów. Backend MarcusW.VncClient 1.0.0-alpha4 jest przypiętą biblioteką w wersji alpha; własny adapter obsługuje Avalonia 12. Nie jest instalowany zewnętrzny viewer ani serwer VNC. Tight/JPEG wyłączono, aby nie wymagać dodatkowego TurboJPEG.

Szybkie połączenie: `vnc://host:5900` lub `vnc://[::1]:5901`.

## Serial

Terminal otwiera COM na Windows lub urządzenie Linux, np. `/dev/ttyUSB0`. Edytor pozwala wykryć porty lub wpisać nazwę ręcznie oraz ustawić baud rate, 5–8 bitów danych, parzystość, bity stopu, XON/XOFF / RTS/CTS, DTR i ręczne RTS. Domyślnie 115200, 8N1, bez sterowania przepływem, DTR/RTS wyłączone. Przy RTS/CTS linią RTS zarządza sterownik.

Serial korzysta z istniejącego emulatora terminala, kopiowania/wklejania, ustawień czcionki i palety oraz dokowania. Nie wymaga loginu, hasła ani sieci i nie uruchamia SSH/SFTP/monitoringu. Otwarcie, rozłączenie i ponowne otwarcie mają własny cykl życia. Odczyt ma limit oczekiwania i ograniczoną kolejkę; tylko wątek odczytu zamyka port, aby uniknąć zakleszczenia natywnego zdarzenia na Linux. Nieobsługiwane parametry, brak uprawnień, brak urządzenia lub zajęty port dają komunikat o błędzie.

Szybkie połączenie: `serial://COM3?baud=9600` lub `serial:///dev/ttyUSB0?baud=115200`. Pozostałe ustawienia wybiera się w profilu. Parametry obsługiwane przez fizyczny sprzęt zależą od urządzenia i sterownika.

## Weryfikacja

214/214 testów xUnit. Adapter VNC przechwytuje oczekiwane anulowanie we własnej pętli odbioru i adapterze nadawania; usuwa to wykrytą awarię upstream alpha4 przy zamykaniu aktywnej sesji. Osobny test kończy aktywny transport w oczekiwaniu na kolejne ramki.

Test syntetycznego AltGr sprawdza, że `ą` i `Ń` trafiają jako Unicode, zamiast Ctrl+A lub polecenia nowego połączenia Ctrl+Shift+N. Nie zastępuje pełnej macierzy fizycznych klawiatur i układów.

Testy modeli i repozytorium obejmują stare profile, nowe URI, walidację, zapis i eksport/import, rozdzielenie referencji poświadczeń oraz limity bufora obrazu. Rzeczywisty transport TCP na loopback z serwerem testowym RFB 3.8 sprawdza hasło/None, błędne hasło, blokadę downgrade, piksele, klawisze i mysz, utratę sieci i zamykanie. Scenariusz UI sprawdza formularze, filtry, obraz, wejście Unicode/Control, Ctrl+Alt+Del, blokadę podglądu i ciągłość przy dokowaniu. Nie zastępuje testów z konkretnym serwerem TigerVNC/UltraVNC/RealVNC.

Rzeczywisty System.IO.Ports na izolowanym pseudo-terminalu Linux potwierdził round trip UTF-8 i ANSI/Control, anulowanie pustego odczytu przy 9600/115200 8N1, zamknięcie i ponowne otwarcie. Osobny test uruchomił ten port w terminalu aplikacji, zachowując emulator po odpięciu, dokowaniu i zastosowaniu wyglądu. Fizycznego adaptera COM/USB nie było w środowisku; nie deklarujemy testu sprzętowego Windows ani wszystkich kombinacji parametrów linii.

Wyniki: `artifacts/protocols-0180-unit-checks.txt`, `artifacts/protocols-0180-ui-checks.txt`, `artifacts/protocols-0180-serial-checks.txt`, `artifacts/protocols-0180-serial-pane-checks.txt`. Obrazy: `artifacts/ui-0180-protocols`, `artifacts/ui-0180-serial-pane`.
