# ServerDesk 0.11.0 — parametry serwera, ikony folderów i wspólny nagłówek

Dolny pasek pokazuje CPU, używaną/całkowitą RAM, odbieranie i wysyłanie przez sieć oraz uptime aktywnej sesji SSH. Przy większej szerokości dochodzą zajętość systemu plików `/`, zegar w strefie serwera i nazwa dystrybucji. Przy 900 px pozostają podstawowe pola i status połączenia. Pełne opisy i czas próbki są w tooltipach. Prędkości są w KiB/s lub MiB/s; pamięć w GiB.

Monitoring startuje po połączeniu SSH, również w Terminalu i SFTP. Panel Monitoring i pasek korzystają z jednego pollera/połączenia diagnostycznego na sesję, z interwałem z ustawień (domyślnie 5 s). Nieaktywne sesje wstrzymują próbkowanie. CPU i prędkość sieci wymagają dwóch próbek. Sieć to suma zmian liczników `/proc/net/dev` interfejsów poza `lo`, z uwzględnieniem czasu między próbkami; pojawienie się interfejsu lub reset licznika nie powodują fałszywych skoków. Serwery Linux z `/proc` są wymagane; RDP nie dostarcza tych parametrów. Po rozłączeniu/reconnect wartości są czyszczone, błędy i nieaktualne próbki są oznaczane zamiast prezentowania ich jako bieżących.

Foldery mają polecenie **Zmień ikonę folderu** w menu pod prawym przyciskiem. Okno zawiera podgląd i domyślny folder oraz role serwerów z obecnego zestawu Lucide. Wybór działa dla folderów i podfolderów, zapisuje się w bazie i pozostaje po zmianie nazwy. Usunięcie folderu usuwa jego ikonę. Anulowanie edycji nie zmienia konfiguracji.

Baza jest migrowana z wersji 1 do 2 bez usuwania profili i ustawień. Eksport JSON w wersji 2 zawiera `FolderIcons`; import nadal obsługuje wersję 1. Przy scalaniu zachowuje już ustawione lokalne ikony. Nieznane poprawne identyfikatory ikon są zachowywane i wyświetlane jako folder. Starsze wydania aplikacji nie otwierają bazy/eksportu w wersji 2.

Główny pasek narzędzi zajmuje obszar tytułu okna: nazwa ServerDesk jest wyświetlana raz, obok przycisków aplikacji i systemowych przycisków okna na Windows. Pusty fragment nagłówka ma natywną rolę przeciągania, a przyciski aplikacji są oznaczone jako interaktywne. Wykorzystano API Avalonia 12: [custom title bars](https://docs.avaloniaui.net/docs/platform-specific-guides/windows). X11/WSLg nie rozszerzał obszaru tytułu w tej konfiguracji, dlatego Linux korzysta z dekoracji BorderOnly oraz przycisków minimalizacji/maksymalizacji/zamknięcia w nagłówku i obsługi przeciągania/dwukliku. Okna dialogowe i odpięte sesje zachowują swoje tytuły.

Weryfikacja:

- 128 testów xUnit: migracja starej bazy, zapis/rename/delete/eksport/import ikon i parser/różnice liczników sieci;
- `UiSmoke --workspace-details`: rzeczywiste okno wyboru ikony, zapis i anulowanie, aktualizacja drzewa, układ parametrów Midnight/Light;
- pełne UiSmoke z SSH/SFTP Ubuntu/WSL: automatyczne parametry podczas pracy w Terminalu, 900 px, docking i reconnect; log `artifacts/ui-workspace-details-full-verification.txt`, obrazy `artifacts/ui-workspace-details-full`;
- `UiSmoke --native-titlebar`: natywny Windows, stan zwykły/maksymalizacja/900 px; log i obrazy `artifacts/ui-native-titlebar`;
- natywny Linux/WSLg: odpowiednik nagłówka BorderOnly; log `artifacts/ui-native-titlebar-linux-verification.txt`, obrazy `artifacts/ui-native-titlebar-linux`;
- uruchomienia gotowych paczek i zawartość: `artifacts/release-0.11.0-windows-smoke.txt`, `artifacts/release-0.11.0-linux-smoke.txt`, `artifacts/release-0.11.0-package-check.txt`.

Testy WSLg nie zastępują macierzy rzeczywistych Kubuntu/Fedora, systemowych gestów na wielu monitorach ani mieszanego DPI. Pozostały zakres MVP: [roadmap.md](roadmap.md).

Windows: paczka ServerDesk-0.11.0-win-x64.zip, pliki w artifacts/win-x64-0.11.0. Poprzednia wersja pozostała w swoim katalogu, ponieważ była uruchomiona podczas przygotowywania wydania. Skrypt publish.ps1 -Runtime win-x64 -Versioned umożliwia kolejne publikacje obok działającej aplikacji.
