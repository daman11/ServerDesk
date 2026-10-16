# Odzyskiwanie przeglądania SFTP przed 1.0

Wersja rozwojowa po 0.36.2, bez nowych zależności i zmiany formatu danych. Zakres: ręczne odzyskanie wykrytego zerwania osobnego połączenia plikowego; działający terminal SSH pozostaje aktywny.

Przyciski Odśwież/F5, jawna nawigacja i otwieranie SFTP korzystają ze wspólnej, serializowanej ścieżki. Martwy klient jest zwalniany poza UI, a nowy przechodzi zwykłe uwierzytelnianie oraz weryfikację klucza hosta. Katalogi, filtry, zaznaczenie i historia nie są resetowane przy samej wymianie klienta plikowego. Istniejące blokady i token sesji ograniczają tę pracę; zamknięcie czeka na oddanie klienta. Odczyt po transferze pozostaje osobną ścieżką bez automatycznego reconnect i nie ponawia żadnej operacji zapisującej.

## Wykonane celowane sprawdzenia

- Własny OpenSSH w Ubuntu WSL, loopback:22222, klient Windows i odizolowana baza. Nie korzystano z zapisanych połączeń ani danych uwierzytelniających użytkownika.
- `--sftp-browser-recovery`: zerwanie wyłącznie transportu przeglądania, rzeczywisty przycisk Odśwież, zachowanie obu katalogów/filtrów/zaznaczeń/historii i identyfikatora połączenia SSH. Wysłany przez oryginalny strumień SSH marker pojawił się w tym samym terminalu. PASS.
- Ten sam scenariusz: odrzucenie celowo niezgodnego fingerprintu w bazie fixture bez publikowania nowego klienta; po naprawie zaufania F5 i dodatkowe odświeżenie otwierają łącznie jednego klienta. PASS.
- Zamknięcie karty w trakcie odzyskiwania, przy testowej decyzji o kluczu oczekującej z CancellationToken: anulowanie i oddanie klienta, brak połączenia w zamkniętej karcie. PASS. To kontrolowany callback testowy, nie test fizycznego systemowego dialogu.
- Regresje `--sftp-navigation-network` oraz `--transfer-queue-recovery`: PASS. Obejmują historię/rollback/Unicode/nieaktualne żądania oraz kolejkę, wstrzymane odświeżanie, anulowanie i retry do pierwotnego celu.
- 25/25 testów `DirectoryNavigationHistoryTests`, `PathNavigationTests`, `LocalizationTests`, bez pominięć. Build celowanego narzędzia: 0 ostrzeżeń i błędów. Obejrzano zrzut panelu po odzyskaniu.
- `git diff --check` i kontrola zawartości/linków repozytorium: PASS.

Dowody: `artifacts/reports/dev-sftp-browser/` — `recovery.txt`, `navigation.txt`, `queue.txt`, `sftp-browser.trx`, `sftp-browser-recovered.png`. Własne katalogi zdalne i serwer uruchomiony dla próby są sprzątane po weryfikacji.

Nie wykonywano pełnej macierzy, Linux GUI, fizycznych gestów i testów WAN. Cicha utrata pakietów może nadal czekać na timeout biblioteki; ta zmiana nie dodaje nowego detektora bezczynności ani automatycznej pętli ponawiania. Zmiany po stronie serwera i plików wymagają zwykłej walidacji po ponownym odczycie. Nie zbudowano instalatora, paczek ani tagu; dostarczana jest wersja do testowania z folderu.
