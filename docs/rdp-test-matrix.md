# Macierz RDP, klawiatury, schowka, D&D i DPI

## Uzupełnienie 19 października 2026 — krok roboczy po 0.43.0

Nowe RDP domyślnie otwiera się w ServerDesk. Windows obsługuje bezpośredni zrzut lokalnych plików na osadzony pulpit przez schowek i pojedyncze zdalne wklejenie. [Bieżący raport](reports/rdp-desktop-drop-2026-10-19.md) rozróżnia gest użytkownika, odbiór prawdziwych strumieni RDP i testy lokalne.

- LIVE: Windows 11, lokalne konto, 100%; osiem kolejnych zrzutów pliku i folderu do własnego odbiornika RDP, porównanie bajtów i SHA-256. USER + LIVE: użytkownik przyjął gest pliku na zdalnym polu; odbiornik potwierdził plik.
- LIVE: dokładne porównanie tekstu Unicode oraz pikseli PNG w obu kierunkach przed i po serii zrzutów; przed każdą próbą odmienna zawartość docelowa.
- AUTO/native: rejestracja/zwalnianie OLE, Unicode, Copy-only, anulowanie, niedostępna sesja, replay, błędne dane i wyjątki callbacków. AUTO/headless: strefa plików i marginesy przy 240/520/1100 px.
- DO TESTU: pełne scenariusze Eksploratora (m.in. konflikty, anulowanie, duże pliki i utrata sieci), wszystkie formaty schowka, fizyczne AltGr i japoński IME **wewnątrz ServerDesk** oraz mieszane DPI. Wcześniejsze potwierdzenie AltGr/IME z 19 października dotyczyło konsoli VMware, więc nie zalicza RDP.

## Historyczne wykonanie 0.16.0

Stan: 14 października 2026. Testy rzeczywiste dotyczą osadzonego komponentu Microsoft RDP na Windows 11 i lokalnego konta na udostępnionym serwerze Windows. Dostępny ekran: 1920×1080, 100%. To nie jest potwierdzenie wszystkich systemów ani wszystkich kombinacji sprzętu.

**LIVE**: wykonana rzeczywista próba. **AUTO**: test kodu/modelu/UI bez rzeczywistego zdalnego gestu. **USER**: próba fizycznej klawiatury potwierdzona przez użytkownika. **DO TESTU**: przygotowany scenariusz, niewykonany. Nie należy traktować AUTO ani DO TESTU jako LIVE.

## Schowek

| ID | Próba i oczekiwany wynik | Wynik |
|---|---|---|
| C01 | Lokalny tekst → Notatnik RDP; polskie znaki, emoji, tabulator, CRLF | LIVE: widoczna prawidłowa próbka |
| C02 | RDP → lokalny tekst; najpierw zastąp lokalny schowek inną próbką, potem porównaj całość | LIVE: dokładne porównanie PASS |
| C03 | Duży tekst, 4096 powtórzeń / 225 280 jednostek UTF-16, w obie strony | LIVE: dokładne porównanie PASS |
| C04 | Bitmapa 64×32, dwie kolorowe połowy → Paint RDP | LIVE: prawidłowy obraz i rozmiar |
| C05 | Zaznaczona bitmapa Paint → lokalnie, po zastąpieniu lokalnego obrazu tekstem | LIVE: bitmapa 64×32 odczytana ponownie; porównanie wszystkich pikseli jest dostępne w narzędziu do kolejnej próby |
| C06 | PNG z przezroczystością, DIB/DIBV5, duży obraz i odmienna głębia kolorów | DO TESTU: porównać piksele, alfa i rozmiary |
| C07 | Kilka plików/folderów, spacje i Unicode → zdalny Eksplorator | LIVE: plik oraz folder skopiowane do osobnego katalogu; zagnieżdżony plik otwarty z właściwą treścią |
| C08 | Pliki RDP → lokalny Eksplorator | DO TESTU: kopiowanie w drugą stronę i porównanie SHA-256 |
| C09 | Schowek OFF + reconnect, zdalne Ctrl+C nie zmienia lokalnej próbki | LIVE: lokalny sentinel pozostał bez zmian |
| C10 | Schowek OFF, lokalny tekst nie trafia do zdalnego Notatnika; strefa plików nieaktywna | LIVE: dokument nie zmienił się po Ctrl+V, strefa disabled |
| C11 | Schowek ON po wcześniejszym OFF + reconnect | DO TESTU: ponowić tekst i obraz; ustawienie wymaga nowego połączenia |
| C12 | Pusty schowek, HTML/RTF z formatowaniem, jednoczesne kopiowanie z dwóch sesji | DO TESTU: rozróżnić typ danych i aktywny cel; systemowy schowek jest wspólny |
| C13 | Nieistniejący plik, anulowanie skanowania, wybór ≥2 GiB | AUTO: odrzucenie przed zmianą schowka |
| C14 | Dowiązania/junctions i wybór ponad 10 000 elementów | Implementacja odrzuca; fizyczna próba dowiązania DO TESTU |

Po przygotowaniu plików komunikat oznacza **zawartość schowka**, a nie zakończenie przesyłania. Wybierz folder na serwerze i wklej Ctrl+V. Limit zestawu: poniżej 2 GiB, do 256 wybranych pozycji i 10 000 elementów z zawartością folderów. Transfer może zostać przerwany przez utratę sieci; po powrocie przygotuj pliki ponownie. Nie udostępniamy automatycznie wszystkich dysków.

## Klawiatura i fokus

Każdą próbę powtórzyć z klawiaturą fizyczną w trybach skrótów: lokalnie / na serwerze / tylko w pełnym ekranie. Zwykłe Ctrl+C/V pozostają operacjami zdalnej aplikacji po kliknięciu pulpitu. Pełny ekran w tym wydaniu dotyczy klienta zewnętrznego.

| ID | Próba i oczekiwany wynik | Wynik |
|---|---|---|
| K01 | Ctrl+A/C/V, Shift+litera w Notatniku | LIVE: zaznaczanie, kopiowanie, wklejanie i wielka litera działają |
| K02 | Alt+litera otwiera menu Notatnika zgodnie z językiem aplikacji | USER: Alt+F potwierdzone z fizycznej klawiatury |
| K03 | Ctrl+Alt+←/→ oddaje klawiaturę do ServerDesk | USER: Ctrl+Alt+→ potwierdzone; drugi kierunek DO TESTU |
| K04 | AltGr+ą/ę/ł/ó/ś/ź/ż i Shift+AltGr w układzie Polski programisty | DO TESTU fizycznie: próba syntetycznego Alt dawała niepoprawne wyniki, nie dowodzi błędu fizycznej klawiatury |
| K05 | Tab/Shift+Tab, Enter, Backspace/Delete, Home/End, PageUp/Down, Ctrl+Shift+strzałki | DO TESTU: tekst, pozycja kursora i zaznaczenie |
| K06 | Numpad 0–9, +/−, separator, Enter, NumLock ON/OFF | DO TESTU: znaki i nawigacja zgodne ze stanem NumLock |
| K07 | Alt+Tab/Shift+Alt+Tab, Alt+PageUp/Down, Alt+Insert, Alt+Delete | DO TESTU: poprawny lokalny/zdalny cel w każdym trybie |
| K08 | Win, Win+E/R/L, Ctrl+Alt+End | RĘCZNE: scenariusze klawiszy systemowych i ekranu zabezpieczeń; nie wykonywano ich automatycznie |
| K09 | Lokalny Ctrl+Tab po oddaniu fokusu, klik drugiego panelu, powrót do pulpitu | Model/UI pokryte wcześniej; fizyczny przebieg całej kombinacji DO TESTU |
| K10 | Przytrzymane Ctrl/Alt/Shift podczas zmiany karty, odpięcia, reconnect i minimalizacji | DO TESTU: brak pozostawionego modyfikatora oraz brak pisania do niewłaściwej sesji |
| K11 | Zapis/odczyt/eksport trybu skrótów i autoreconnect; stare profile | AUTO: zachowane ustawienia i domyślny tryb Fullscreen |

K02/K03 potwierdził użytkownik po próbach wejścia syntetycznego. Narzędzie nie odwzorowywało poprawnie kombinacji Alt w tym środowisku. Nie oznaczamy tych prób syntetycznych jako zaliczonych. Systemowe kontrolki certyfikatu obsługiwał użytkownik.

## Drag and drop

| ID | Próba i oczekiwany wynik | Wynik |
|---|---|---|
| D01 | Plik przeciągnięty gestem do strefy RDP → schowek z plikiem | LIVE: gest z systemowym formatem pliku zakończony prawidłowym komunikatem |
| D02 | Folder przeciągnięty do strefy i potem Ctrl+V na serwerze | Transfer folderu LIVE przez tę samą procedurę przygotowania; sam gest folderu DO TESTU |
| D03 | Plik upuszczony bezpośrednio na natywny pulpit | Windows: wdrożone i sprawdzone we własnym odbiorniku 19.10.2026; szczegóły powyżej. Linux: niedostępne |
| D04 | Drag przy schowku OFF, przed logowaniem i podczas reconnect | LIVE/AUTO: strefa wyłączona; brak przygotowania plików |
| D05 | Zakładka między panelami; ta sama sesja/HWND, właściwa kolejność | Model i natywne dokowanie pokryte; pełny gest DO TESTU po przerwaniu Computer Use |
| D06 | Klik krzyżyka karty, przycisku, pola tekstu lub separatora nie zaczyna drag | Poprawiona implementacja; gesty graniczne DO TESTU |
| D07 | Serwer do folderu, także po wcześniejszej edycji koloru/nazwy | AUTO: zmiana wyłącznie Folder, bez nadpisania aktualnej edycji |
| D08 | Serwer do korzenia/zagnieżdżonego folderu, Escape, drop poza celem | Model częściowo pokryty; pełne gesty DO TESTU |
| D09 | SFTP lokalnie → zdalnie, zdalnie → lokalnie, foldery i pliki z Eksploratora | Rzeczywiste transfery pokryte wcześniej; gesty DO TESTU |
| D10 | SFTP: drop po rozłączeniu, do tego samego panelu, między dwiema różnymi sesjami, symlink | Kod odrzuca niewłaściwy cel; pełne gesty DO TESTU |

Źródła zakładek/serwerów zgłaszają Move; pliki zgłaszają Copy. CaptureLost, odpięcie kontrolki i anulowanie czyszczą początek drag. Testy nowych ustawień i strefy plików sprawdzają również układ przy 240/520/1100 px.

## Utrata sieci

| ID | Próba i oczekiwany wynik | Wynik |
|---|---|---|
| N01 | RST po zalogowaniu, brak transportu przez 8 s, autoreconnect ON | LIVE: stan Ponawianie, próba 1/5, OnAutoReconnected, ten sam zdalny pulpit i otwarte dokumenty |
| N02 | Rozdzielczość i strefa plików po odzyskaniu połączenia | LIVE: strefa ponownie aktywna, serwer potwierdził 996×569 przy 100% |
| N03 | EOF podczas negocjacji, potem świeża próba | AUTO: czytelny błąd i ponowienie zakończone prawidłową negocjacją |
| N04 | Reset TCP podczas negocjacji | AUTO: polski błąd utraty połączenia |
| N05 | Serwer milczący podczas negocjacji, timeout, anulowanie | AUTO: timeout różni się od anulowania |
| N06 | Autoreconnect OFF po utracie transportu | DO TESTU LIVE: stan zakończony i ręczne Połącz ponownie |
| N07 | Brak transportu przez wszystkie 5 prób, anulowanie w trakcie, potem ręczny reconnect | DO TESTU LIVE: zwolnienie starego klienta i właściwy stan kontrolek |
| N08 | Cicha utrata pakietów po zalogowaniu, opóźnienie i krótkie flapping | DO TESTU LIVE: nie utożsamiać z resetem TCP ani timeoutem samej negocjacji |
| N09 | Zerwanie podczas kopiowania RDP/SFTP i odtworzenie połączenia | DO TESTU LIVE: sprawdzić brak niejawnego nadpisania oraz integralność plików |
| N10 | Zamknięcie/odpięcie aplikacji podczas utraty sieci | Cykl życia kontrolki/procesu AUTO/native; długotrwała awaria DO TESTU |

Test N01 korzystał z lokalnego przekaźnika przesyłającego zaszyfrowane bajty do autoryzowanego serwera. Resetowano tylko jego własne gniazda. Nie zmieniano zapory, adapterów ani konfiguracji serwera. Maksymalnie 5 prób, keepalive 10 s, timeout pojedynczego nawiązywania 15 s, całkowitego 30 s; polityka serwera może ograniczyć autoreconnect. Ustawienie domyślnie wyłączone.

## DPI i okna

| ID | Próba i oczekiwany wynik | Wynik |
|---|---|---|
| P01 | Rozmiar fizyczny = viewport × skala, 100/125/150/175/200% | AUTO: przeliczenia i limity; LIVE 100%: zdalny rozmiar równy wysłanemu |
| P02 | Równy rozmiar w pikselach przy innym DPI | AUTO: zmiana skali nie jest pomijana przez deduplikację |
| P03 | Nieparzysta szerokość, skrajny viewport, NaN/Infinity, skala niepoprawna | AUTO: parzysta szerokość, ograniczenie rozmiarów, odrzucenie błędnych danych |
| P04 | Split, odpięcie/redokowanie, zmiana rozmiaru, ponowne połączenie | Natywny cykl życia sprawdzony; resize LIVE w macierzy i we wcześniejszym wydaniu |
| P05 | Przenoszenie 100↔125/150/200% między fizycznymi monitorami | NIEDOSTĘPNE: użytkownik podał 100%, system udostępnia jeden ekran 100% |
| P06 | Odpięte okno na drugim DPI, maximized/minimized, klik czterech rogów pulpitu i drag przy separatorze | DO TESTU sprzętowo: kontrola hit testing, ostrości i braku przesunięć |
| P07 | Zmiana DPI bez zmiany fizycznego rozmiaru, odłączenie monitora podczas sesji | DO TESTU sprzętowo: obsługa ScalingChanged i geometrii okna |

Host obserwuje zmianę skalowania właściwego TopLevel i usuwa subskrypcję przy odpięciu. Po reconnect ponownie wysyła ustawienia. SmartSizing pozostaje dostępne, gdy starszy serwer odrzuca dynamiczną zmianę rozdzielczości.

## Odtworzenie

`scripts/build.ps1 -Test` uruchamia testy kodu. `ServerDesk.UiSmoke --rdp-pane` sprawdza stany, ustawienia i układ. `ServerDesk.AppProbe EXE REPORT HOST USER --interaction` uruchamia izolowane stanowisko z tekstem, obrazem i plikami syntetycznymi. Dodatkowe `--network` włącza przekaźnik i przycisk awarii 8 s. Hasło jest odczytywane bez echa i przekazywane przez pipe CurrentUserOnly, nigdy jako argument procesu.

Wyniki: `artifacts/rdp-matrix-unit-checks.txt`, `rdp-matrix-ui-checks.txt`, `rdp-matrix-interaction.json`, `rdp-matrix-network.json`, `rdp-matrix-final-input.json`, `rdp-matrix-keyboard.json`. Pierwszy raport miał końcowe Authenticated=false wskutek sprzątania; dwa jego wyniki porównania tekstu są prawidłowe. Kolejne raporty zachowują stan przed zamknięciem. Konto/certyfikat zaakceptowano wcześniej, dane schowka nie trafiają do raportu. Na serwerze pozostawiono syntetyczny katalog `Pobrane/ServerDesk matrix 2026-10-14`.

Automatyzację gestów zakończono po przerwaniu Computer Use klawiszem Escape przez użytkownika. Pozostałe komórki pozostają jawnie oznaczone. Zewnętrzne mstsc, Linux/FreeRDP, AD/Microsoft i fizyczne mieszane DPI wymagają osobnych wykonań tej macierzy.

Źródła: [skróty Windows w RDP](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclientsecuredsettings-keyboardhookmode), [oddawanie fokusu](https://learn.microsoft.com/en-us/windows/win32/termserv/imstscaxevents-onfocusreleased), [autoreconnect i wartości kontynuacji](https://learn.microsoft.com/en-us/windows/win32/termserv/-imstscaxevents--onautoreconnecting), [limit kopiowania przez schowek](https://learn.microsoft.com/en-us/troubleshoot/windows-server/remote/copying-2-gb-file-by-clipboard-redirection-fails), [parametry ekranu RDP](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-rdpedisp/ea2de591-9203-42cd-9908-be7a55237d1c).
