# Odświeżanie osadzonego RDP Windows — 2026-10-21

Mała poprawka prezentacji po 0.44.0, po zgłoszeniu zatrzymanego obrazu przy wyłączeniu ekranu zdalnego Windows. Bez nowego releasu, zmian schematu, zależności, poświadczeń i konfiguracji zdalnego komputera.

## Ustalenie i zmiana

**Sprawdź serwer** pokazuje wiersz pracy w panelu. Zmiana geometrii uruchamia dotychczasową obsługę rozmiaru osadzonego pulpitu, więc może przy okazji odświeżyć obraz. Sam przycisk wykonuje diagnostykę RDP/TLS, a nie operację naprawy renderowania. Na podstawie kodu i zgłoszenia jest to wyjaśnienie możliwego efektu; nie odtworzono dokładnego przypadku wyłączenia zdalnego ekranu.

Dodano osobne odmalowanie własnego HWND RDP i jego potomków przez `RedrawWindow`, bez synchronicznego przetwarzania paint, zmiany rozmiaru panelu i globalnego odświeżania pulpitu gospodarza. Samo `InvalidateVisual` Avalonia nie obejmuje całej natywnej powierzchni. Widoczna zalogowana kontrolka żąda odmalowania co pięć sekund, po aktywacji okna i podczas oddania fokusu. Ukryte, zminimalizowane, nieutworzone i zwolnione kontrolki są pomijane. Timer startuje po zdarzeniu zalogowania/powrotu, zatrzymuje się przy reconnect, odpięciu i rozłączeniu; ponowne dołączenie zalogowanej kontrolki go wznawia. Subskrypcja aktywacji należy do bieżącego okna i jest usuwana przy odpięciu/dispose.

**Pulpit** odmalowuje widok i oddaje fokus do tej sesji; przy dynamicznej rozdzielczości wymusza ponowne wysłanie bieżących parametrów nawet przy niezmienionym rozmiarze. Nie przełącza tej preferencji i nie zmienia stałej rozdzielczości po jej wyłączeniu. Powrót do karty uwzględnia teraz także fokus natywnego hosta Windows, analogicznie do istniejącej ścieżki Linux.

Nie dodano sondowania sieci w tle, reconnect na podstawie dostępności portu, zmian statusu na podstawie odmalowania ani sztucznego wejścia zdalnego. Zachowano keepalive i istniejące reguły ponownego połączenia.

## Lokalne sprawdzenie Windows

- Build aplikacji i UiSmoke Windows x64 self-contained: **PASS**, bez ostrzeżeń i błędów.
- **63/63 PASS**, bez pominiętych, filtrem `FullyQualifiedName~RdpTests|FullyQualifiedName~RdpDisplayTests|FullyQualifiedName~LocalizationTests`.
- **UiSmoke `--native-rdp --refresh-only`: PASS** — rzeczywisty host ActiveX bez połączenia. Win32 `GetUpdateRect` potwierdza unieważnienie natywnego okna, bez zmiany HWND lub stanu połączenia. Ukryty, nieutworzony i zwolniony host odrzuca repaint; ponownie widoczny zachowuje ten sam uchwyt. Rzeczywiste zarejestrowane callbacki zalogowania/rozłączenia są wywoływane syntetycznie do sprawdzenia startu/stopu timera; nie oznacza to uwierzytelnienia ani pomiaru rzeczywistej transmisji. Zachowanie HWND przy odpięciu/ponownym dołączeniu i zwolnienie przy dwukrotnym dispose również przechodzą.
- **UiSmoke `--rdp-pane`: PASS** — preflight, proces zewnętrzny, potwierdzenie/anulowanie rozłączenia, ponowienie, zamknięcie aplikacji, brak duplikatu uruchamiania, niedostępny endpoint, zapis ustawień, odzyskiwanie po niedostępnym hoście natywnym i układ 520/1100 px przy czcionkach 13/16. Wyłącznie własny proces i dane, bez uwierzytelniania RDP.

Build do lokalnego uruchomienia: `artifacts/dev-harness/ServerDesk.App.exe`. Wybrane dowody: `artifacts/reports/rdp-refresh-2026-10-21/`; odtwarzalne katalogi robocze: `artifacts/rdp-refresh-work/`.

## Granice

Nie uruchomiono VM ani zapisanych połączeń maintenera. Nie potwierdzono rzeczywistego monitora OFF, blokady/uśpienia zdalnego Windows, zaniku sieci, powrotu dekodera obrazu, fizycznego DPI i schowka przez serwer. Lokalne odmalowanie nie budzi komputera, nie dowodzi sprawności połączenia ani otrzymania nowej klatki. Wymaga próby na zgłoszonym stanowisku; dotychczasowa [macierz RDP](../rdp-test-matrix.md) pozostaje otwarta w tych obszarach.

Źródła: [Win32 RedrawWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-redrawwindow), [UpdateSessionDisplaySettings](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclient9-updatesessiondisplaysettings).
