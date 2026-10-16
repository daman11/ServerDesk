# Linux: konsola i certyfikat RDP oraz zmiana rozmiaru okna

Etap roboczy po 0.37.0, 2026-10-16. Zgłoszenie z Kali/KDE dotyczyło ogólnego błędu TLS/NLA przy uruchamianiu RDP oraz braku możliwości przeciągania krawędzi głównego okna. Nie łączono się z zapisanym serwerem maintenera i nie używano jego poświadczeń.

## Poprawka

- FreeRDP nadal działa jako zewnętrzny klient w interaktywnym PTY. Usunięto `/from-stdin:force`, który blokował pytanie o niezaufany certyfikat. Pozostawiono NLA i normalną weryfikację certyfikatu; nie dodano automatycznego zaufania, hasła w argumentach ani obniżania zabezpieczeń. Uzasadnienie i źródła upstream są w [instrukcji Linux](../platform-support.md#rdp-błąd-tlsnla-i-pytanie-o-certyfikat).
- Po błędzie klienta konsola pozostaje widoczna. Przełącznik Konsola RDP / Podsumowanie RDP zachowuje bufor również po zamknięciu procesu. Start klienta nadal nie oznacza uwierzytelnionego pulpitu.
- Linux otrzymał osiem obszarów resize przy krawędziach/narożnikach, kursory i widoczny uchwyt w prawym dolnym rogu. Obszary mają role dekoracji oraz obsługę `BeginResizeDrag`; znikają po maksymalizacji lub wyłączeniu `CanResize`. Pozostawiono własny pojedynczy pasek tytułu i dotychczasowe limity geometrii. Windows nie otrzymuje dodatkowych obszarów.
- Etykiety, pomoc i błędy są w PL/EN. Backend poświadczeń, protokoły, formaty zapisu i zarządzanie procesami pozostały bez przebudowy; przygotowano tylko buildy deweloperskie, bez podnoszenia wersji i wydania.

## Wykonane sprawdzenia

- **37/37** celowanych testów Windows: `RdpTests` i `LocalizationTests`. Parametry zachowują NLA, rozdzielczość, login/domenę, schowek i brak hasła/obejścia certyfikatu; odrzucają tryb `from-stdin`.
- Windows `--rdp-pane`: dotychczasowe start/stop/anulowanie/reconnect, stany zewnętrznego klienta, oczekiwanie na sprzątnięcie procesu podczas zamykania aplikacji, edytor trybu i odzyskanie po braku osadzonego hosta. Układ 520/1100 px, tekst 13/16. Procesy testowe nie uwierzytelniają Windows RDP.
- Ubuntu/WSL `--linux-desktop`, PL/EN: trafianie wskaźnika w osiem krawędzi w aktualnej klatce renderera przy skalach 100/150/200%; maksymalizacja/przywrócenie i `CanResize`. Obejrzano uchwyt i układ głównego okna.
- Ten sam test uruchamia własny skrypt w rzeczywistym PTY, czeka na wejście, wypisuje 180 linii i kończy się kodem 133. Konsola i przewijanie pozostają dostępne, przełączenie podsumowania zachowuje szczegóły, reconnect uruchamia nowy proces, zamknięcie go zatrzymuje.
- **Rzeczywisty FreeRDP 2.11.5**, wywołany przez produkcyjny `SessionPane` z produkcyjnymi parametrami: własny endpoint na losowym porcie loopback odpowiada na negocjację NLA i przedstawia jednorazowy niezaufany certyfikat TLS. Klient pokazuje `Y/T/N`; test wysyła `N`, klient kończy się błędem, konsola zostaje widoczna. Endpoint nie otrzymał danych aplikacyjnych CredSSP. Nie zapisano zaufania ani danych kont Windows. Listener, certyfikat i proces należą wyłącznie do fixture.
- Kompilacje harnessów bez ostrzeżeń. Odczyt repozytorium, linki i diff sprawdzone; offline `--linux-desktop` dodano do przyszłego CI Linux. Pełnej macierzy i nowego release nie uruchamiano dla tej celowanej poprawki.

Wybrane logi, TRX i zrzuty: `artifacts/reports/linux-rdp-resize-2026-10-16/`. Bazy i pliki robocze testów są odseparowane od danych aplikacji i nie należą do dowodów. Polecenia odtworzenia są w [instrukcjach rozwoju](../development.md).

## Granice weryfikacji

Nie wykonano fizycznego przeciągania okna w natywnym KWin/Kali ani logowania do pełnego Windows RDP z Linuksa. Nie uruchamiano lokalnie FreeRDP 3/SDL/Wayland; zgodność pytania o certyfikat sprawdzono także w źródle upstream, co nie zastępuje próby tych binariów. Ogólny błąd ze zrzutu nie rozstrzyga wszystkich możliwych problemów TLS/NLA. Nowy build pozwala odpowiedzieć na certyfikat i odczytać szczegóły, ale inne błędy konta lub konfiguracji serwera wymagają dalszej diagnostyki.
