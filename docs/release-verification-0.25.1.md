# ServerDesk 0.25.1 — weryfikacja 2026-10-15

## Zmiany i pliki

Wspólna ergonomia i stabilność istniejących funkcji: kompaktowa nawigacja ustawień, ochrona zapisu, dopasowanie dialogów do monitora właściciela, przewijanie długich komunikatów z osobnymi akcjami, zaznaczanie treści, paleta poleceń, dostęp do Network Tools z ekranu startowego, poprawne cele WoL/subnet/HTTP, szczegóły i stany wyników, blokada parametrów podczas operacji i aktualne karty/status. [Opis użytkowy](release-0.25.1.md).

Główne pliki: `Dialogs.cs`, `AppearanceDialogs.cs`, `ApplicationSettingsDialog.cs`, `ProfileEditorDialog.cs`, `FileDeletionDialog.cs`, `WorkspaceWelcome.cs`, `WorkspaceLayout.cs`, `MainWindow.cs`, `NetworkToolPane.cs`, `NetworkWorkspace.cs` i `DesignComponents.cs` w `src/ServerDesk.App`. Zaktualizowano katalogi PL/EN i dokumentację. Nowy scenariusz: `tools/ServerDesk.UiSmoke/AppPolishChecks.cs`, opcja `--app-polish`.

Brak nowych bibliotek. Protokoły SSH/SFTP/RDP/VNC/Serial i backendy Network Tools nie są przepisywane przez to wydanie. Zależności Network Tools pozostają DnsClient 1.8.0 i SharpSnmpLib 12.5.7, z notices w paczkach.

## Testy

- **423/423 testy rozwiązania na Windows**, zero pominiętych; kompilacja Release bez błędów i ostrzeżeń. Katalogi PL/EN mają zgodne klucze i zachowane placeholdery.
- **Nowy przekrojowy test UI na Windows i Ubuntu 24.04.4/WSLg:** PL/EN, Graphite/Light, ustawienia 680×500 i powrót pełnych etykiet przy 960 px, widoczne akcje wszystkich czterech kategorii. Wstrzymane asynchroniczne stosowanie ustawień potwierdza blokadę pól/przycisków i zamknięcia, a zwolnienie operacji kończy zapis.
- Paleta: brak wyników nie zamyka okna po Enter, kilka słów zawęża wyniki, ↑/↓ zachowuje fokus wyszukiwania, Enter wybiera właściwe polecenie. Długi komunikat przy wysokości 400 px zachowuje przyciski poniżej przewijanej treści, treść jest zaznaczalna, Escape zamyka okno.
- Wybór zapisanego hosta WoL zachowuje MAC i wypełnia cel IPv4; IPv6 kalkulatora i HTTP otrzymuje właściwy zapis. Nazwa karty HTTP pomija login/hasło, ścieżkę i query. Rzeczywisty Ping loopback potwierdza blokadę parametrów, Stop/odblokowanie, pełne szczegóły wiersza i zachowanie pracy po anulowaniu zamknięcia aplikacji.
- **Network Tools:** wszystkie dziewięć narzędzi przy pustej bazie, niezależne równoczesne Ping, edytowalne cele kontekstowe bez zmiany profilu, automatyczny kalkulator, zamykanie/anulowanie, istniejący split/floating i snapshot bez fikcyjnych połączeń; PL/EN, Graphite/Light, font16 i wąskie panele.
- **66/66 wspólnych przypadków backendu na Linux** przez self-contained UiSmoke `--network-backend`, jako zwykły użytkownik. Są to te same testy z dwóch plików `NetworkToolsTests.cs` i `NetworkServicesTests.cs`: własne endpointy ICMP/TCP/DNS/HTTP/TLS/SNMP v2c i UDP WoL, walidacja, obliczenia, limity, historia i kolekcje. Bez usług publicznych.
- Główna regresja UI: pięć motywów i kontrast, SVG/ikony, rzeczywiste zdarzenia klawiatury/wskaźnika w headless, sidebar i filtry, 900×600, skalowanie renderera 150/200%, ustawienia, ścieżki SFTP, personalizacja, split/floating/redock, odzyskiwanie bez odczytu poświadczeń/łączenia, przełącznik sesji oraz śledzenie końca procesu RDP z adapterem testowym.
- Osobne scenariusze: `--settings-layout`, `--visual-surfaces`, `--transfer-overview`, `--file-deletion`, `--protocols`, `--security` i `--stability`. Obejmują m.in. podgląd/anulowanie/zapis ustawień, monitoring i SFTP przy 650/1200 px, listę transferów, usuwanie na własnych danych, walidację czterech protokołów, rzeczywisty lokalny serwer VNC/piksele/klawisze/zerwanie i zatrzymaną ramkę, odblokowany UI, niepoprawną ramkę, brak portu Serial, ochronę magazynu oraz blokadę zdalnego OSC 52.

Logi i zrzuty lokalne: `artifacts/polish-0251-*.txt`, katalogi `app-polish-0251*` i `polish-0251-*`, wynik TRX w `artifacts/test-results`.

## Gotowe aplikacje i dystrybucja

- Gotowy Windows EXE **0.25.1.0**: poprawny manifest Windows 10/11, asInvoker/uiAccess=false; natywny host RDP, ciągłość HWND przy split/floating/redock/resize i zwolnienie. Bez połączenia do zdalnego RDP.
- Trzy starty opublikowanej aplikacji z własną bazą: korekta dawnej pozycji poza ekranem, przywrócenie maksymalizacji, fallback po zapisaniu położenia na nieobecnym monitorze oraz poprawne zamknięcie. Sprawdzono rzeczywiste granice natywnego okna.
- Instalator: świeża instalacja w ścieżce ze spacjami i polskimi znakami, uruchomienie, aktualizacja, ochrona zablokowanych plików, zgodność każdego bajtu z payloadem, zachowanie bazy/ustawień/logów i obcych plików, usunięcie wyłącznie plików aplikacji. Normalną rejestrację/skróty użytkownika pozostawiono bez zmian; próby wykonano w izolowanym katalogu z `/NOREGISTER`.
- Opublikowana aplikacja Linux: rzeczywisty proces i nowe natywne okno w WSLg, z własnym katalogiem danych. Proces testowy został zakończony, a jego dane tymczasowe usunięte.
- Zestaw dystrybucyjny zawiera instalator, Windows ZIP, Linux TAR/DEB, źródła MIT, release notes, raport i sumy SHA-256. Kontrola paczek obejmuje zgodność bajtową, notices/licencje, kompletność, metadata, uprawnienia Linux oraz wykluczenie prywatnych danych i plików kompilacji. Poprzednie wydania pozostają osobnymi, zachowanymi zestawami.

## Granice weryfikacji

Linux zweryfikowano w WSLg, nie na pełnej macierzy fizycznych dystrybucji. Test dopasowania do monitora nie zastępuje rzeczywistego przenoszenia między monitorami o mieszanym DPI. W tym wydaniu nie wykonywano ponownego logowania do prywatnego RDP, testów sprzętowego Serial, fizycznego WoL ani pełnego audytu bezpieczeństwa. Protokoły i ograniczenia publicznej bety pozostają w [known-issues.md](known-issues.md), a zakres narzędzi w [network-tools.md](network-tools.md). Wydanie jest bez podpisu zaufanego wydawcy i bez automatycznego aktualizatora.
