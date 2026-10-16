# ServerDesk 0.26.0 — weryfikacja 2026-10-15

## Zakres

Odświeżenie ekranu startowego, wyszukiwanie ustawień oraz nagłówki aktywnych paneli SSH/SFTP. [Opis obsługi](../releases/release-0.26.0.md). Dodatkowo wysokości wierszy i nagłówków plików dostosowują się do czcionki/odstępów. Zmiany PL/EN korzystają z dotychczasowych motywów i mechanizmów logowania, sortowania oraz zapisu ustawień. Brak nowych zależności, schematu danych i implementacji protokołów.

Główne pliki: `WorkspaceWelcome.cs`, `WorkspaceLayout.cs`, `ApplicationSettingsDialog.cs`, nowy `SettingsSearchIndex.cs`, nowy `SftpPanelChrome.cs`, `SessionAppearance.cs`, `FilePresentation.cs`, `DesignSystem.cs`, `InterfacePreferences.cs`, katalogi PL/EN oraz nowy `tools/ServerDesk.UiSmoke/DesignRefreshChecks.cs`.

## Wyniki testów

- **Windows: 441/441 testów jednostkowych**, zero pominiętych. Kompilacja rozwiązania i UiSmoke Release: zero błędów/ostrzeżeń. Katalogi tłumaczeń i istniejące testy backendu przeszły bez zmian.
- **Dedykowany UI Windows i Linux/WSLg `--design-refresh`:** pusta baza bez fikcyjnej historii, rzeczywiste liczniki i trzy najnowsze połączenia; zmiana nazwy/usunięcie, zachowanie kontrolek po niezmienionym odświeżeniu. Renderowanie nie odczytuje poświadczeń. Kliknięcie przechodzi do normalnego promptu; anulowanie nie tworzy sesji. Nazwa użytkownika `Anuluj` nie nadaje przyciskowi semantyki Cancel/Default.
- Ekran startowy: równe kafelki w każdym wierszu, dwie kolumny przy oknach 900/1320 px i jedna przy szerokim panelu bocznym 500 px; przewijanie zapewnia dostęp do każdej z czterech akcji. Font16, PL/EN, Graphite/Light. Zrzuty: 34 na każdym systemie, łącznie z ustawieniami i SFTP.
- Ustawienia: wyszukiwanie CPU w innej kategorii, `polaczen` bez polskich znaków, angielskie zapytanie wielowyrazowe, brak wyników, czyszczenie przyciskiem/Escape, Ctrl+F, zachowanie draftu czcionki terminala, przywrócenie kategorii/opisów i anulowanie podglądu. Niepoprawny akcent ukryty filtrem zostaje pokazany przy zapisie; nie jest zapisywany. Przy 680×500 wyszukiwarka, pusty stan i akcje mieszczą się w oknie.
- SFTP: kliknięcie lokalnego/zdalnego nagłówka wybiera właściwą stronę i fokus listy, zachowuje zaznaczenie; nazwa zdalna aktualizuje się po edycji profilu. Sprawdzone granice nagłówków i wierszy font16 przy 650/1400 px, kompaktowych/przestronnych odstępach, PL/EN, Graphite/Light. W wąskim obszarze widoczna jest aktywna lista.
- **Rzeczywisty SSH/SFTP Windows i Linux:** własny tymczasowy OpenSSH, klucz z passphrase, transfer 1 MiB w obie strony z porównaniem zawartości. Niezależne filtry/sortowanie/zaznaczenie, naturalne nazwy i klikalne kolumny, kierunek po odświeżeniu, nawigacja/skróty, odmowa nadpisania, anulowanie kolejki i aktywnego pytania, ponawianie na pierwotny cel, odzyskanie błędu, zwijanie/czyszczenie i limit historii. Serwer testowy zatrzymano po obu przebiegach.
- **Regresja Windows:** pełny główny UiSmoke — pięć motywów, kontrast tekstu, SVG/ikony, klawiatura/wskaźnik, filtry, 900×600 i renderer 150/200%, ustawienia, ścieżki SFTP, personalizacja, split/floating/redock, odzyskiwanie bez automatycznego logowania, przełącznik sesji i adapter procesu RDP. Dodatkowe `--settings-layout`, `--sorting`, `--app-polish`, `--visual-surfaces`, `--sftp-focus`, `--transfer-overview`, `--protocols`, `--network-tools` przeszły. Obejmują zapis/anulowanie ustawień, responsywność sortowania przy blokadzie SQLite, cztery protokoły, prawdziwy lokalny VNC i zerwanie połączenia, brak portu Serial, przywrócone karty/transfery oraz wszystkie dziewięć Network Tools.
- **Linux: 30/30 współdzielonych przypadków sortowania oraz 66/66 przypadków Network Tools** (`--sorting-backend`, `--network-backend`), te same źródła testów backendu uruchomione w samodzielnym UiSmoke bez SDK Linux. Zwykły użytkownik Ubuntu 24.04.4/WSL.

Logi regresji i paczek: `artifacts/design-0260-*.txt`; obrazy Windows w `artifacts/design-0260-refresh`, Linux w `artifacts/design-0260-linux-refresh`; jednostkowe TRX w `artifacts/test-results/unit.trx`. Testy mają własne bazy/katalogi i nie korzystają z haseł ani prywatnych serwerów użytkownika.

## Gotowe paczki

- Windows EXE **0.26.0.0**: prawidłowy manifest Windows 10/11, asInvoker i uiAccess=false. W gotowej aplikacji natywny host RDP zachowuje HWND przy split/floating/redock/resize i zwalnia go po zamknięciu. Bez logowania do zdalnego pulpitu. Raport: `artifacts/design-0260-native-rdp.json`.
- Trzy odizolowane starty gotowego EXE potwierdzają poprawienie pozycji poza ekranem, przywrócenie maksymalizacji, fallback po nieobecnym monitorze i czyste zamknięcie. Nowy proces Linux 0.26.0 otworzył natywne okno WSLg; proces i dane testu posprzątano.
- Instalator: instalacja w ścieżce ze spacjami i polskimi znakami, uruchomienie, zgodność każdego bajtu payloadu, aktualizacja/zachowanie danych, odmowa aktualizacji/deinstalacji działającego programu i usuwanie tylko własnych plików. Użyto `/NOREGISTER`; istniejąca rejestracja i skróty użytkownika zostały zachowane.
- Linux TAR/DEB: wszystkie **329 plików** zgodne bajtowo z payloadem, wersja/architektura, prawa wykonania, root ownership w DEB, launcher i desktop entry. Windows ZIP i oba payloady mają prawidłowe metadata, pełne notices/licencje i wykluczone dane prywatne/pliki kompilacji.
- Paczki do zestawu 0.26.0: Setup EXE, portable ZIP, Linux TAR.GZ/DEB, źródła MIT, opis zmian, raport i sumy SHA-256. Sześć wcześniejszych zamrożonych wydań 0.23.0–0.25.2 zachowuje poprawne sumy SHA-256. Końcowy zestaw sprawdza `scripts/test-release-content.ps1 -ReleaseDirectory artifacts/releases/0.26.0`.

## Ograniczenia

Weryfikacja Linux dotyczy Ubuntu/WSLg, nie całej macierzy dystrybucji. Skale renderera 150/200% nie zastępują testu fizycznych monitorów o mieszanym DPI. Nie wykonywano logowania do prywatnego RDP, testów sprzętowego Serial, czytników ekranu ani długotrwałych sesji produkcyjnych. Wyszukiwarka ustawień dopasowuje nazwy opcji/sekcji/kategorii, nie aktualne wartości pól. Nadal obowiązują [known issues](../known-issues.md), w tym brak podpisu zaufanego wydawcy i automatycznych aktualizacji. Pełne MVP pozostaje rozwijane.
