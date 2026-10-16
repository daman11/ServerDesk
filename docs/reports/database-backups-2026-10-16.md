# Pierwszy krok stabilizacji danych przed 1.0

Wersja rozwojowa po 0.36.2, bez zmiany schematu 3 i zależności. [Plan 1.0](../stable-release-plan.md), [obsługa i odtwarzanie](../database-backups.md).

Wdrożono ręczne kopie SQLite w ustawieniach Bezpieczeństwo, spójne odczyty z WAL, sprawdzenie struktury i wersji kopii przed nadaniem końcowej nazwy oraz kopię przed migracją istniejącej bazy 0/1/2. Błąd kopii zatrzymuje migrację. Nowa/pusta i bieżąca baza nie tworzą kopii przy każdym starcie. Niepoprawna/niedostępna/nowsza baza daje komunikat i zamknięcie bez zapisu układu zamiast resetu danych. Połączenie, którego otwarcie się nie powiedzie, jest zwalniane.

Wykonano wyłącznie celowane sprawdzenia na Windows:

- 40/40 testów: `DatabaseBackupTests`, `RepositoryTests`, `ConnectionHistoryTests`, `FolderIconTests`. W tym 13 nowych przypadków kopii/migracji/WAL/anulowania/współbieżności/odtworzenia i ochrony uszkodzonego/nowszego źródła. Błąd zapisu odtworzono przez plik blokujący utworzenie katalogu kopii; nie jest to fizyczny test zapełnienia dysku.
- UiSmoke `--database-backups`: PL/EN, wyszukanie sekcji, ręczna kopia i widoczny wynik także po wyszukaniu, błąd zapisu, zachowanie kopii po zamknięciu ustawień oraz bezpieczne zamknięcie po wykryciu uszkodzonej bazy. Wszystkie dane i ścieżki były odizolowane. Obejrzano render kompaktowego widoku EN po poprawce widoczności wyniku.
- Kompilacja aplikacji i celowanego narzędzia: 0 ostrzeżeń/błędów. Windows self-contained EXE przygotowano w `artifacts/dev-app`; obecny `dev-harness` był zablokowany przez działającą kopię użytkownika. Nie kończono jej procesu.
- Kontrola zawartości repozytorium/linków oraz `git diff --check`: PASS.

Dowody: `artifacts/reports/dev-database-backups/database-backups.trx` i cztery obrazy w podkatalogu `ui`. Syntetyczne bazy UI są usuwane po sprawdzeniu; nie stanowią materiału do publikacji.

Nie uruchamiano pełnej macierzy testów, Linux, sprzętu, rzeczywistego RDP ani całej procedury wydania. Nie zbudowano instalatora, paczek ani tagu. To pierwszy przyrost do testowania z folderu, nie wydanie 1.0. Kopia bazy nie obejmuje sekretów i zaszyfrowanych treści z magazynu systemowego; pozostaje ręczne odtwarzanie według instrukcji.
