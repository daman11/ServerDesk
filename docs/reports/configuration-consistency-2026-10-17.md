# Spójność konfiguracji — sześć zadań MVP, 2026-10-17

Kolejny mały etap rozwojowy po 0.39.0 dotyczy istniejącego repozytorium konfiguracji. Bez zmiany schematu bazy, formatu JSON, zależności, systemowego magazynu poświadczeń ani cyklu sesji protokołów. Wersja aplikacji pozostaje 0.39.0; nie powstają nowe paczki wydania.

## Wykonany zakres

| Zadanie | Wynik |
|---|---|
| 1. Przenoszenie usuniętego profilu | Aktualizacja wymaga istniejącego wpisu przed tworzeniem folderów docelowych. Brak wpisu wycofuje transakcję i pokazuje komunikat PL/EN z instrukcją odświeżenia. |
| 2. Metadane przy zmianie nazwy folderu | Odczyt odbywa się po uzyskaniu blokady zapisu; SQL zmienia wyłącznie `Folder`. Zachowane są aktualne pola profilu, czas ostatniego połączenia i nieznane pola JSON. |
| 3. Niezmieniona nazwa | Istniejący folder przy tym samym celu kończy operację bez aktualizacji wierszy. Brak źródła nadal zgłasza błąd. |
| 4. Usuwanie pustego drzewa | Sprawdzenie profili i odczyt folderów należą do tej samej transakcji zapisu co usuwanie. Profil zatwierdzony przez wcześniejszego piszącego zostaje zauważony. Zachowana jest obsługa starszego niejawnego rodzica bez własnego wiersza. |
| 5. Spójny eksport | Profile, foldery i ikony czytane są jednym połączeniem w transakcji odczytu. Serializacja i zapis do pliku następują po zwolnieniu tego odczytu. |
| 6. Import przy błędzie zapisu | Zweryfikowano dotychczasowy rollback po rzeczywistym błędzie SQLite przy zapisie folderu oraz ikony, zachowanie całego poprzedniego stanu, ponowienie po usunięciu przyczyny, nowe ID profilu i zachowanie lokalnej ikony podczas scalenia. Nie wymagało to zmiany algorytmu importu. |

## Weryfikacja lokalna — Windows

- Przed poprawką: cztery z pierwszych siedmiu nowych prób wykazały błędy — brak blokady usuniętego wpisu, utratę nieznanych pól, odrzucenie tej samej nazwy oraz usunięcie folderu mimo konkurującego zapisu profilu. Import już poprawnie wycofywał oba badane błędy. Próba równoległego eksportu w tej pierwszej serii nie odtworzyła niespójnego wyniku; potrzebę wspólnego obrazu wykazał przegląd trzech oddzielnych odczytów.
- Końcowy zestaw: **56/56 PASS**, bez pominięć. Dziewięć nowych przypadków plus istniejące testy repozytorium, ikon, poprzednich transakcji, eksportów, kopii i limitu importu. Dodatkowe próby obejmują konkurującą edycję przed zmianą nazwy oraz niejawnego rodzica ze starszych danych.
- W próbach współbieżności dwie osobne sesje SQLite używają tej samej własnej bazy. Przy zmianie nazwy/usuwaniu kontrolowany zapis oczekuje na zatwierdzenie drugiej transakcji. Eksport wykonuje 80 odczytów przy 60 zmianach ścieżek i ikon przez drugie połączenie. To ograniczona próba, nie dowód wszystkich możliwych przeplotów.
- UiSmoke `--connection-browser`: **PASS PL/EN**, produkcyjne dialogi niezmienionej nazwy i przenoszenia nieaktualnego wpisu, lokalizowany błąd, brak nowych folderów i odświeżenie. Zachowane wcześniejsze sprawdzenia menu/ikon, sortowania, filtrowania, zaznaczenia i restartu.
- UiSmoke `--configuration-export`: **PASS PL/EN**, zapis, komunikaty i retry eksportu; natywne okno wyboru pliku jest pomijane. Obejrzano nowe komunikaty przenoszenia w obu językach.
- Logi, TRX i reprezentatywne zrzuty: lokalne `artifacts/reports/configuration-consistency-2026-10-17/`, poza Gitem. Istniejąca konfiguracja CI obejmuje zmieniony scenariusz drzewa i eksportu oraz nowe testy jednostkowe; zdalny CI nie był uruchamiany.

Nie korzystano z zapisanych serwerów, sekretów użytkownika ani produkcyjnych baz. Ten etap nie potwierdza akceptacji Linux/KDE, natywnych gestów drag and drop, fizycznie pełnego dysku, twardej awarii procesu, pełnych migracji historycznych danych ani wielogodzinnych pomiarów. Szersze punkty pozostają otwarte w [planie 1.0](../stable-release-plan.md).
