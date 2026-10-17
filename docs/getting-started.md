# ServerDesk 0.39.0 — pierwsze kroki

**Pluginy (0.39.0):** w Ustawienia → Pluginy włącz lub wyłącz Monitoring Linux i Komendy. Zapis działa bez restartu; wyłączenie zachowuje terminal, SFTP i własne szablony. [Obsługa](plugins.md).

**Komendy — Ctrl+Shift+K.** Korzystaj offline ze 165 wariantów poleceń albo dopasuj bibliotekę do aktywnego SSH. Dodawaj własne szablony, parametry i ulubione. Wstawianie nie wysyła Enter; wykonanie wymaga potwierdzenia. [Instrukcja](commands.md), [wydanie 0.39.0](releases/release-0.39.0.md).

W menu **Układ** wybierz **Trzy panele (1 + 2)** albo **Cztery panele (2 × 2)**. Otwarte karty rozłożą się między pustymi panelami; możesz też przeciągać je do wybranego panelu lub użyć menu karty „Dokuj do panelu 1–4”. Kliknij numer pustego panelu przed otwarciem połączenia. Separatory zmieniają rozmiary, a **Wyrównaj wielkość paneli** przywraca równy podział. Przy zmniejszeniu liczby paneli karty pozostają otwarte w widocznych grupach.

**Ustawienia → Obszar pracy**: wybierz układ startowy, miejsce nowych kart (aktywny/pierwszy/najmniej kart), wysokość zakładek, szerokość separatorów i numery paneli. Rozmiary mają podgląd; Anuluj go cofa. Układ startowy działa przy kolejnym uruchomieniu i nie łączy sesji automatycznie. [Opis wydania](releases/release-0.35.0.md).

W panelu SFTP użyj Wstecz/Dalej lub Alt+←/→, Alt+↑ dla katalogu nadrzędnego i Alt+Home dla domowego. Ctrl+L wybiera ścieżkę, Enter ją otwiera, a Escape przywraca poprzednio otwarty katalog. Historia jest osobna dla obu paneli i znika przy ponownym połączeniu całej sesji SSH.

**Od 0.37.0:** jeżeli zerwało się tylko połączenie plikowe, wybierz **Odśwież / F5** lub ponownie otwórz SFTP. Program odtworzy wykryte zerwane połączenie z zachowaniem katalogów, filtrów i historii, bez restartowania terminala. Klucz hosta jest ponownie weryfikowany. Ta czynność odczytuje listę plików; nie ponawia transferów, usuwania ani zmiany nazw. Przy utracie całego SSH najpierw połącz sesję ponownie.

Bezpłatna aplikacja open source na licencji MIT. **Publiczna beta**; zakres testów i ograniczenia: [known-issues.md](known-issues.md). English: [getting-started.en.md](getting-started.en.md).

Od 0.37.0 kolejka transferów podaje przyczynę błędu i wskazówki przed ponowieniem. Najedź na opis, aby przeczytać całość. Po poprawieniu uprawnień lub ścieżki użyj **Ponów**; przy utracie SSH najpierw połącz sesję. Ponowienie przesyła plik od początku. Ogólny błąd serwera SFTP może oznaczać m.in. uprawnienia lub brak miejsca — serwer nie zawsze podaje dokładną przyczynę.

## Windows x64

Uruchom `ServerDesk-0.39.0-win-x64-Setup.exe`. Instalator działa dla bieżącego użytkownika, dodaje skrót w menu Start i wpis na liście zainstalowanych aplikacji. Nie wymaga osobnej instalacji .NET. Dostępny jest też ZIP: rozpakuj **całe archiwum** do osobnego katalogu i uruchom `ServerDesk.App.exe`.

Wydanie nie ma podpisu zaufanego wydawcy. Pobieraj je z zaufanego źródła; nie wyłączaj ochrony Windows. Sumę paczki możesz porównać z `SHA256SUMS.txt` poleceniem `Get-FileHash .\ServerDesk-0.39.0-win-x64-Setup.exe -Algorithm SHA256`. Suma obok pliku potwierdza zgodność pobrania, ale sama nie potwierdza tożsamości wydawcy.

## Linux x64

Ubuntu/Debian: `sudo apt install ./serverdesk_0.39.0_amd64.deb`, następnie uruchom ServerDesk z menu lub poleceniem `serverdesk`. Do zapamiętywania haseł potrzebny jest `libsecret-tools` oraz odblokowany Secret Service (np. GNOME Keyring). Do RDP zainstaluj FreeRDP dla swojej dystrybucji.

Archiwum: `mkdir serverdesk-0.39.0`, następnie `tar -xzf ServerDesk-0.39.0-linux-x64.tar.gz -C serverdesk-0.39.0` i `./serverdesk-0.39.0/ServerDesk.App`. W razie braków bibliotek sprawdź zależności w pakiecie DEB lub [development.md](development.md). Testy Linux wykonano pod WSLg; pozostałe środowiska wymagają weryfikacji.

## Pierwsze połączenie

1. Kliknij `+`, wybierz SSH, RDP, VNC albo Serial i podaj parametry swojego serwera/portu.
2. Połącz. Zweryfikuj odcisk klucza SSH lub certyfikat RDP z administratorem serwera. Hasło podaj w oknie aplikacji; opcja zapamiętania jest dobrowolna.
3. Dla SSH otwórz Terminal, SFTP lub Monitoring. Kartę możesz przeciągnąć do drugiego panelu albo osobnego okna.
4. Motyw, wielkość interfejsu, terminal i język zmienisz w `Menu → Ustawienia`. **Zmiana języka wymaga ponownego uruchomienia aplikacji.** Przypomnienie wyświetla się w wybranym języku. Wybierz „Zapisz i uruchom ponownie” lub zapisz i skorzystaj później z przycisku „Uruchom ponownie” w oknie głównym. Przy otwartych kartach potwierdź rozłączenie sesji i transferów. Anulowanie pozostawia aplikację otwartą; przy następnym uruchomieniu wczyta zapisany język. Odtworzenie kart zależy od ustawienia zapamiętywania obszaru pracy i nie loguje automatycznie.

W terminalu SSH: **Ctrl+Shift+C** kopiuje zaznaczenie (bez zaznaczenia nic nie wysyła), **Ctrl+Shift+V** wkleja, a **Ctrl+C** przerywa zdalny proces. Escape działa jako klawisz zdalnego terminala, m.in. do wyjścia z trybu edycji vim. Rozmiar PTY podąża za panelem, także podczas zmiany wielkości okna. [Zmiany terminala](releases/release-0.28.0.md).

VNC nie szyfruje transmisji: używaj VPN lub istniejącego bezpiecznego tunelu. Nie wystawiaj VNC bezpośrednio do Internetu.

Ekran startowy pokazuje do trzech ostatnio używanych połączeń. Kliknięcie uruchamia zwykłe logowanie; samo otwarcie aplikacji nie łączy automatycznie. W ustawieniach użyj **Ctrl+F**, aby znaleźć opcję w dowolnej kategorii; **Escape** w polu czyści filtr. Wpisane zmiany pozostają zachowane przy wyszukiwaniu, a Anuluj cofa podgląd.

Nagłówki **Lokalny komputer** i **Zdalny · serwer** identyfikują strony SFTP. Kolor wyróżnia aktywną stronę dla operacji/skrótów; kliknięcie nagłówka przenosi fokus na listę. W wąskim obszarze przełącz stronę listą **Lokalny/Zdalny**. [Zmiany wyglądu i obsługi](releases/release-0.27.0.md).

## Operacje i usuwanie plików w panelu SFTP

**Pliki częściowe (0.37.0):** przy zerwanym uploadzie lub błędzie lokalnego sprzątania zadanie może pokazać „sprawdź plik częściowy”. Pełny opis/podpowiedź zawiera dokładną ścieżkę. Na serwerze włącz **Ukryte**, sprawdź plik i użyj zwykłego usuwania z podglądem. Program nie usuwa automatycznie starszych `.part`, które mogą należeć do innych transferów. **Ponów** zaczyna plik od początku i ponownie pyta o nadpisanie; ukończone pliki katalogu pozostają na miejscu po anulowaniu.

Ostrzeżenie o wcześniejszej próbie pozostaje w historii także po udanym ponowieniu. Nie jest to ponowne sprawdzenie istnienia pliku; po ręcznym posprzątaniu możesz wyczyścić zakończone zadanie. Filtr **Błędy, anulowane i ostrzeżenia** obejmuje takie zadania, a wyszukiwanie transferów uwzględnia także pełne opisy i ścieżki plików częściowych. Historia znika po zamknięciu karty.

**Ustawienia → Terminal → Kursor i historia**: wybierz kreskę, blok lub podkreślenie i miganie kursora. Podgląd działa od razu; Anuluj przywraca poprzedni wybór. Limit historii 0–100 000 wierszy obowiązuje przy kolejnym połączeniu SSH/Serial i nie usuwa bieżącej historii (0 wyłącza historię). Program zdalny może zmieniać kursor.

**Ustawienia → SFTP**: wskaż lokalny katalog startowy (puste pole oznacza domowy), ukryte pliki, aktywny panel i osobne sortowanie/kierunki obu stron. Opcje obowiązują przy pierwszym otwarciu SFTP w karcie. Zapis ustawień i reconnect tej samej karty zachowują wybór w przeglądarce; nowa karta użyje nowych opcji. Gdy katalog startowy jest niedostępny, aplikacja otwiera domowy i pokazuje komunikat. [Opis wydania](releases/release-0.35.0.md).

Dowiązania mają osobną akcję **Kopiuj zawartość dowiązania…** z potwierdzeniem rozwiązanej ścieżki i miejsca zapisu. Kopiowana jest zwykła zawartość, a zagnieżdżone linki są pomijane. **Szczegóły** pokazują rzeczywisty cel; Enter/dwuklik otwiera dowiązany katalog. **Utwórz zdalne dowiązanie…** tworzy link do wybranego istniejącego celu. [Dokładna obsługa i limity](releases/release-0.27.0.md).

Zaznacz pliki/katalogi i wybierz **Usuń zaznaczone…** albo naciśnij Delete na liście. Podgląd pokazuje komputer/serwer, pełne ścieżki i liczbę wszystkich wpisów, także ukrytych. Dopiero **Usuń trwale** rozpoczyna pracę. Można usunąć niepuste katalogi; cele dowiązań pozostają poza operacją. Przerwanie nie cofa wykonanych usunięć. Po błędzie/utracie połączenia sprawdź raport i odśwież listę; licznik obejmuje potwierdzone żądania. Limit podglądu: 20 000 wpisów i 64 poziomy.

## Network Tools

Otwórz **Network Tools** z lewego paska lub Menu. Wybierz narzędzie, wpisz cel, rozwiń **Parametry** i wybierz **Start**. Zapisane połączenie nie jest wymagane. Menu kontekstowe serwera może uzupełnić edytowalny adres. Każde narzędzie to niezależna karta; Escape zatrzymuje, Ctrl+Enter uruchamia, F5 restartuje. Historia nie zawiera sekretów, a pełne kolekcje korzystają z systemowego magazynu poświadczeń. Linux wymaga `iputils-ping` dla ICMP bez root. [Instrukcja narzędzi, eksportów i ograniczeń](network-tools.md).

## Aktualizacja, kopia i odinstalowanie

Zamknij sesje i aplikację. Przed aktualizacją skopiuj cały katalog danych, gdy ServerDesk nie działa: Windows `%APPDATA%\ServerDesk`; Linux `~/.local/share/serverdesk` i `~/.config/serverdesk` (lub własne ścieżki XDG). Hasła Linux są osobno w systemowym magazynie, a DPAPI Windows jest związane z kontem i komputerem. Eksport JSON przenosi konfigurację profili, **bez haseł i kluczy**.

Zmiana robocza po 0.39.0: **Menu → Eksport konfiguracji** zapisuje przygotowany JSON do pliku tymczasowego obok celu, a po zakończeniu zastępuje wybrany plik. Błąd lub anulowanie zachowuje poprzedni eksport. Przy błędzie sprawdź wolne miejsce, uprawnienia i blokadę pliku, następnie ponów. Wybierz plik ze ścieżką w systemie plików; dostawca bez takiej ścieżki pokazuje komunikat zamiast opróżniać istniejący plik. Po awarii procesu może pozostać `.part` — nie jest ukończonym eksportem. [Zakres weryfikacji](reports/configuration-export-2026-10-17.md).

Dalsza zmiana robocza po 0.39.0 pobiera serwery, foldery i ikony do eksportu z jednego obrazu bazy. Zatwierdzenie niezmienionej nazwy folderu nie zgłasza błędu. Jeśli serwer został już usunięty w innym widoku, przenoszenie pokaże komunikat — odśwież listę i ponów operację na aktualnym wpisie; nowe foldery nie zostaną utworzone. [Sześć zadań stabilizacji konfiguracji](reports/configuration-consistency-2026-10-17.md).

Instalator Windows aktualizuje instalację w tym samym katalogu. Przy ZIP/TAR rozpakuj nową wersję do **nowego katalogu**, nie mieszaj plików różnych wersji. DEB aktualizuj przez `apt install ./nowy-pakiet.deb`. Dane użytkownika są oddzielone od plików programu. Nie wracaj do starszej wersji na bazie zmodyfikowanej przez nowszą; przywróć kopię do osobnego katalogu.

Windows: odinstaluj przez Ustawienia → Aplikacje. Linux DEB: `sudo apt remove serverdesk`. Wersję ZIP/TAR usuwa się przez usunięcie jej katalogu. Dane i hasła pozostają; zasady usuwania: [privacy.md](privacy.md).

## Zgłoszenia

Podaj wersję, system, skalę ekranu, protokół i kroki odtworzenia. Logi znajdują się w katalogu danych. Przed wysłaniem usuń adresy, nazwy kont i inne dane infrastruktury; nigdy nie wysyłaj haseł, kluczy ani całej bazy. Zasady zgłaszania podatności: `SECURITY.md`.


**RDP Linux (0.38.0):** pulpit działa w karcie, a hasło i niezaufany certyfikat obsługują okna ServerDesk. Wymagany jest klient X11 FreeRDP (xfreerdp3/xfreerdp), także w sesji XWayland. [Obsługa, zaufanie i ograniczenia](rdp-integration.md).
