# Personalizacja interfejsu, listy połączeń i kart

Zmiana deweloperska po 0.39.1, bez podnoszenia wersji i pakowania releasu. Rozszerza istniejące ustawienia; nie zmienia protokołów ani schematu bazy.

Dodano siedem preferencji: krój czcionki interfejsu z listy sześciu opcji i fallbackiem systemowym, promień zaokrąglenia kontrolek 0–12 px, pionowy padding menu 2–12 px, padding wierszy połączeń 0–10 px, adresy pod nazwami oraz niezależną widoczność ikon i przycisków zamykania kart. Ustawienia listy połączeń i kart mają osobne sekcje. Zmiany mają podgląd na żywo, wyszukiwanie i tłumaczenia PL/EN; Anuluj przywraca stan, Zapisz utrwala wybory, Domyślne ustawienia obejmuje wszystkie nowe pola. Stare JSON-y otrzymują dotychczasowy wygląd bez migracji. Wskaźniki stanu pozostają, a Ctrl+Shift+W i menu karty pozwalają zamykać ją po ukryciu przycisku. Adresy współdziałają z informacją o sortowaniu bez powielania adresu.

Walidacja obejmuje docelowe testy ustawień i tłumaczeń oraz scenariusz `--extended-settings`: siedem kategorii, podgląd rzeczywistych kontrolek i nagłówków sesji/narzędzia, padding menu/drzewa, adresy i sortowanie, zachowany bufor SSH/Serial, Anuluj/Zapisz/ponowne otwarcie/przywrócenie domyślnych i wczytanie preferencji przez nowe okno. Interfejs sprawdzany jest w PL/EN, Graphite/Light, font 16, 680×500 i 960×720. Profile są odizolowane, bez połączeń z serwerami i bez poświadczeń użytkownika.

Wynik: 26/26 testów `ApplicationSettingsTests` i `LocalizationTests` na Windows; buildy harnessu Windows i Linux bez ostrzeżeń/błędów. Jeden celowany scenariusz UI na każdym systemie przeszedł obie wersje językowe, zapis i odczyt nowego okna oraz powrót do domyślnych. Sprawdzono zrzuty ustawień w szerokim i wąskim oknie; kontrola repozytorium obejmuje 647 plików. Wybrane zrzuty i logi pozostają w `artifacts/reports/customization-2026-10-17/`.

Linux sprawdzono headless na Ubuntu/WSL; nie jest to pełna akceptacja na KDE/Wayland ani sprawdzenie wszystkich kombinacji DPI i zainstalowanych czcionek. Nie powtarzano pełnej macierzy protokołów i releasu. Gotowy build deweloperski Windows przygotowano do testu z folderu.
