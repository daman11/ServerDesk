# Kopie bazy danych / Database backups

Wersja rozwojowa po 0.36.2 dodaje **Ustawienia → Bezpieczeństwo → Kopie bazy danych → Utwórz kopię bazy**. Operacja działa od razu, niezależnie od Zapisz/Anuluj w ustawieniach. Po zakończeniu pokazuje pełną ścieżkę pliku. English: **Settings → Security → Database backups → Create database backup**.

Od 0.39.1 dostępne są **Zapisane kopie bazy** w tej samej sekcji. Lista pokazuje 20 najnowszych plików według czasu UTC w nazwie kopii; data jest wyświetlana w czasie lokalnym. Widać rozmiar, wersję schematu i powód (ręczna/przed migracją). Po utworzeniu nowej kopii lista odświeża się i wybiera jej plik. Ręczne odświeżenie zachowuje wybrany plik, jeśli nadal istnieje.

**Kopiuj ścieżkę** kopiuje ścieżkę zaznaczonej kopii, a gdy lista jest pusta — folderu. **Otwórz folder kopii** uruchamia systemowy menedżer plików na wyraźne kliknięcie. Pełna lista jest dostępna w folderze; ServerDesk niczego automatycznie nie usuwa. Pliki `.part`, nieznane nazwy i dowiązania plikowe nie trafiają na listę. Metadane są odczytywane poza UI, bez otwierania SQLite; widoczność pliku **nie potwierdza**, że jego zawartość jest poprawna.

0.40.0 dodaje **Sprawdź kopię**. Zaznacz plik i uruchom kontrolę: program odczyta go poza wątkiem UI, wykona `quick_check(1)` i porówna schemat SQLite z nazwą kopii. Obsługiwane są schematy 0–4, bez migracji. Wynik lub błąd pojawia się pod przyciskami. Zmiana zaznaczenia i odświeżenie listy usuwają poprzedni wynik kontroli.

Kontrola nie podmienia działającej bazy, nie naprawia kopii, nie tworzy obok niej WAL/SHM i nie odczytuje magazynu poświadczeń. Odmawia sprawdzania pliku z towarzyszącym dziennikiem SQLite jako samodzielnej kopii. Zamknięcie ustawień anuluje oczekującą pracę i czeka na zakończenie rozpoczętej kontroli; natywny odczyt SQLite może zakończyć się dopiero po bieżącym sprawdzeniu. Poprawny wynik dotyczy struktury SQLite i wersji schematu, bez potwierdzania pól profili, sekretów czy późniejszych zmian pliku. Pełna próba odtworzenia nadal wymaga kroków poniżej. [Zakres testów i ograniczenia](reports/backup-verification-2026-10-17.md).

English: **Saved database backups** lists up to 20 newest snapshot filenames with local time, size, schema and reason. Refresh preserves selection; creating a backup selects the new file. **Copy path** copies the selected snapshot or, for an empty list, the directory. **Open backup folder** uses the system file manager. The list only reads metadata, excludes partial/unknown/link entries, and does not certify or restore database contents.

0.40.0 adds **Check backup** for a selected snapshot. It checks SQLite integrity and filename/schema agreement on a worker without modifying or migrating the file, creating journal files, restoring data or accessing credentials. Schemas 0–4 are supported. Corrupt, missing, changed, newer and journal-dependent snapshots are reported. Refresh/selection changes clear the result. A successful SQLite check does not validate application fields or missing secrets; test restoration in a separate data directory.

## Zakres i przechowywanie

- Wersja deweloperska po 0.42.0: pełny reset fabryczny tworzy kopię przy planowaniu oraz świeżą kopię `before-reset` przy następnym starcie, przed wyczyszczeniem danych. Druga kopia obejmuje końcowy zapis podczas zamykania. W katalogu kopii ma opis **Przed resetem**. Błąd kopii lub transakcji zatrzymuje reset; oryginalna baza i plan pozostają do ponownej próby.
- Kopie utworzone przez tę wersję pomijają jedynie znacznik zaplanowanego resetu, aby odtworzenie nie wyczyściło danych ponownie. Dotyczy to także ręcznej kopii wykonanej przy zaplanowanym resecie. Plan w działającej bazie pozostaje bez zmian.
- Pełny reset usuwa wszystkie aktywne dane z bazy: profile z notatkami, foldery/ikony, historię, zaufanie SSH, ustawienia/tagi i odnośniki pluginów. Zabezpieczone treści DPAPI/Secret Service pozostają na tym komputerze, co umożliwia ich odczyt po przywróceniu odnośników z kopii, o ile magazyn nadal jest dostępny. Nie są odczytywane ani eksportowane do jawnego pliku. Logi, poprzednie kopie i zewnętrzne pliki (np. klucze prywatne, pliki pobrane SFTP) pozostają zachowane. To reset stanu aplikacji, bez bezpiecznego wymazywania dysku lub przenośnej kopii sekretów.
- Kopia SQLite zawiera profile, foldery, ustawienia, historię, zaufane klucze SSH oraz referencje do danych w magazynie systemowym.
- Nie zawiera haseł, prywatnych kluczy ani treści własnych komend i kolekcji przechowywanych w DPAPI/Secret Service. Referencja w starej kopii może wskazywać wpis magazynu, który został później usunięty. Sama kopia bazy nie odtwarza tego wpisu. Nie jest to pełna ani przenośna kopia wszystkich danych aplikacji.
- Metadane infrastruktury są poufne. Plik bazy nie jest szyfrowany. Nie publikuj go w zgłoszeniu ani repozytorium.
- Pliki są w `backups` w katalogu danych: Windows `%APPDATA%\ServerDesk\backups`, Linux `$XDG_DATA_HOME/serverdesk/backups` lub `~/.local/share/serverdesk/backups`. `SERVERDESK_DATA_HOME` zmienia również lokalizację kopii.
- Nazwa zawiera czas UTC, wersję schematu, powód i unikalny identyfikator. Istniejące kopie nie są nadpisywane ani automatycznie usuwane. Kopie na tym samym dysku nie chronią przed jego awarią; ważne pliki zabezpiecz też poza tym dyskiem.

## Bezpieczny zapis i migracje

Kopia powstaje przez SQLite Online Backup z transakcji odczytu. Obejmuje zatwierdzone strony WAL; nie jest kopią samego otwartego pliku `serverdesk.db`. Praca odbywa się poza wątkiem UI. Operacja natywna nie udostępnia przerwania w połowie kopiowania; anulowanie jest sprawdzane przed i po niej.

Najpierw zapisywany jest unikalny plik `.part`, następnie sprawdzane `quick_check` i wersja schematu. Dopiero zamknięta, sprawdzona kopia otrzymuje końcową nazwę `.db`. Nieudana próba sprząta własne pliki tymczasowe, zachowując wcześniejsze kopie. Po nagłym zakończeniu procesu może pozostać `.part`; nie traktuj go jako ukończonej kopii.

Przed migracją istniejącej bazy ze schematu 0/1/2/3 do 4 aplikacja musi utworzyć kopię `before-migration`. Błąd kopii zatrzymuje migrację. Nowa pusta baza i baza w bieżącym schemacie nie tworzą takiej kopii przy każdym uruchomieniu. Schemat 4 zapisuje wersję rozumiejącą nowe powiązania poświadczeń. Migracja nie przepisuje JSON-ów profili ani sekretów. Starsze aplikacje (do 0.42.0) odmawiają uruchomienia bazy 4; przed powrotem do nich odtwórz kopię sprzed migracji, po zabezpieczeniu całego katalogu danych. Baza z nowszego programu jest odrzucana bez obniżania wersji. Kontrola struktury SQLite nie potwierdza poprawności każdego pola JSON ani dostępności sekretów.

## Przywracanie

1. Zamknij **wszystkie** instancje ServerDesk. Nie podmieniaj działającej bazy.
2. Zabezpiecz cały dotychczasowy katalog danych. Zachowaj razem oryginalne `serverdesk.db` i istniejące pliki `-wal`/`-shm`; nie mieszaj ich ze starszą kopią.
3. Najpierw sprawdź wybraną ukończoną kopię `.db` w **osobnym, pustym katalogu danych**: skopiuj ją jako `serverdesk.db` i uruchom program z `SERVERDESK_DATA_HOME` wskazującym ten katalog. Nie kopiuj tam starego WAL/SHM. Sprawdź profile, foldery i ustawienia. Starszy schemat przejdzie normalną zabezpieczoną migrację.
4. Po sprawdzeniu i ponownym zamknięciu wszystkich instancji można przywrócić bazę w docelowym katalogu. Przenieś dotychczasowy plik i jego WAL/SHM do zabezpieczonego katalogu zamiast usuwać je. Umieść sprawdzoną kopię jako `serverdesk.db`.
5. Sprawdź tożsamości serwerów. Stara kopia przywraca także wcześniejsze zaufanie SSH i wcześniejsze cele profili. Przywrócenie metadanych nie oznacza przywrócenia poświadczeń; w razie potrzeby podaj je ponownie. DPAPI jest związane z kontem i komputerem, Linux korzysta z osobnego systemowego keyringu.

Jeśli baza jest uszkodzona, zajęta, niedostępna lub pochodzi z nowszego programu, komunikat startowy wyjaśnia problem. Po jego zamknięciu aplikacja kończy pracę bez zwykłego zapisu układu i preferencji. Nie usuwa bazy ani nie zastępuje jej automatycznie pustą.

English: Close every app instance, preserve the original data directory and its database/WAL/SHM files together, then test a completed snapshot as `serverdesk.db` in a separate empty data directory using `SERVERDESK_DATA_HOME`. Never mix a restored database with another database's WAL. Database snapshots exclude credentials and encrypted command/collection contents in the OS credential store. Recheck restored profiles and SSH trust before connecting.
