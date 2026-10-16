# ServerDesk 0.30.0 — weryfikacja MVP, 2026-10-15

## Poprawki

Etap obejmuje cykl życia istniejących sesji, transferów i okien. Test przed poprawką wykazał **8/8 pozostających źródeł anulowania** po zamknięciu ośmiu błędnych połączeń, mimo zwolnienia samych kontrolek. Po poprawce wynik wynosi **0/8** przy nadal żyjącym tokenie rodzica.

Karta udostępnia jedno idempotentne zadanie zamknięcia. Czeka ono na zakończenie łączenia, odświeżania, wyglądu, transferów, monitoringu, zegara i historii, po czym zwalnia źródła anulowania. Reconnect czeka na stare transporty i transfery. Aplikacja śledzi również zamykanie kart wcześniej usuniętych z widoku. Klienci SSH/SFTP/monitoringu są zamykani poza UI; zamykanie kanałów ma krótki limit. Zakończenie powłoki zatrzymuje zależne połączenia i czyści stare metryki. Zadanie zakończenia SSH dostaje wynik także przy błędzie sprzątania.

Test działającego terminala wykrył wyjątek `InvalidateArrange on wrong LayoutManager` przy przenoszeniu do osobnego okna. Przed ponownym dołączeniem karty opróżniamy układ poprzedniego okna przez publiczne `UpdateLayout()`. Analiza mechanizmu: [kod Avalonia12.0.4](https://github.com/AvaloniaUI/Avalonia/blob/12.0.4/src/Avalonia.Base/Layout/LayoutManager.cs). Bez refleksji do prywatnego API, nowych zależności, migracji bazy i zmian formatów sekretów.

## Sprawdzone scenariusze

- **461/461 testów jednostkowych**, zero pominiętych; Release rozwiązania i końcowych harnessów bez błędów i ostrzeżeń.
- **Windows i Ubuntu24.04/WSL, rzeczywisty OpenSSH:** cztery równoległe terminale, 16 reconnect, odczyt własnych znaczników z powłoki, dwa panele, floating i redock. Po zakończeniu starych operacji GC zwalnia wszystkie 16 poprzednich źródeł anulowania. Test nie utrzymuje silnych referencji do starych transportów we własnych zmiennych.
- Dodatkowe `exit` z powłoki przy otwartych SFTP i monitoringu: zatrzymanie obu klientów, usunięcie nieaktualnej próbki i kolejne poprawne połączenie. Łącznie **17 reconnect** w tej macierzy na każdej platformie.
- **Cicha utrata sieci** obejmuje jednocześnie własne połączenia terminala, przeglądarki, monitoringu i uploadu 8MiB. Istnieje też zadanie oczekujące na slot. Usunięcie karty: **9ms Windows / 7ms Linux**; pełne zamknięcie: **2082ms / 2108ms** w końcowych przebiegach. Oba transfery kończą pracę, wracają dwa globalne sloty. Okno aplikacji zamyka się dopiero po zakończeniu sprzątania usuniętej karty. To pomiary konkretnego fixture z limitem5s, nie gwarancja dla każdego WAN.
- Relay zatrzymuje tylko jawnie wskazane własne połączenia; nie zmienia zapory, interfejsów, listenera SSH ani cudzych sesji. Pliki mają własny `/tmp/serverdesk-lifetime-<GUID>`, sprzątany z kontrolą zakresu i bez podążania za linkami.
- NetworkProbe: klucz/passphrase i exec, interaktywny PTY/ANSI/resize, monitoring, EOF, utrata własnego procesu sesji, zakończenie przy backpressure, dwie izolowane sesje i reconnect. SFTP: Unicode i porównanie bajtów, chmod/rename/delete, odmowa nadpisania, cancel z zachowaniem oryginałów, usuwanie własnych plików tymczasowych na działającym łączu, obcięty download oraz odmowa zmienionego klucza hosta.
- UI SFTP Windows: rzeczywisty plik1MiB w obie strony, filtry/sortowanie/zaznaczenie, ścieżki/klawiatura, overwrite/cancel/retry, odzyskiwanie błędów i limit50 zakończonych zadań.
- Pełny UiSmoke Windows/Linux: pięć motywów, kontrast, ikony, ustawienia, małe okna i skale renderera, personalizacja, split/floating/redock i odtwarzanie bez automatycznych połączeń. Regresja SSH/VNC/RDP: anulowanie zawieszonego handshake, propagacja tokenu i zamknięcie podczas łączenia. VNC/Serial: rzeczywisty loopback RFB, klawiatura/wskaźnik/view-only, utrata sieci, reconnect/cleanup i brakujące urządzenie Serial. Obejrzano zrzut dwóch paneli.

## Ograniczony test ośmiu sesji

**Windows, 10 minut, osiem połączeń i 36 reconnect.** Cykle sprawdzają rzeczywiste znaczniki terminala, monitoring, przeglądarkę SFTP oraz zmianę paneli i osobne okna. Wszystkie 36 próbek wykazuje osiem połączonych sesji; końcowe sprzątanie i zwrot slotów również przechodzą. To cykliczna praca z przerwami, nie osiem terminali stale generujących maksymalny ruch.

Od 120 s pomiary po wymuszonym GC mieszczą się w zakresach: zarządzana sterta **24,45–42,09 MiB**, private bytes **97,82–120,27 MiB**, uchwyty **761–778**, wątki **40–44**. Ostatnia próbka w 590 s: **28,50 MiB** sterty i **119,83 MiB** private bytes, 766 uchwytów i 40 wątków. Są to zasoby procesu harnessu z aplikacją headless; private bytes nie oznacza RSS. Wyniki pokazują przebieg tej próby, a nie dowód braku wszystkich wycieków ani gwarancję wielogodzinnej stabilności. Surowe próbki: `artifacts/mvp-0300-endurance/endurance.json`.

Dodatkowa regresja transferów Windows: utrata sieci podczas uploadu — cancel2077ms, download — cancel1588ms; zachowane oryginały i retry z porównaniem bajtów. Katalog5000 wpisów: pełny wynik, naturalne sortowanie, zachowane zaznaczenie i anulowany odczyt; odświeżenie126ms w tym przebiegu. Ostrzeżenie o nieudanym sprzątaniu zdalnego `.part` podczas utraty sieci jest spodziewane; katalog fixture usunięto później przez osobne działające połączenie.

## Gotowe paczki

- Windows EXE **0.30.0.0**: poprawny manifest/asInvoker, rzeczywisty natywny host RDP zachowany podczas split/floating/redock/resize i zwolniony przy zamknięciu. Bez nowego logowania RDP.
- Trzy osobne starty EXE: korekta położenia poza ekranem, ponowne uruchomienie zmaksymalizowanego okna i fallback po odłączonym monitorze; poprawne zamknięcie.
- Linux: rzeczywisty proces i nowe okno WSLg w stanie X11 `IsViewable`, własne dane i kontrolowane zamknięcie własnego PID.
- Instalator: własna ścieżka z polskimi znakami/spacjami, realna baza/ustawienia, start/wyjście, odmowa update/uninstall działającej aplikacji, zachowanie danych i obcych plików oraz zgodność bajtów payloadu. `/NOREGISTER` zachowuje istniejącą instalację, rejestrację i skróty użytkownika.
- ZIP i payloady: metadane, pełne notices/licencje, kompletność i wykluczenie danych prywatnych. Raport jest dołączany także do katalogu docs obu aplikacji, aby lokalne odnośniki działały offline. Linux TAR/DEB: **331 plików** zgodnych z payloadem, wersja 0.30.0/amd64, prawa wykonania/root ownership i desktop entry. Dziesięć starszych zamrożonych wydań 0.23.0–0.29.0 zachowuje wszystkie sumy SHA-256.

Istotne logi: `mvp-0300-lifetime-before.txt`, `mvp-0300-eof-final.txt`, `mvp-0300-linux-session.txt`, `mvp-0300-endurance.txt`, `mvp-0300-build-final.txt`, `mvp-0300-probe.txt`, `mvp-0300-sftp.txt`, `mvp-0300-resilience.txt`, `mvp-0300-ui.txt`, `mvp-0300-linux-ui.txt`, `mvp-0300-control-final.txt`, `mvp-0300-protocols.txt`, `mvp-0300-native-rdp.json`, `mvp-0300-startup.txt`, `mvp-0300-installer.txt` w `artifacts/`. Harnessy zawierają odtwarzalne `--session-lifecycle` i `--session-endurance`. Nie użyto prywatnego RDP, jego hasła ani danych serwerów użytkownika. Własny sshd zatrzymano po testach.

## Granice

Nie wykonano wielogodzinnego soak, dziesiątek równocześnie obciążonych terminali, fizycznych mieszanych DPI/IME/Serial, nowych logowań RDP ani oddzielnej akceptacji Windows10/KDE/KWallet/Fedora/Debian. Headless i skale renderera nie zastępują fizycznych gestów i monitorów. Po utracie sieci zdalny `.part` może pozostać; retry kopiuje od początku. [Stan MVP](../mvp-status.md), [ograniczenia bety](../known-issues.md).
