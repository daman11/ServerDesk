# Bezpieczny zapis eksportu konfiguracji — 2026-10-17

Mała zmiana robocza po 0.39.0 usuwa opróżnianie wybranego pliku przed zapisem eksportu. Dotyczy **Menu → Eksport konfiguracji**, bez zmian formatu JSON, schematu bazy ani systemowego magazynu poświadczeń.

## Zachowanie

- Cały JSON jest przygotowywany w pamięci z dotychczasowym limitem 32 MiB, zanim powstanie plik tymczasowy.
- Dotychczasowy prymityw `AtomicFile` zapisuje unikalny `.part` obok celu, opróżnia bufory na dysk i zamyka plik przed podmianą nazwy. Przy zwykłym błędzie/anulowaniu sprząta wyłącznie swój plik tymczasowy. Nie usuwa starszych plików częściowych.
- Przygotowanie i zapis do pliku wykonują się poza wątkiem UI. Sukces jest zgłaszany dopiero po zakończeniu podmiany. Czytelny błąd PL/EN wskazuje wolne miejsce, uprawnienia, blokadę pliku i ponowienie.
- Dostawca pliku bez ścieżki systemu plików jest odrzucany przed otwarciem do zapisu, z komunikatem PL/EN. Nie stosujemy dla niego zapisu, który najpierw opróżnia cel. Format JSON nadal nie zawiera haseł ani zawartości prywatnych kluczy.

## Weryfikacja

- Windows: **28/28 celowanych testów PASS**, bez pominięć. Osiem nowych przypadków obejmuje utworzenie/nadpisanie i ponowny import profili Unicode oraz ikon, anulowanie przed startem, uszkodzony JSON źródła, błąd publikacji z zachowaniem niezwiązanych plików oraz blokadę Windows i retry. Pozostałe obejmują repozytorium, ikony i pięć istniejących prób prymitywu atomowego zapisu.
- Testy prymitywu obejmują błąd po częściowym zapisie, anulowanie po zapisie oraz odczyt starego kompletnego pliku do czasu publikacji nowego.
- UiSmoke `--configuration-export`: **PASS PL/EN**, zapis i komunikat sukcesu, produkcyjne okno błędu publikacji, wymóg ścieżki i udane ponowienie; własna baza i pliki. Natywne okno wyboru pliku jest pomijane. Obejrzano render obu komunikatów błędu.
- Scenariusz dodano do konfiguracji CI Windows/Linux; zdalny CI ani Linux nie były uruchamiane w tym małym kroku. Lokalne logi, TRX i zrzuty są w `artifacts/reports/configuration-export-2026-10-17/`, poza Gitem.

Nie jest to test fizycznie pełnego dysku, awarii zasilania/procesu, portalu KDE/GNOME ani wszystkich systemów plików i udziałów sieciowych. Po nagłym przerwaniu może pozostać `.part`; nie jest ukończonym eksportem. Gwarancje trwałości podmiany zależą od systemu plików. Zapis eksportów komend i narzędzi sieciowych jest osobnym zakresem; ta poprawka nie jest pełnym audytem eksportów. Szersze próby pozostają w [planie 1.0](../stable-release-plan.md).
