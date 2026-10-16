# Stabilizacja kolejki SFTP przed 1.0

Wersja rozwojowa po 0.36.2. Zakres: kolejka transferów i dostępność Ponów; bez zmian protokołu, schematu danych ani zależności.

## Problem i poprawka

Zakończone kopiowanie utrzymywało dedykowane połączenie SFTP oraz globalny slot podczas `RefreshAsync` korzystającego z osobnego połączenia przeglądania. Dwie zatrzymane listy mogły zablokować oba sloty, choć widok pokazywał już zakończone transfery. Własny scenariusz przed poprawką zakończył się błędem `Completed uploads retained global slots while the browser was paused`.

Po poprawce dedykowane połączenie i slot są zwalniane przed odświeżeniem. Zadanie nadal pozostaje obserwowane do końca odświeżania, aby istniejący mechanizm zamknięcia/reconnect mógł je zakończyć. Błąd listowania dodaje informację o ręcznym odświeżeniu bez zmiany sukcesu kopiowania. Przyciski Ponów są wyłączone przy rozłączeniu i łączeniu; ten sam stan trafia do podglądu Transfery.

## Celowana weryfikacja

- Własny OpenSSH na loopback:22222 w Ubuntu WSL, klient Windows, syntetyczne pliki i osobna baza. Nie używano zapisanych połączeń ani poświadczeń użytkownika. Zakłócano wyłącznie własne gniazda przekaźnika, bez zmian konfiguracji sieci/zapory.
- `--transfer-queue-recovery`: trzy zadania, zatrzymane odświeżanie, oba sloty zwolnione i trzecie zadanie ukończone; przerwanie listowania zachowuje sukces i wyświetla wskazówkę; pobrane pliki porównane bajt po bajcie. Anulowanie zadania oczekującego nie publikuje pliku. Utrata transportu SSH wyłącza Ponów; reconnect je włącza, a ponowienie zachowuje pierwotny cel i zwalnia slot. PASS.
- 14/14 celowanych testów `CoalescingProgressTests`, `TransferTraversalTests`, `AcknowledgedReadStreamTests`, 0 pominiętych. Build narzędzia: 0 ostrzeżeń/błędów. Obejrzano zrzut kolejki po reconnect i ponowieniu.
- Kontrola zawartości/linków repozytorium i `git diff --check`: PASS.

Dowody: `artifacts/reports/dev-transfer-queue/` — `before.txt`, `after.txt`, `transfer-queue.trx` i `transfer-queue-recovery.png`. Testowy serwer uruchomiony dla tej próby jest zatrzymywany po weryfikacji.

To celowana regresja kolejki. Nie zastępuje wielogodzinnych prób, rzeczywistych łączy WAN, braku miejsca na dysku, wszystkich faz przerwania transferu ani pełnej macierzy Windows/Linux. Nie przygotowano instalatora, nowego numeru wersji, tagu ani pakietów wydania; aplikacja jest dostarczana z folderu developerskiego.
