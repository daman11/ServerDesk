# ServerDesk 0.9.0 — automatyczny zapis i ochrona transferów

Układ kart zapisuje się w trakcie pracy, po 600 ms bez kolejnej zmiany. Obejmuje dodanie/usunięcie karty, dokowanie, kolejność i wybór, kierunek/proporcje podziału, panel Terminal/SFTP/Server Info oraz normalną geometrię osobnego okna. Poprawne zamknięcie dodatkowo opróżnia kolejkę zapisu. Po awarii wraca ostatni ukończony zapis. Karty nadal czekają na „Połącz”; odtwarzanie nie uruchamia transportu ani nie odczytuje poświadczeń.

Stan kontrolek jest zbierany bez przerw na wątku UI, a operacja SQLite odbywa się poza nim. Zapis jest wykonywany kolejno, więc starsze zadanie nie nadpisuje nowszego układu. Jedna transakcja sprawdza ustawienie przywracania i identyfikatory istniejących profili oraz zapisuje karty/kierunek/proporcje. Pomijanie Quick Connect następuje przed ograniczeniem do 128 kart. Zapis referencji nie odczytuje pełnych profili ani ich grafik. Wyłączenie przywracania jest respektowane również przez oczekujący zapis. Błąd zapisu jest widoczny w nagłówku; kolejna zmiana ponawia próbę.

Wielokrotne żądanie zamknięcia otwiera jedno potwierdzenie. Anulowanie pozostawia karty i osobne okna. Zamknięcie czeka na inicjalizację i zapis przed zwolnieniem sesji. Karta oczekująca na magazyn poświadczeń ma nieaktywny przycisk połączenia; usunięta karta nie rozpoczyna spóźnionego połączenia. Licznik przywróconych kart w nagłówku aktualizuje się po próbie połączenia/usunięciu karty.

SFTP sprawdza rozmiar pliku przed publikacją pliku tymczasowego. Jeżeli serwer skróci plik podczas pobierania, lokalny oryginał zostaje zachowany. Wysyłanie także weryfikuje rozmiar przed rename. Sprzątanie nie zastępuje pierwotnego błędu transferu błędem usunięcia pliku tymczasowego. Nadpisywanie zdalnego pliku nadal korzysta z atomowego POSIX rename; nie usuwa wcześniej oryginału.

## Weryfikacja

120 testów xUnit zaliczonych. Nowe testy repozytorium obejmują istniejące/usunięte identyfikatory, opt-out przy oczekującym zapisie, ponowne włączenie, rollback niepoprawnego układu oraz limit po odfiltrowaniu Quick Connect.

Dwuprocesowy test headless pokazuje okno z dwiema kartami jednego profilu, podziałem, SFTP i osobnym oknem; czeka na automatyczny zapis, a następnie kończy proces przez `Environment.Exit` bez `Closing` i `Dispose`. Drugi proces odtwarza układ z zerem odczytów poświadczeń i zerem połączeń. Sprawdza wielokrotne żądanie zamknięcia/anulowanie, szybkie zmiany układu, automatyczne zapamiętanie usunięcia ostatniej karty oraz opóźniony odczyt poświadczeń: jeden odczyt po podwójnym kliknięciu i brak połączenia na usuniętej karcie. Reprodukcja: `scripts/test-workspace-recovery.ps1`. Wyniki: `artifacts/workspace-crash-writer.txt`, `artifacts/workspace-crash-recovery.txt`.

Rzeczywiste testy na izolowanym OpenSSH Ubuntu/WSL potwierdzają anulowanie pobierania i wysyłania do istniejącego/nowego celu, zachowanie oryginałów, brak opublikowanych częściowych plików i sprzątanie własnych plików tymczasowych przy zachowanym połączeniu. Kontrolowane skrócenie zdalnego pliku w trakcie pobierania jest wykrywane i nie nadpisuje lokalnego pliku. Pozostałe SSH/PTY/EOF/zerwanie/reconnect/monitoring/fingerprint także przeszły. Wyniki: `artifacts/network-probe-0.9.txt`.

Pełne UiSmoke obejmuje poprzednie scenariusze ikon, personalizacji, dokowania, wyszukiwarki oraz rzeczywiste terminal/nano, SFTP i monitoring: `artifacts/ui-reliability-verification.txt`, obrazy `artifacts/ui-reliability-final`. Uruchomienia natywne wydania Windows oraz Ubuntu/WSLg: `artifacts/release-0.9-windows-smoke.txt`, `artifacts/release-0.9-linux-smoke.txt`.

## Ograniczenia

Awaria przed zakończeniem zapisu może utracić najnowszą zmianę układu. Zapis nie odtwarza procesów serwera, terminalowego scrollbacku ani transferów. Sprawdzenie rozmiaru nie jest sprawdzeniem kryptograficznej integralności i nie wykryje podmiany treści o tym samym rozmiarze. Przy utracie transportu lub braku uprawnień usunięcie pliku `.part` może się nie udać; nie ma automatycznego dziennika sprzątania po reconnect. RDP pozostaje klientem zewnętrznym, bez testowego serwera Windows. Pozostała macierz: [roadmap.md](../roadmap.md).
