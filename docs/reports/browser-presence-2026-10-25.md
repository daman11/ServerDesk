# Aktualizacja stanu połączeń bez przebudowy drzewa

Krok roboczy po 0.50.0, 2026-10-25. Zmiana dotyczy prezentacji przeglądarki;
nie zmienia poświadczeń, bazy ani cyklu życia transportów. Bez podnoszenia wersji
i nowego wydania, zgodnie z przyjętym lekkim cyklem rozwoju.

## Przyczyna i zmiana

Przy zmianie zbioru połączonych profili `UpdateStatus` zlecał `BuildTree`.
Przełączanie kart i dokowanie również zlecały tę przebudowę. Oznaczało to
ponowne tworzenie wszystkich elementów, menu i folderów także dla dużej listy.

Znaczniki są teraz powiązane ze stanem konkretnego profilu. Mapa stanu powstaje
tylko dla prezentowanych wierszy i jest zastępowana wraz z generacją drzewa.
Nowo prezentowany wiersz korzysta z aktualnego zbioru połączonych UUID,
więc późniejsze rozwinięcie folderu nie pokazuje starego stanu. Wiele sesji tego
samego UUID odpowiada jednemu znacznikowi. Miejsce znacznika pozostaje przed
ikoną serwera, a położenie nazwy i protokołu jest stałe.

Sortowanie, filtrowanie, edycja i odświeżenie metadanych nadal przebudowują
listę. Dotyczy to także odświeżenia historii po udanym połączeniu; poprawka
eliminuje przebudowy wywołane samym statusem i aktywacją karty.

## Celowane sprawdzenie

Windows, Avalonia Headless, własne dane syntetyczne. Przeszły cztery scenariusze:

1. `--connection-presence`: PL/EN, 5000 importowanych profili i 200 folderów,
   1000 symulowanych aktualizacji. Tożsamość drzewa, zaznaczenie, przewinięcie,
   fokus, otwarte menu i położenie protokołu pozostają zachowane. Sprawdzono
   późniejsze rozwinięcie folderu, duplikaty UUID, usunięcie ostatniego znacznika,
   wymianę mapy przy filtrowaniu oraz przełączanie/dokowanie niepołączonych kart.
2. `--connection-row-layout`: stałe położenie nazwy/protokołu i znacznika,
   SSH/RDP, ulubione, oba stany, dwa rozmiary czcionki i szerokości, Dark/Light.
3. `--connection-browser`: PL/EN, foldery i menu, sortowanie, filtrowanie,
   zachowanie poprawnego zaznaczenia, blokada Enter dla ukrytego wpisu,
   tymczasowe rozwijanie wyszukiwania i odtworzenie zwinięcia po restarcie.
4. `--terminal-focus`: kliknięcia kart, szybkie przełączanie, Ctrl+Tab, panele,
   późniejszy fokus formularza, pisanie w SFTP i izolacja przywróconych kart.

Kompilacja aplikacji i harnessu: bez błędów i ostrzeżeń. Scenariusz obecności
dodano do CI Windows/Linux; lokalne wyniki nie zaliczają zdalnego uruchomienia CI.

## Pomiar operacji na tym komputerze

| Operacja | PL | EN |
|---|---:|---:|
| Jedno przygotowanie całego drzewa 5000 profili | 2391,47 ms | 2138,51 ms |
| 1000 symulowanych aktualizacji stanu łącznie | 32,25 ms | 23,81 ms |
| Przydziały na wątku UI: jedno przygotowanie drzewa | 116 800 712 B | 116 259 536 B |
| Przydziały na wątku UI: 1000 aktualizacji łącznie | 362 944 B | 360 624 B |

Porównano różne operacje w bieżącym buildzie, nie dwie wersje aplikacji.
Pomiar aktualizacji obejmuje lokalną projekcję stanu, powiadomienia i narzut
harnessu; nie obejmuje sieci ani późniejszego renderowania. Nie jest to pomiar
RSS, fizycznego inputu ani gwarancja czasu na innym sprzęcie.

Dowody lokalne: `artifacts/reports/browser-presence-2026-10-25/` — pomiary JSON,
zrzuty i logi. Nie użyto rzeczywistych serwerów ani poświadczeń użytkownika.
Wielogodzinna praca, fizyczne DPI, czytniki ekranu i rzeczywista macierz RDP
pozostają w [planie 1.0](../stable-release-plan.md).
