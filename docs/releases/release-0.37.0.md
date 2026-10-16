# ServerDesk 0.37.0 — Public Beta

Wydanie stabilizuje dane, podstawowy panel połączeń i SFTP przed wersją 1.0. Obejmuje zmiany rozwijane po 0.36.2. Nadal jest publiczną betą na licencji MIT.

## Połączenia i foldery

- Wybrany folder pozostaje zaznaczony po zmianie sortowania, kierunku i odświeżeniu danych.
- Wyszukiwanie, filtr protokołu i Ulubione usuwają zaznaczenie ukrytego profilu. Enter i podwójne kliknięcie sprawdzają aktualny filtr również przed przebudowaniem drzewa.
- Rozwijanie wyników wyszukiwania jest tymczasowe. Po wyczyszczeniu zapytania i restarcie wraca zwykły stan zwinięcia folderów; wybór sortowania pozostaje zapisany.

## Dane i odzyskiwanie

Ustawienia → Bezpieczeństwo pozwalają utworzyć sprawdzoną kopię SQLite. Starszy schemat jest kopiowany przed migracją; nieudana kopia blokuje migrację. Kopia obejmuje zatwierdzone strony WAL. Uszkodzona, niedostępna lub nowsza baza nie jest zastępowana pustą konfiguracją. [Odtwarzanie i ograniczenia](../database-backups.md). Format bazy pozostaje w wersji 3; kopia nie obejmuje systemowego magazynu haseł ani kluczy prywatnych.

## SFTP

Ręczne Odśwież/F5 odzyskuje wykryte zerwanie osobnego połączenia plikowego, zachowując terminal, katalogi, filtry i historię. Host jest ponownie weryfikowany. Zakończona kopia zwalnia slot przed odczytem listy, a błąd odświeżania nie zmienia sukcesu transferu.

Komunikaty błędów, anulowania i ponawiania mają wskazówki PL/EN. Poprawiono chmod. Gdy nie potwierdzono sprzątania `.part`, zadanie pokazuje dokładną ścieżkę oraz ostrzeżenie zachowane po udanym retry. Filtr ostrzeżeń i wyszukiwanie pełnego opisu ułatwiają jego znalezienie. Sprzątanie podmienionego dowiązania usuwa sam wpis, zachowując cel.

Ponów zaczyna plik od początku i zachowuje potwierdzenie nadpisania. Nie ma automatycznego zbiorczego usuwania starych `.part`; historia dotyczy otwartych kart.

## Instalacja i zakres

Paczki: Windows x64 Setup/ZIP, Linux x64 DEB/TAR.GZ i źródła ZIP. Instalator nie zamyka działających sesji; przed aktualizacją zamknij aplikację i zabezpiecz dane. Nie nadpisuj plików starszego wydania. Wydanie nie ma podpisu Authenticode.

[Instrukcja PL](../getting-started.md), [English guide](../getting-started.en.md), [zakres weryfikacji](../reports/release-verification-0.37.0.md), [pozostałe ograniczenia](../known-issues.md), [plan 1.0](../stable-release-plan.md). Testy WSL nie zastępują natywnego Linux, fizycznego Serial, mieszanych DPI ani pełnej macierzy RDP. Przygotowanie lokalnego wydania nie oznacza publikacji w GitHub.
