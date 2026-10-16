# ServerDesk 0.31.0 — responsywne SSH i odporniejszy Server Info

Odczyt i odszyfrowywanie klucza z passphrase, połączenie SSH/SFTP oraz sprzątanie po błędzie odbywają się poza wątkiem interfejsu. Żądanie anulowane przed rozpoczęciem nie czyta klucza. Anulowanie w trakcie przygotowania zapobiega połączeniu; rozpoczęta operacja biblioteki musi zakończyć odczyt lub obliczenia, zanim zwolni swoje zasoby.

Monitoring wykonuje polecenia i parsowanie w tle. Brak danych CPU, pamięci lub opcjonalnych `df`, `ip`, `lscpu` nie wyłącza pozostałych sekcji. Widok pokazuje listę niedostępnych danych i zachowuje aktualne parametry. Brak RAM oznacza „dane niedostępne”, a nie poprawne zero. Fallback do `MemFree` jest oznaczony jako przybliżenie. Po przerwie CPU i ruch sieciowy ustalają nowy punkt odniesienia.

Poprawiono walidację przepełnienia liczników i pamięci, rozpoznawanie granic sekcji oraz czyszczenie starych kart i wykresów. Nowe komunikaty działają w PL/EN. Nie dodano agenta, uprawnień sudo, nowych zależności ani migracji bazy.

Po utracie działającego połączenia monitoringu aplikacja zamyka jego klienta i po przerwie otwiera nowego. Terminal SSH pozostaje połączony. Pętla monitoringu jest wspólna dla kolejnych prób; ponowne otwarcie widoku nie uruchamia równoległego odpytywania.

[Weryfikacja i pomiary](release-verification-0.31.0.md), [pozostałe kryteria pierwotnego MVP](mvp-status.md). Publiczna beta MIT; rozszerzone testy innych dystrybucji, fizycznych klawiatur/monitorów i wielogodzinnej pracy pozostają do wykonania.
