# ServerDesk 0.31.0 — weryfikacja MVP, 2026-10-16

## Poprawki i pomiary

Odczyt/odszyfrowywanie klucza, tworzenie klienta SSH/SFTP, handshake i sprzątanie po błędzie wykonują się w tle. Żądanie wcześniej anulowane nie otwiera klucza. Po zakończeniu rozpoczętej operacji przygotowania ponownie sprawdzamy anulowanie, zanim utworzymy połączenie. Nie przerywamy siłowo synchronicznego odczytu/derywacji wewnątrz biblioteki.

Test przed poprawką: wywołanie z własnym zaszyfrowanym kluczem oddawało Task po **1352,8 ms SSH / 381,8 ms SFTP**. Po poprawce: **0,6 / 0,4 ms Windows**, **1,1 / 0,4 ms Linux**. To czas oddania sterowania przez API, nie czas logowania ani przyspieszenie kryptografii. Harness ma limit 100 ms, pompuje dispatcher, sprawdza błędną passphrase, a następnie poprawne SSH exec i SFTP.

Monitoring wykonuje polecenia, zamknięcie kanału i parsowanie poza UI. Niezależne sekcje zachowują poprawne dane po błędzie CPU/RAM lub braku narzędzi. Braki są oznaczane w widoku i pasku parametrów; niedostępny RAM nie daje poprawnego „0%”. Fallback MemFree jest przybliżeniem. Walidujemy jednostki/zakresy RAM, przepełnienie sumy CPU i granice sekcji. Przerwa zeruje właściwe punkty odniesienia. Nieaktualne karty i wykres są czyszczone, a pusty wykres nie zajmuje miejsca.

Po awarii już działającego połączenia monitoringu klient jest zamykany i odtwarzany po 30 s przerwy oraz interwale odświeżania. Terminal pozostaje połączony. Pętla monitoringu jest jedna także po ręcznym ponowieniu widoku.

## Wyniki testów

- **475/475 testów jednostkowych Windows**, bez pominiętych; 14 nowych przypadków. Obejmują anulowanie przed odczytem klucza, pełną próbkę/CRLF/granice, brak lub nieprawidłowe CPU, opcjonalne polecenia, przerwę i nowy punkt odniesienia, brak/overflow/range RAM, obce pola i MemFree. Release rozwiązania i harnessy: zero błędów i ostrzeżeń. Istniejący test strumienia uploadu sprawdza teraz także rozmiar pierwszego odczytu, usuwając ostrzeżenie analizatora.
- **Windows i Ubuntu 24.04/WSL, rzeczywisty OpenSSH:** ograniczony PATH w procesie polecenia bez df/ip/lscpu zachowuje CPU i pamięć, oznacza brak dysków/topologii/adresów. W osobnych próbach polecenie odczytu CPU lub RAM trafia we własny plik z odmową dostępu; pozostałe sekcje nadal zawierają rzeczywiste dane. Nie zmieniano /proc, systemowego PATH ani uprawnień systemu.
- Obie platformy: zerwanie tylko własnego połączenia monitoringu przez relay. Interfejs czyści stare wartości, terminal nadal jest połączony, a nowy klient monitoringu przywraca próbki po backoff. To test realnych transportów, nie samo ustawienie flagi statusu.
- PL/EN, dark/light, 650/900 px: ostrzeżenia, dostępne i niedostępne pola, RAM — zamiast zera, ukryty pusty wykres, powrót poprawnych danych oraz czyszczenie przy zamknięciu. Obejrzano zrzuty panelu.
- Macierz cyklu życia na obu platformach: cztery równoległe sesje, 16 reconnect i dodatkowy reconnect po EOF, terminalowe znaczniki, split/floating/redock, zwolnienie 16 starych źródeł anulowania i ośmiu zamkniętych błędnych prób. Przy cichej utracie własnych połączeń SSH/SFTP/monitoringu oraz uploadu z zadaniem oczekującym karta oddaje oba sloty; aplikacja czeka na jej sprzątanie. Windows: usunięcie karty **13 ms**, pełne zamknięcie **2103 ms**; Linux: **11 ms / 2227 ms**. Pomiary fixture z limitem 5 s, nie gwarancja WAN.
- NetworkProbe: rzeczywisty klucz/passphrase, PTY/ANSI/resize, CPU/RAM/dyski/sieć, EOF, utrata własnego procesu sesji, backpressure/dispose, izolacja terminali i reconnect. SFTP: Unicode/checksum/chmod/rename/delete, odmowa nadpisania, cancel z zachowaniem oryginałów, porządkowanie własnych plików tymczasowych na działającym łączu, obcięty download i blokada zmienionego klucza hosta.
- Windows UI SFTP: 1 MiB w obie strony z porównaniem bajtów, wybór/filtry/sortowanie, nawigacja, overwrite/cancel/retry, odzyskiwanie błędów, zwijanie/czyszczenie i limit 50 zakończonych zadań. Regresja anulowania SSH/VNC/RDP obejmuje zawieszony handshake i zamknięcie podczas łączenia.
- Pełny UiSmoke: motywy/kontrast/ikony/ustawienia, małe układy i skale renderera, personalizacja, split/floating/redock oraz odtwarzanie bez pobierania poświadczeń i automatycznego logowania. Dodatkowe release smoke sprawdza lokalizację, security/stability i protokoły na własnych fixture.

Testy wykorzystują wyłącznie własny OpenSSH, bazy i katalogi GUID. Sprzątanie `/tmp/serverdesk-monitoring-<GUID>` usuwa dowiązania, nie podąża za nimi. Nie użyto prywatnego RDP ani jego hasła. Nowe odtwarzalne scenariusze: `--ssh-responsiveness`, `--monitoring-acceptance`; istniejące `--session-lifecycle` i NetworkProbe pozostają w źródłach.

## Zależności i paczki

Audyt NuGet: `dotnet list ServerDesk.sln package --vulnerable --include-transitive --format json --no-restore`, kod 0, cztery projekty, źródło api.nuget.org, brak zgłoszonych pakietów podatnych i problemów źródła. Surowy wynik: `artifacts/mvp-0310-dependency-audit.json`. Nie jest to pełny audyt bezpieczeństwa kodu. Brak nowych zależności, migracji bazy i zmian formatów poświadczeń; MIT pozostaje licencją projektu.

Kontrole wydania obejmują manifest/asInvoker i natywny host RDP w EXE 0.31.0.0 podczas split/floating/redock/resize, trzy odizolowane starty Windows z korektą położenia okna, rzeczywiste okno Linux/WSLg w stanie IsViewable, instalację/upgrade/uninstall we własnej ścieżce z /NOREGISTER oraz zgodność bajtów archiwów i payloadów. Linux zawiera 331 plików. Zamrożony zestaw zawiera Setup/ZIP/DEB/TAR, źródła MIT, raport i SHA-256; wcześniejsze wydania zachowują sumy. Nie jest to nowe logowanie RDP ani instalacja na fizycznych dystrybucjach Linux.

Ważne logi w `artifacts/`: `mvp-0310-key-before.txt`, `mvp-0310-key-after.txt`, `mvp-0310-linux-key.txt`, `mvp-0310-monitoring-final.txt`, `mvp-0310-linux-monitoring.txt`, `mvp-0310-tests-final.txt`, `mvp-0310-session-recovery.txt`, `mvp-0310-linux-session.txt`, `mvp-0310-probe.txt`, `mvp-0310-sftp.txt`, `mvp-0310-ui.txt`, `mvp-0310-control.txt`. Wyniki instalatora/startu/paczek mają ten sam prefiks. Szczegółowe zrzuty: `mvp-0310-monitoring-final/` i `mvp-0310-linux-monitoring/`.

## Granice

Minimalny PATH i zmieniona ścieżka do własnego pliku z odmową odczytu nie zastępują pełnej macierzy dystrybucji, powłok ani polityk uprawnień. Pierwszy błąd utworzenia monitora można ponowić przez otwarcie Monitoring; automatyczne odtworzenie dotyczy utraty już uruchomionego monitora. Bieżący etap nie powtarza 10-minutowego testu ośmiu sesji z 0.30.0 i nie stanowi wielogodzinnego soak. Fizyczne DPI/IME/klawiatury, sprzęt Serial, AD/Microsoft RDP i KDE/KWallet/Fedora/Debian nadal wymagają akceptacji. Pozostają też ograniczenia transferów i możliwe zdalne .part po utracie sieci. [Stan MVP](../mvp-status.md), [ograniczenia bety](../known-issues.md).
