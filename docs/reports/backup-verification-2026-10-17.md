# Sprawdzanie zapisanej kopii bazy — 2026-10-17

Mały krok rozwojowy po 0.39.1: przycisk **Sprawdź kopię / Check backup** w Ustawienia → Bezpieczeństwo → Zapisane kopie bazy. Bez nowego wydania, migracji, zależności i zmian w magazynie poświadczeń lub sesjach protokołów.

Kontrola wybranego pliku wykonuje poza UI `quick_check(1)`, odczyt `user_version` i porównanie z metadanymi nazwy. Akceptuje wersje schematu 0–3, ale nie migruje ich ani nie potwierdza zawartości pól aplikacji. Sprawdza obecność, rozmiar, dowiązanie i towarzyszące dzienniki; po odczycie ponawia kontrolę rozmiaru/czasu modyfikacji. Nie podmienia bazy, nie tworzy WAL/SHM ani nie czyta haseł i kolekcji. Lista nadal jest katalogiem metadanych; dopiero jawne kliknięcie rozpoczyna odczyt SQLite.

Użyty tryb [URI Microsoft.Data.Sqlite](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings) oraz [SQLite immutable](https://www.sqlite.org/uri.html) jest przeznaczony wyłącznie dla ukończonych, samodzielnych snapshotów. Nie jest stosowany do działającego `serverdesk.db`. Wynik dotyczy sprawdzonego pliku w chwili kontroli; nie gwarantuje poprawności po późniejszych zmianach ani nie zastępuje testu odtworzenia. Nie testowano niekooperujących równoczesnych zapisów do kopii, sieciowych systemów plików i pełnej kolekcji historycznych wydań.

`DatabaseBackupVerificationTests` używa własnych plików: rzeczywistych kopii, syntetycznych schematów 0–3, specjalnych znaków URI, read-only, uszkodzonych/uciętych/pustych plików, nowszego i niezgodnego schematu, dzienników, zmienionych metadanych, anulowania, zniknięcia pliku, blokady Windows i dowiązania Unix. Porównuje bajty przed/po i brak nowych plików; prawdziwa kopia jest sprawdzana obok działającej bazy fixture'a, której dane i bajty pozostają zachowane.

UiSmoke `--database-backups` obejmuje PL/EN, istniejące tworzenie/katalog/ścieżki, nową ikonę i etykietę, wynik poprawnej kopii, uszkodzony i znikający plik, usuwanie starego wyniku po odświeżeniu i brak późnych aktualizacji po zamknięciu. Używa własnych katalogów i nie otwiera prywatnych kopii ani poświadczeń. Wybrane logi/TRX i zrzuty znajdują się w `artifacts/reports/backup-verification-2026-10-17`.

Wynik: po 34/34 celowane testy jednostkowe Windows i Linux, bez pominiętych; obejmują dotychczasowe kopie/katalog oraz 18 nowych przypadków weryfikacji. Kontrole blokady Windows i dowiązania Unix wykonują się tylko na właściwym systemie. Buildy harnessu bez ostrzeżeń/błędów; jeden celowany scenariusz UI na każdym systemie przeszedł w PL/EN. Kontrola treści/linków repozytorium obejmuje 646 plików. Sprawdzono wygląd komunikatu w wąskim oknie ustawień; pełnej macierzy ani releasu nie ponawiano.

Natywne sprawdzenie SQLite jest synchroniczne na workerze; anulowanie sprawdzamy przed i po odczycie, bez deklaracji natychmiastowego przerwania dużej kontroli. Zamknięcie ustawień czeka na tę pracę. Przywracanie pozostaje ręczne: [instrukcja](../database-backups.md).
