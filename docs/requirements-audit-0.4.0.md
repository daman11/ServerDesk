# Audyt zgodności ServerDesk 0.4.0 z wymaganiami

Data: 13 października 2026. Zakres: obie instrukcje użytkownika — pierwotna specyfikacja ServerDesk oraz kompleksowy redesign UI/UX. Sprawdzono kod aplikacji, zapisane wyniki integracji, zrzuty przed/po oraz ponownie uruchomiono kompilację, testy jednostkowe i testy renderowania/interakcji UI.

**Wniosek: projekt jest działającą wersją przedpremierową, lecz nie spełnia jeszcze wszystkich założeń. Nie ma podstaw do uznania pełnego MVP ani całego redesignu za zakończone.** Poprzedni opis „wdrożony redesign” dotyczył dostarczonych zmian 0.4.0; nie stanowi potwierdzenia wszystkich kryteriów akceptacji. Zwłaszcza kompletność design systemu została opisana zbyt szeroko.

Statusy: „spełnione” oznacza implementację z konkretnym dowodem w dostępnym środowisku; „częściowo” oznacza brak fragmentu wymagań lub niepełny zakres; „nieweryfikowane” oznacza istniejącą implementację bez odpowiedniego testu; „brak” oznacza niezaimplementowaną funkcję. Opcje wymienione jako opcjonalne lub przyszłe nie są traktowane jako obowiązkowy blokujący brak MVP.

## 14 kryteriów akceptacji redesignu

| Nr | Kryterium | Ocena | Dowód / ograniczenie |
|---|---|---|---|
| 1 | Wszystkie główne widoki używają wspólnego design systemu | Częściowo | Kolory i część stylów są centralne. Tokeny ControlHeight, SpacingSmall/Medium/Large, FontBody/Caption/Heading i BorderWidth są zadeklarowane, ale nie wykorzystane jako wspólne źródło wartości w widokach. Rozmiary i odstępy nadal są wpisywane lokalnie. |
| 2 | Jakość wizualna nowoczesnego narzędzia klasy VS Code | Częściowo; ocena jakościowa | Nowy układ i zrzuty potwierdzają duży postęp. Toolbar sesji nadal jest rozbudowany; w wąskim widoku wymaga przewijania. Nie wszystkie kontrolki i dialogi mają dopracowane wspólne stany. |
| 3 | Ergonomiczny menedżer serwerów | Częściowo | Drzewo, foldery, wyszukiwanie, menu, ulubione i ostatnie działają. Brakuje wyboru sortowania oraz pełnego widoku historii. Drag & drop jest zakodowany, bez potwierdzenia wszystkich gestów na systemach docelowych. |
| 4 | Profesjonalny, czytelny domyślny Dark | Spełnione w sprawdzonych widokach | Zrzuty Dark i kontrola kontrastu tokenów; nie jest to certyfikacja dostępności całego UI. |
| 5 | Pięć motywów i Follow System | Spełnione jako dostępne opcje | Dark, Midnight, Graphite, Nord, Light i system są w ustawieniach. Rzeczywista zmiana wyglądu systemu w trakcie pracy pozostaje nieweryfikowana. |
| 6 | Zmiana motywu bez restartu | Spełnione | Zmiana dynamicznych zasobów w istniejącym oknie, zrzuty wszystkich pięciu motywów. |
| 7 | Zapamiętanie motywu | Spełnione | Ustawienia zapisują identyfikator w SQLite; startup odczytuje ten sam klucz; test zapisu przeszło. |
| 8 | Niezależne palety terminala | Spełnione | Pięć palet; test zmiany motywu UI bez zmiany Background terminala, renderowanie ANSI/UTF-8. |
| 9 | Wygodna regulacja sidebaru i paneli | Częściowo | Sidebar ma splitter, ukrywanie i zapis; SFTP zapisuje proporcję lokalny/zdalny. Nie ma dokowania SFTP/Server Info obok działającego terminala, zapisu ostatnio aktywnego widoku ani oddzielnego ukrywania kolejki po jej otwarciu. Dokowanie nie jest jedynym dopuszczonym układem, ale pełne dostosowanie paneli pozostaje ograniczone. |
| 10 | Wszystkie podstawowe funkcje nadal działają | Częściowo zweryfikowane | Rzeczywiste SSH/SFTP/monitoring/reconnect sprawdzono. RDP sprawdzono tylko jako konfigurację/proces, a nie połączenie. Audyt ujawnił błąd edycji ścieżki SFTP. |
| 11 | Wieloplatformowość | Spełnione w zakresie zbudowanych i uruchomionych paczek | Wspólny C#/.NET/Avalonia, self-contained Windows x64 i Linux x64. Uruchomienia Windows 11 i Ubuntu/WSLg; nie oznacza to weryfikacji każdej wymaganej dystrybucji. |
| 12 | Brak niespójnych wymiarów, odstępów i przypadkowych kolorów | Częściowo | Kolory widoków uporządkowano; tokenizacja geometrii/typografii jest niepełna. Kontrolki częściowo nadal wykorzystują wygląd Fluent. |
| 13 | Czytelność różnych rozmiarów i DPI | Częściowo | Renderowanie 900×600, paneli 650 px, skale 150/200% przeszło. Rzeczywiste mieszane DPI, wiele monitorów i przenoszenie okien nie zostały sprawdzone. |
| 14 | Kompilacja i dostępne testy przechodzą | Spełnione, ale zakres testów ma luki | Ponownie: 0 ostrzeżeń/błędów, 59/59 xUnit. UI smoke przechodzi dotychczasowe scenariusze, lecz nowy scenariusz audytu odtworzył błąd niepokryty dotychczasowymi testami. |

## Braki i błędy wykryte przy ponownym przeglądzie

### P1 — błąd przy edycji zdalnej ścieżki SFTP

W `src/ServerDesk.App/SessionAppearance.cs:49` breadcrumb zawsze przyjmuje zdalny root `/`, po czym wykonuje `path[root.Length..]`. Kiedy użytkownik zaznaczy całą ścieżkę i ją usunie, pusty string jest cięty od indeksu 1. Powstaje `ArgumentOutOfRangeException` w obsłudze TextChanged. To zwykła czynność edycji, nie nietypowe wejście.

**Błąd odtworzono w rzeczywistym widoku Avalonia bez serwera sieciowego.** Wynik: `CONFIRMED BUG: clearing remote SFTP path throws ArgumentOutOfRangeException in breadcrumb rendering` w `artifacts/ui-audit-results.txt`. Test diagnostyczny: `tools/ServerDesk.UiSmoke/AuditChecks.cs`. Błąd pozostaje w kodzie aplikacji 0.4.0; ten audyt nie jest wydaniem naprawczym.

### P2 — niekompletny design system i stany kontrolek

`src/ServerDesk.App/DesignSystem.cs:25` definiuje wspólne wymiary i odstępy, ale wyszukiwanie ich użyć wykazało jedynie deklaracje. Tokeny nie sterują wszystkimi widokami. Wiele lokalnych wartości typu 7/8/10/12/14/20 pozostaje w formularzach, toolbarach i kartach.

Własne reguły hover/focus/disabled/selected dotyczą przede wszystkim Button, ListBoxItem, TreeViewItem i TabItem (`DesignSystem.cs:46`). Nie przygotowano kompletnej macierzy własnych stanów dla CheckBox, ToggleSwitch, ComboBox, ScrollBar i pozostałych komponentów; wiele zachowań pozostaje po stronie Fluent. DataGrid nie jest używany — tabela SFTP to ListBox z kolumnami Grid, co samo w sobie nie jest błędem, ale nie potwierdza wykonania punktu dotyczącego stylu DataGrid. Brak własnego stanu nie oznacza braku działającego stanu dostarczanego przez framework.

### P2 — niedokończona ergonomia i prezentacja

- Czas sesji aktualizuje lokalne `state.Text` (`SessionPane.cs:73`), natomiast globalny pasek czyta `ConnectionStatus` (`WorkspaceLayout.cs:145`). Globalny pasek nie dostaje aktualnego czasu trwania.
- SFTP pokazuje ikonę/nazwę/rozmiar/datę; uprawnienia są tylko w tooltipie, brak oddzielnej kolumny typu. Dowiązanie nie otrzymuje osobnej ikony w nowym szablonie.
- Toolbar SFTP jest zestawem wielu równorzędnych akcji; w wąskim widoku zawija się. Toolbar terminala wymaga przewijania. To funkcjonalne, ale nie kończy wymaganego minimalistycznego dopracowania.
- Formularze mają grupy i etykiety, lecz oznaczenie błędu przy polu dotyczy głównie pustej nazwy/hosta i portu. Nie pokrywa wszystkich błędów loginu, adresu, klucza czy rozdzielczości.
- Brak pełnych stanów ładowania dla otwierania SFTP i pobierania pierwszych danych monitoringu; istnieją komunikaty połączenia i kolejki transferów.
- RAM pokazuje używane/całkowite i procent, ale nie podaje osobno dostępnej pamięci wymaganej w pierwotnej specyfikacji (`SessionPane.cs:380`).
- `ApplyAppearance` przechwytuje każde Exception bez logu (`SessionAppearance.cs:32`), również rzeczywiste błędy odczytu/aplikacji ustawień. Może to ukrywać problem zamiast go zgłosić.

## Zgodność z pierwotną specyfikacją aplikacji

| Obszar | Ocena | Stan |
|---|---|---|
| C#/.NET 10/Avalonia, wspólny projekt | Spełnione | App/Core/Infrastructure, DI, SQLite, Serilog, CommunityToolkit. Proponowana liczba osobnych projektów nie była sztywnym wymaganiem. |
| MVVM i oddzielenie UI od logiki | Częściowo | MainViewModel obsługuje listę i filtry; dużo orkiestracji sesji, transferów i operacji lokalnych jest nadal w MainWindow/SessionPane. |
| CRUD profili, foldery/podfoldery, ulubione, Quick Connect | Zasadniczo spełnione | Implementacja i testy repozytorium/URI; menu i drzewo w UI. Gesty D&D wymagają dodatkowych testów. |
| Historia, sortowanie, kolory i ikony serwerów | Częściowo | Historia jest zapisywana, Recent pokazuje ostatnie połączenie SSH. Brak pełnego widoku historii i wyboru sortowania. Color istnieje w modelu, nie ma jego edycji w formularzu; ikona zależy od protokołu, brak personalizacji ikony. RDP nie jest zapisywane jako potwierdzone połączenie. |
| Interaktywny SSH, PTY, UTF-8, ANSI, reconnect, izolacja | Spełnione w wykonanych scenariuszach | Rzeczywiste klucze z passphrase, nano, resize, dwie sesje, EOF/transport, odzyskiwanie po błędzie. Password auth istnieje, lecz nie ma rzeczywistego testu logowania hasłem. Vim/htop/MC/fish/zsh i szeroka macierz klawiszy pozostają nieweryfikowane. |
| Weryfikacja kluczy SSH i bezpieczny magazyn | Spełnione w sprawdzonym zakresie | Fingerprint, zapamiętanie i blokada zmiany; DPAPI i rzeczywisty GNOME Keyring. KWallet pozostaje nieweryfikowany. Brak sekretów w eksporcie/profilach. |
| Dwupanelowy SFTP i transfery | Częściowo | Upload/download, Unicode, chmod, rename/delete, katalogi, ukryte pliki, limity/postęp/anulowanie. Brak pełnej obsługi transferu dowiązań i oddzielnej operacji przenoszenia w UI. Usuwanie katalogów ograniczone do pustych. Po zerwaniu sieci zdalny plik .part może pozostać. |
| Server Info bez agenta i sudo | Zasadniczo spełnione | Rzeczywiste CPU z dwóch próbek, RAM/swap, dyski, sieć, system i CPU chart. Brak osobnej prezentacji dostępnego RAM; brak testów wszystkich dystrybucji i ograniczeń uprawnień. |
| RDP na Windows/Linux | Częściowo wdrożone, połączenie nieweryfikowane | IRdpClient, mstsc/FreeRDP, rozdzielczość, domena/NLA/certyfikaty/schowek jako konfiguracja, zarządzanie procesem. Test procesu nie jest testem logowania ani obrazu. Brak serwera potwierdzony przez użytkownika. |
| Osadzenie RDP / działający proof of concept | Brak | Jest dokument analizy i plan adaptera framebuffer; brak natywnego bridge oraz obrazu w Avalonia. Tymczasowy klient zewnętrzny jest dopuszczonym planem awaryjnym, lecz nie zastępuje wymaganego PoC docelowej integracji. |
| Wiele sesji i przeciąganie zakładek | Zasadniczo spełnione | Niezależne sesje, przełączanie/zamykanie/reconnect; D&D zakodowane, bez pełnej weryfikacji gestów. Split view jest opcjonalny i nie blokuje MVP. |
| SQLite, wersjonowanie, import/eksport, katalogi XDG | Spełnione w obecnym schemacie | user_version=1, odrzucenie nowszego schematu, tworzenie schematu i stare profile z wartościami domyślnymi; testy import/export/folderów. Nie należy tego rozszerzać na potwierdzenie przyszłych migracji. |
| Logi rotowane i bez sekretów | Zasadniczo spełnione | Serilog i ograniczenie treści; niektóre lokalne catch ukrywają błędy bez logowania. |
| Windows/Linux i wszystkie wymienione platformy | Częściowo zweryfikowane | Windows 11, Ubuntu 24.04 pod WSLg/XWayland. Brak wykonanych testów Windows 10, Kubuntu/KWallet, Debian, Fedora i niezależnego natywnego Wayland. |
| Wydajność i stabilność wielogodzinnej pracy | Nieweryfikowane w pełnym zakresie | Limity transferów, ograniczenie monitoringu, kontrolowany strumień terminala są wdrożone. Brak benchmarków setek serwerów, długich sesji, dużych katalogów i testów wycieków. Drzewo jest przebudowywane przy zmianach, więc wymaga pomiaru. |
| Dystrybucja i instalacja | Częściowo | Windows self-contained ZIP + skrypt instalacji/deinstalacji; Linux self-contained tar.gz i DEB. Brak graficznego instalatora EXE/MSI. AppImage/RPM były warunkowe/opcjonalne, więc ich brak nie jest samodzielnym blokującym brakiem. |
| Funkcje przyszłe | Poza kryteriami tej wersji | Synchronizacja, bastion, forwarding, VNC, pluginy itd. nie są liczone jako braki MVP. |

## Weryfikacja w tym audycie

- `scripts/build.ps1 -Test`: kompilacja Release, 0 ostrzeżeń, 0 błędów, 59/59 xUnit.
- UiSmoke z `--audit`: pięć motywów, kontrast primary/secondary na pięciu rodzajach powierzchni, sidebar i filtry, 900×600, render scaling 150/200%, zapis wyglądu, niezależność palet; dotychczasowe scenariusze przeszły.
- Dodatkowy rzeczywisty scenariusz UI potwierdził wyjątek po wyczyszczeniu ścieżki SFTP. Jest to znalezisko audytu; zielony wynik istniejącego zestawu testów nie dowodzi braku błędów.
- Rzeczywiste integracje SSH/SFTP i monitoring nie były ponownie uruchamiane w tym audycie; sprawdzono zachowane wyniki `artifacts/ui-redesign/verification.txt` i `network-verification.txt` z poprzedniego etapu. Nie przedstawiamy tych wyników jako nowych uruchomień.
- Testy sieciowe RDP, KWallet i pozostałych systemów pozostają niewykonane. Renderowanie headless nie zastępuje testu na fizycznym monitorze ani czytniku ekranu.

## Kolejność domknięcia

1. Naprawić i pokryć regresją błąd edycji ścieżki SFTP; poprawić aktualizację globalnego czasu sesji i raportowanie błędów wyglądu.
2. Wprowadzić rzeczywiste wspólne użycie tokenów geometrii/typografii oraz uzupełnić macierz stanów kontrolek, formularzy i ładowania.
3. Domknąć ergonomię toolbarów, prezentację danych SFTP/RAM, sortowanie/historię i zakres regulacji paneli.
4. Wykonać brakujące testy SSH, D&D, wydajności, czytników ekranu, Follow System i mieszanych DPI na docelowych systemach.
5. Zrealizować PoC osadzonego RDP; po udostępnieniu serwera sprawdzić rzeczywiste RDP lokalne/domenowe, NLA/certyfikaty/schowek/input. Brak serwera ogranicza testy, nie uzasadnia oznaczania implementacji jako gotowej.
6. Zweryfikować KWallet i dystrybucję/instalację na pozostałych platformach przed ogłoszeniem pełnego MVP.

## Postęp po audycie

Powyższe tabele zachowują stan 0.4.0. Poprawki błędów, historia/sortowanie i usprawnienia prezentacji znajdują się w [release-0.5.0.md](release-0.5.0.md). W [release-0.6.0.md](release-0.6.0.md) dodano spójny zestaw wektorowych ikon oraz personalizację ikony serwera. [release-0.7.0.md](release-0.7.0.md) dodaje kolor, import własnej grafiki oraz dokowanie/split view z testem ciągłości rzeczywistego SSH. Osadzony RDP pozostaje otwarty. Aktualna lista prac: [roadmap.md](roadmap.md).
