# ServerDesk 0.40.0 — Public Beta

Większa swoboda dostosowania aplikacji: wybór czcionki interfejsu, zaokrągleń kontrolek, odstępów menu i listy połączeń, adresów pod nazwami serwerów oraz widoczności ikon i przycisków zamykania kart. Opcje działają na żywo, są wyszukiwalne i przetłumaczone na PL/EN. Anuluj przywraca wygląd, a Zapisz utrwala wybory. Czcionka terminala jest niezależna; brakującą czcionkę interfejsu zastępuje domyślna. Starsze ustawienia zachowują dotychczasowe wartości.

**Ustawienia → Bezpieczeństwo → Zapisane kopie bazy → Sprawdź kopię** kontroluje SQLite i zgodność schematu z nazwą pliku. Działa poza wątkiem UI, bez modyfikowania kopii, migracji, przywracania i dostępu do haseł. Wykrywa m.in. uszkodzone/nowsze/zmienione pliki i kopie zależne od dzienników. Poprawna struktura SQLite nie potwierdza kompletności danych aplikacji ani magazynu poświadczeń; odtwarzanie pozostaje ręczne.

Network Tools pewniej obsługuje Start/Stop/Restart, opóźnione obliczenia podsieci i zamykanie kart. Stop/Escape anuluje oczekujące obliczenia; zamknięcie anuluje i rozlicza akcje, opóźnione odczyty kolekcji i okna zapisu. Wyniki zamkniętej karty nie są później publikowane.

Wydanie zawiera dotychczasowe zabezpieczenia eksportów/importu. Bez nowych zależności i migracji schema3. Windows x64 Setup/ZIP, Linux x64 DEB/TAR.GZ oraz źródła ZIP; licencja MIT. Aktualizacja instalatorem wymaga zamknięcia aplikacji i zachowuje dane użytkownika. Przed aktualizacją przygotuj kopię danych. Nadal Public Beta bez podpisu Authenticode i automatycznego aktualizatora.

[Weryfikacja i ograniczenia](../reports/release-verification-0.40.0.md), [instrukcja PL](../getting-started.md), [English guide](../getting-started.en.md), [plan 1.0](../stable-release-plan.md).
