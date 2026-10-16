# Kopie bazy danych / Database backups

Wersja rozwojowa po 0.36.2 dodaje **Ustawienia → Bezpieczeństwo → Kopie bazy danych → Utwórz kopię bazy**. Operacja działa od razu, niezależnie od Zapisz/Anuluj w ustawieniach. Po zakończeniu pokazuje pełną ścieżkę pliku. English: **Settings → Security → Database backups → Create database backup**.

## Zakres i przechowywanie

- Kopia SQLite zawiera profile, foldery, ustawienia, historię, zaufane klucze SSH oraz referencje do danych w magazynie systemowym.
- Nie zawiera haseł, prywatnych kluczy ani treści własnych komend i kolekcji przechowywanych w DPAPI/Secret Service. Referencja w starej kopii może wskazywać wpis magazynu, który został później usunięty. Sama kopia bazy nie odtwarza tego wpisu. Nie jest to pełna ani przenośna kopia wszystkich danych aplikacji.
- Metadane infrastruktury są poufne. Plik bazy nie jest szyfrowany. Nie publikuj go w zgłoszeniu ani repozytorium.
- Pliki są w `backups` w katalogu danych: Windows `%APPDATA%\ServerDesk\backups`, Linux `$XDG_DATA_HOME/serverdesk/backups` lub `~/.local/share/serverdesk/backups`. `SERVERDESK_DATA_HOME` zmienia również lokalizację kopii.
- Nazwa zawiera czas UTC, wersję schematu, powód i unikalny identyfikator. Istniejące kopie nie są nadpisywane ani automatycznie usuwane. Kopie na tym samym dysku nie chronią przed jego awarią; ważne pliki zabezpiecz też poza tym dyskiem.

## Bezpieczny zapis i migracje

Kopia powstaje przez SQLite Online Backup z transakcji odczytu. Obejmuje zatwierdzone strony WAL; nie jest kopią samego otwartego pliku `serverdesk.db`. Praca odbywa się poza wątkiem UI. Operacja natywna nie udostępnia przerwania w połowie kopiowania; anulowanie jest sprawdzane przed i po niej.

Najpierw zapisywany jest unikalny plik `.part`, następnie sprawdzane `quick_check` i wersja schematu. Dopiero zamknięta, sprawdzona kopia otrzymuje końcową nazwę `.db`. Nieudana próba sprząta własne pliki tymczasowe, zachowując wcześniejsze kopie. Po nagłym zakończeniu procesu może pozostać `.part`; nie traktuj go jako ukończonej kopii.

Przed migracją istniejącej bazy ze schematu 0/1/2 do 3 aplikacja musi utworzyć kopię `before-migration`. Błąd kopii zatrzymuje migrację. Nowa pusta baza i baza w bieżącym schemacie nie tworzą takiej kopii przy każdym uruchomieniu. Schemat 3 pozostaje bez zmian. Baza z nowszego programu jest odrzucana bez obniżania wersji. Kontrola struktury SQLite nie potwierdza poprawności każdego pola JSON ani dostępności sekretów.

## Przywracanie

1. Zamknij **wszystkie** instancje ServerDesk. Nie podmieniaj działającej bazy.
2. Zabezpiecz cały dotychczasowy katalog danych. Zachowaj razem oryginalne `serverdesk.db` i istniejące pliki `-wal`/`-shm`; nie mieszaj ich ze starszą kopią.
3. Najpierw sprawdź wybraną ukończoną kopię `.db` w **osobnym, pustym katalogu danych**: skopiuj ją jako `serverdesk.db` i uruchom program z `SERVERDESK_DATA_HOME` wskazującym ten katalog. Nie kopiuj tam starego WAL/SHM. Sprawdź profile, foldery i ustawienia. Starszy schemat przejdzie normalną zabezpieczoną migrację.
4. Po sprawdzeniu i ponownym zamknięciu wszystkich instancji można przywrócić bazę w docelowym katalogu. Przenieś dotychczasowy plik i jego WAL/SHM do zabezpieczonego katalogu zamiast usuwać je. Umieść sprawdzoną kopię jako `serverdesk.db`.
5. Sprawdź tożsamości serwerów. Stara kopia przywraca także wcześniejsze zaufanie SSH i wcześniejsze cele profili. Przywrócenie metadanych nie oznacza przywrócenia poświadczeń; w razie potrzeby podaj je ponownie. DPAPI jest związane z kontem i komputerem, Linux korzysta z osobnego systemowego keyringu.

Jeśli baza jest uszkodzona, zajęta, niedostępna lub pochodzi z nowszego programu, komunikat startowy wyjaśnia problem. Po jego zamknięciu aplikacja kończy pracę bez zwykłego zapisu układu i preferencji. Nie usuwa bazy ani nie zastępuje jej automatycznie pustą.

English: Close every app instance, preserve the original data directory and its database/WAL/SHM files together, then test a completed snapshot as `serverdesk.db` in a separate empty data directory using `SERVERDESK_DATA_HOME`. Never mix a restored database with another database's WAL. Database snapshots exclude credentials and encrypted command/collection contents in the OS credential store. Recheck restored profiles and SSH trust before connecting.
