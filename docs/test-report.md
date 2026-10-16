# Operacje na plikach — 0.24.0

357/357 xUnit. Podgląd i usuwanie grup/niepustych katalogów lokalnie i przez SFTP, PL/EN, przerwanie i częściowe wyniki, ochrona celów dowiązań, rewalidacja zakresu. Rzeczywiste OpenSSH na Windows i Linux/WSLg; regresja transferów, bezpieczeństwa i stabilności; sprawdzone paczki/instalator. [Zakres testów](release-verification-0.24.0.md) i [zmiany/ograniczenia](release-0.24.0.md).

# Publiczna beta — 0.23.0

335/335 xUnit, także po czystej kompilacji z archiwum źródeł. Sprawdzono cykl instalacji/aktualizacji/deinstalacji Windows, odmowę pracy z aktywnym procesem, zachowanie danych, rejestrację i skrót, payloady DEB/TAR oraz rzeczywiste okno Linux/WSLg. Regresja PL/EN, bezpieczeństwa, VNC/Serial i opublikowanego hosta RDP przeszła. Źródła na MIT i pełne notices zależności. [Zakres i dowody](release-verification-0.23.0.md), [ograniczenia beta](known-issues.md).

# Bezpieczeństwo i stabilność — 0.22.2

335/335 xUnit. Naprawiono oczekiwanie UI na niepełną ramkę VNC, rozpoznawanie awarii i wyścig prezentacji stanu VNC/Serial. Windows: atomowy zapis zaszyfrowanych poświadczeń, serializacja równoczesnych operacji, kontrolowane odzyskiwanie po błędzie odczytu. Linux: ograniczone strumienie procesu Secret Service, awarie i zablokowany stdin. Testy UI PL/EN i rzeczywistego VNC przeszły na Windows i Linux/WSLg; Secret Service sprawdzono w jednorazowym keyringu. [Dowody i ograniczenia](security-stability-0.22.2.md).

# Bezpieczeństwo — 0.22.1

319/319 xUnit; 57 nowych przypadków. Potwierdzone i poprawione problemy VNC, importu, nazw SFTP Windows, uruchamiania RDP oraz ponownego użycia poświadczeń. Testy UI sprawdzają też blokadę zdalnych żądań schowka. Regresja interfejsu i rzeczywisty VNC na loopback przeszły. NuGet audit nie zgłosił znanych podatności w zależnościach bezpośrednich/przechodnich. [Pełny raport, poziomy i ograniczenia](security-review-0.22.1.md).

# Zbiorcze SFTP — 0.22.0

262/262 xUnit. Zaznaczenia grup, Ctrl/Shift i prawy klik, początek drag, filtrowanie/sortowanie, Ctrl+A/Escape, liczniki, PL/EN, 650/1200 px font16 i wirtualizowana lista 1000 plików. Rzeczywisty OpenSSH/WSL: zbiorczy round trip z SHA-256, utrzymanie wyboru po odświeżeniu, ustalone źródła/cele przy zmianie paneli, limit 2, odmowa nadpisania, anulowanie/ponowienie i błąd pojedynczego elementu. Regresja wcześniejszych transferów i interfejsu przeszła. Fizycznego zakończenia gestów D&D i pełnej macierzy platform nie potwierdzono. Szczegóły: [release-0.22.0.md](release-0.22.0.md).

# Wygląd i ustawienia — 0.21.0

247/247 xUnit. Ustawienia: boczna nawigacja myszą/klawiaturą, kontrola przycinania, PL/EN, Dark/Light/Graphite, 680×500, 760×700, 960×720 i tekst 16. Regresja edytora, walidacji, głównego okna, motywów, ikon, układu i protokołów. SFTP i monitoring renderowane z jawnymi danymi testowymi w trzech motywach przy 650/1200 px. VNC na loopback: rzeczywisty obraz, wejście, dokowanie i utrata sieci; zapis języka zachowuje połączenie. Testy RDP używają własnego procesu i hosta, bez logowania do prywatnego Windows. Szczegóły: [release-0.21.0.md](release-0.21.0.md).

# Przypomnienie o restarcie i wygląd — 0.20.1

247/247 xUnit. Nowy test UI sprawdza PL/EN, widoczny komunikat przed i po zapisie języka, anulowanie, błędny zapis, ponowne otwarcie, ukrycie i powrót do aktualnego języka. Cztery kategorie ustawień: Dark/Light, tekst 16, 680×500 i 760×700. Połączenie VNC na loopback zachowuje ciągłość po zapisie i zamknięciu przypomnienia. Natywne okno Windows: 900 px i maksymalizacja, nagłówek i przypomnienie nie nakładają się na sesję. Przeszła regresja motywów, ikon, personalizacji, dokowania, ustawień i lokalizacji. Szczegóły: [release-0.20.1.md](release-0.20.1.md).

# Języki interfejsu — 0.20.0

247/247 xUnit. Zapis/anulowanie języka, zgodność starych ustawień i parametrów katalogów, angielskie główne okno, ustawienia, edytor i walidacja, historia, paleta poleceń i karty protokołów. Testy zachowują nazwę profilu Połącz i folder Ustawienia. Edytor Dark/Light, 600/780 px. Język jest stosowany po ponownym uruchomieniu. Regresja polskiego interfejsu przeszła. Szczegóły: [release-0.20.0.md](release-0.20.0.md).

# Uruchamianie i położenie okien — 0.19.1

241/241 xUnit. Błąd odtworzono w natywnym Windows; poprawka sprawdza cały prostokąt, obszar roboczy, skalowanie i osobny zapis rozmiaru normalnego oraz maksymalizacji. Test natywny potwierdza korektę starej pozycji, ponowne otwarcie po maksymalizacji/minimalizacji i widoczność odłączonej sesji. Model obejmuje kilka monitorów, ujemne współrzędne, odłączenie i skale 100–200%; fizycznej macierzy monitorów nie wykonano. Szczegóły: [release-0.19.1.md](release-0.19.1.md).

# Anulowanie i historia — 0.19.0

226/226 xUnit. Test UI potwierdza anulowanie wstrzymanych transportów SSH/VNC na loopback oraz sprawdzania RDP przez adapter, brak błędów i historii po anulowaniu, token zewnętrzny i zamknięcie potwierdzenia klucza. Rzeczywisty RFB potwierdza zapis i recency VNC. Wspólna historia: filtrowanie, Unicode, stary cel po edycji, bezpieczne ponowienie, szybkie zmiany filtrów i zamknięcie podczas wyszukiwania. Dark/Light 600/780 px, tekst 16. Szczegóły: [release-0.19.0.md](release-0.19.0.md).

# VNC i Serial — 0.18.0

214/214 xUnit. Nowe profile, walidacja/URI, zgodność starych danych, eksport/import i osobna referencja hasła VNC. Lokalny serwer RFB 3.8 sprawdza uwierzytelnianie, piksele, klawisze/mysz, utratę sieci i bezpieczne zamknięcie aktywnej sesji. UI sprawdza obraz, wejście, podgląd, filtry i dokowanie. System.IO.Ports na prawdziwym pseudo-terminalu Linux potwierdza przesył UTF-8/ANSI/Control, anulowanie odczytu i ponowne otwarcie przy 9600/115200. Osobny test terminala aplikacji sprawdza transmisję i ciągłość przy odpinaniu. Fizyczny COM/USB, konkretne zewnętrzne serwery VNC oraz szersza macierz klawiszy wymagają dalszych prób. Szczegóły: [release-0.18.0.md](release-0.18.0.md).

# Edytor i przeglądanie serwerów — 0.17.0

193/193 testów xUnit; pełna regresja headless UI oraz osobny scenariusz formularza i przeglądarki połączeń. Sprawdzono walidację i korektę pól, zapis/anulowanie, porty domyślne i własne, niewykorzystane ustawienia RDP przy SSH, podgląd koloru, wyszukiwanie metadanych, filtry protokołu, liczniki, puste wyniki i aktualizację zaznaczenia. Renderowanie formularza: Dark/Light, 600×520 i 780×720, tekst 13/16; panel boczny 220 px z tekstem 16. Szczegóły: [release-0.17.0.md](release-0.17.0.md). Nie powtarzano zdalnego logowania ani sprzętowych testów DPI dla zmian formularza.

# Schowek, klawiatura i odporność RDP — 0.16.0

183/183 testy xUnit, nowe ustawienia i układ RDP 240/520/1100 px. Rzeczywiste próby: tekst Unicode i duży tekst w obie strony, bitmapa w obie strony, plik i folder na serwerze, gest pliku do strefy, schowek OFF i powrót tego samego pulpitu po awarii transportu 8 s. Użytkownik potwierdził fizyczne Alt+F i Ctrl+Alt+→. Testy wykonywano przy 100% DPI. Pozostałe scenariusze oznaczone jawnie: [rdp-test-matrix.md](rdp-test-matrix.md). Zmiany: [release-0.16.0.md](release-0.16.0.md).

# Poprawka manifestu RDP — 0.15.1

Błąd zgłoszony przez użytkownika odtworzono przez UiSmoke.exe; odczyt zasobów opublikowanego EXE 0.15.0 potwierdził brak supportedOS. Po dodaniu manifestu test natywnego EXE tworzy i zachowuje kontrolkę po odpięciu oraz ją zwalnia. 162/162 xUnit. Test UI sprawdza lokalizację komunikatu, brak duplikatu modalnego i odzyskiwanie przez klienta zewnętrznego. Publikacja kontroluje manifest rzeczywistego EXE. Szczegóły: [release-0.15.1.md](release-0.15.1.md). Wyniki: `artifacts/rdp-0151-repro.txt`, `artifacts/rdp-0151-old-manifest-check.txt`, `artifacts/rdp-0151-native-host.txt`, `artifacts/rdp-0151-unit-checks.txt`, `artifacts/rdp-0151-ui-checks.txt`.

Opublikowany EXE 0.15.1 potwierdził rzeczywiste OnLoginComplete, zachowanie sesji przy split view/odłączeniu/redokowaniu, reconnect i zwolnienie HWND. Certyfikat zgodny z zatwierdzonym odciskiem. Wyniki: `artifacts/rdp-0151-published-host.json`, `artifacts/rdp-0151-published-login.json`.

# Osadzony RDP Windows — 0.15.0

162/162 xUnit. Rzeczywisty systemowy komponent RDP zalogował lokalne konto na udostępnionym Windows i pokazał obraz wewnątrz okna Avalonia. Test pełnego obszaru pracy potwierdził zachowanie HWND i stanu logowania przy split view, odłączeniu/redokowaniu, ukryciu i reaktywacji karty; resize, reconnect oraz zwolnienie kontrolki przy zamknięciu aplikacji. Sprawdzono regresje klienta zewnętrznego i pełny zestaw UI. Szczegóły i dalsza macierz: [release-0.15.0.md](release-0.15.0.md). Logi: `artifacts/rdp-0150-unit-checks.txt`, `artifacts/rdp-0150-ui-checks.txt`, `artifacts/rdp-0150-regression-checks.txt`. Linux nadal korzysta z osobnego okna FreeRDP.

# Diagnostyka i rzeczywiste RDP — 0.14.0

160/160 xUnit, testy UI sterowania klientem i rzeczywistego endpointu/certyfikatu. Na prywatnym Windows zweryfikowano NLA z lokalnym kontem oraz obraz pulpitu przez FreeRDP/Ubuntu/WSLg, z zatwierdzonym fingerprintem i hasłem wyłącznie na stdin. Pierwsze próby błędnie przekazywały CRLF jako część hasła; po naprawie LF logowanie przeszło. Szczegóły i ograniczenia: [release-0.14.0.md](release-0.14.0.md). Wyniki: `artifacts/rdp-ui-checks.txt`, `artifacts/rdp-desktop-session.txt`, `artifacts/rdp-real-checks.txt`. Nie deklarujemy jeszcze rzeczywistego mstsc, kont AD/Microsoft, schowka ani osadzenia obrazu.

# Pliki i kolejka SFTP — 0.13.0

145/145 xUnit. Rzeczywiste SFTP sprawdza filtrowanie/sortowanie, wybór po odświeżeniu, klawiaturę, round trip 1 MiB, pominięcia, anulowanie, retry do pierwotnego celu, odzyskanie błędu i limit historii. Renderowanie 650/1200 px, tekst 13/16. Pełny UiSmoke z rzeczywistym SSH/monitoringiem, dokowaniem i ustawieniami przeszedł. Szczegóły: [release-0.13.0.md](release-0.13.0.md). Wyniki: `artifacts/sftp-workflow-checks.txt`, `artifacts/ui-sftp-full-checks.txt`. Build zgłaszał NU1900 przy niedostępnej usłudze audytu NuGet; zależności bez zmian.

# Personalizacja i kolizje elementów — 0.12.0

135/135 xUnit. Szczegóły: [release-0.12.0.md](release-0.12.0.md). Test natywnego Windows sprawdza teraz styl własnego HWND, a testy komórek paska używają długiej nazwy Debiana i większego tekstu. Nowe ustawienia mają testy transakcyjnego zapisu, podglądu/anulowania i zastosowania do aktywnego SSH.

# Pasek parametrów i foldery — 0.11.0

128/128 xUnit, pełne UI SSH/SFTP oraz natywny nagłówek Windows (normal/maximized/900 px). Szczegóły: [release-0.11.0.md](release-0.11.0.md). Nowe scenariusze sprawdzają parametry bez otwierania Monitoringu, ikony folderów, migrację bazy i eksport/import. Pierwsze przywrócenie zależności zgłosiło NU1900 (niedostępność usługi audytu). Końcowe przywrócenie i kompilacja zakończyły się bez ostrzeżeń.

# Ładowanie i zakładki — 0.10.1

120/120 xUnit i pełne UI z rzeczywistym SSH/SFTP przeszły. Scenariusz `--session-chrome` odtwarza oczekiwanie na poświadczenia i sprawdza klatki animacji, położenie względem sesji/tekstu, usunięcie podkreślenia, fokus grup oraz dynamiczny znacznik koloru. Midnight/Dark/Light, split 900 px i renderowanie 200%: `artifacts/ui-bars-regression.txt`, `artifacts/ui-bars-verified`. Pełne wyniki: `artifacts/ui-bars-full-verification.txt`. Szczegóły: [release-0.10.1.md](release-0.10.1.md).

# Design — 0.10.0

120/120 testów xUnit. Nowy szablon płaskich przycisków sprawdzono zdarzeniami myszy i klawiatury, także dla disabled. Sprawdzono czyszczenie wyszukiwarki, układ 900×600 i kontrast tekstu stanu połączenia w Dark/Light (≥4,5:1 bez przyciemnienia). Pełne UiSmoke z rzeczywistym SSH/SFTP/monitoringiem i dokowaniem przeszło: `artifacts/ui-design-verification.txt`; obrazy: `artifacts/ui-design`. Zakres i ograniczenia: [release-0.10.0.md](release-0.10.0.md).

# Niezawodność — 0.9.0

120 testów xUnit zaliczonych. Szczegóły: [release-0.9.0.md](release-0.9.0.md). Test dwóch procesów headless sprawdza automatyczny zapis, nagłe zakończenie bez obsługi zamknięcia i odtworzenie kart bez połączeń/odczytu poświadczeń, a także anulowanie wielokrotnego żądania zamknięcia, szybkie zmiany układu i zapamiętanie usuniętych kart. Reprodukcja: `scripts/test-workspace-recovery.ps1`; wyniki: `artifacts/workspace-crash-writer.txt`, `artifacts/workspace-crash-recovery.txt`.

Rzeczywiste OpenSSH Ubuntu/WSL: anulowane wysyłanie do istniejącego/nowego celu zachowuje oryginał, nie publikuje częściowego pliku i usuwa własny plik tymczasowy przy działającym transporcie. Skrócenie pliku podczas pobierania nie nadpisuje lokalnego oryginału. Wyniki: `artifacts/network-probe-0.9.txt`. Pełne UI z rzeczywistym SSH: `artifacts/ui-reliability-verification.txt`. Uruchomienia natywne: `artifacts/release-0.9-windows-smoke.txt`, `artifacts/release-0.9-linux-smoke.txt`. Nie uznajemy za zweryfikowane RDP do Windows, przerwania transportu podczas SFTP ani dziennika sprzątania po reconnect.

# Obsługa wielu sesji — 0.8.0

116 testów xUnit zaliczonych. Zakres i ograniczenia: [release-0.8.0.md](release-0.8.0.md). Aktualne UI: `artifacts/ui-sessions-verification.txt`; obrazy: `artifacts/ui-sessions`. Scenariusz zamknięcia/odtworzenia potwierdza duplikaty kart, aktualne/usunięte profile, Quick Connect, geometrię osobnych okien, brak odczytów poświadczeń i brak automatycznych połączeń. Sprawdzono wyszukiwanie sesji, Ctrl+Shift+L/Enter, puste wyniki i wyłączenie opcji przywracania.

Rzeczywiste SSH Ubuntu/WSL potwierdza ręczne uruchomienie odtworzonej karty i brak utraty transportu po anulowaniu wymuszonego restartu. Paski akcji są widoczne w wąskich panelach. Geometria ma testy modelu/headless, nadal bez deklaracji sprzętowych testów wielu monitorów i mieszanego DPI.

# Personalizacja i dokowanie — 0.7.0

106 testów xUnit zaliczonych. Zakres oraz ograniczenia: [release-0.7.0.md](release-0.7.0.md). Aktualne wyniki UI: `artifacts/ui-workspace-verification.txt`; zrzuty: `artifacts/ui-workspace`. Sprawdzono kolor/grafikę profilu, PNG/JPEG/WebP, dwa kierunki podziału, menu dokowania, odpięte okna i zapis układu.

W rzeczywistym SSH Ubuntu/WSL obie sesje zachowały identyfikatory transportu/kontrolki terminala po split view, odpięciu i ponownym dokowaniu; polecenia działały po przeniesieniu, a drugi panel odczytywał monitoring. Gesty systemowe D&D, natywny picker plików, mieszane DPI i pozostałe platformy nie są przez to uznane za zweryfikowane.

# Ikony — 0.6.0

88 testów jednostkowych zaliczonych. Zakres i ograniczenia: [release-0.6.0.md](release-0.6.0.md). Wyniki aktualnego UI: `artifacts/ui-icons-verification.txt`; zrzuty: `artifacts/ui-icons`. Sprawdzono wszystkie 66 osadzonych ikon i formularz wyboru ikony serwera. Szczegóły źródeł i licencji: [icons.md](icons.md).

Całe UiSmoke przeszło także po zmianie ikon: pięć motywów, 150/200% render scaling, świeża baza i regresje audytu. Rzeczywiste terminal/nano, EOF/reconnect, odzyskanie po błędnym passphrase, SFTP/monitoring i dwie zakładki SSH głównego okna sprawdzono ponownie na izolowanym Ubuntu/WSL. RDP pozostało testem cyklu życia procesu, bez serwera Windows.

# Poprawki po audycie — 0.5.0

69 testów jednostkowych. Zakres poprawek i ograniczenia: [release-0.5.0.md](release-0.5.0.md). Bieżące wyniki UI: artifacts/ui-fixes-verification.txt; zrzuty: artifacts/ui-fixes.

# Redesign 0.4.0

Wyniki: [release-0.4.0.md](release-0.4.0.md). Zrzuty: artifacts/ui-redesign. 59 testów jednostkowych, pięć motywów, kontrast tokenów, skale 150/200%, zapis wyglądu i rzeczywiste sesje SSH/SFTP/monitoringu.

# Raport weryfikacji — 13 października 2026

Projekt jest wersją przedpremierową, nie zakończonym i w pełni zweryfikowanym MVP. Poniższe wyniki opisują faktycznie wykonane kontrole.

## Aktualizacja 0.3.0

59 testów xUnit zaliczonych. Dodano testy domen/UPN, rozdzielczości, bezpiecznej konfiguracji Windows/FreeRDP, włączenia folderu, domyślnych ustawień starych profili, SQLite/import/eksport RDP i cyklu życia procesu wraz ze sprzątaniem plików.

Rzeczywisty Secret Service/GNOME Keyring sprawdzono w jednorazowej sesji D-Bus. Wykonano save/read/update/delete, izolację wpisów, Unicode/whitespace/końce linii, brak backendu, timeout i anulowanie subprocessu. Dane użytkownika nie były wykorzystywane. Wyniki: artifacts/secret-service-test.txt. KWallet pozostaje niewykonany.

Pomoc testowego FreeRDP 2.11.5 i uruchomienie z opcjami przeciw nieużywanemu loopback potwierdziły obsługę argumentów i czytanie hasła przez stdin. Wystąpił oczekiwany błąd transportu. Nie wykonano RDP do Windows; użytkownik potwierdził brak testowego serwera. Test UI zakładki korzystał z kontrolowanego procesu testowego, bez symulowania obrazu/protokołu. Obraz: artifacts/ui-smoke/rdp-process-ended.png.

Szczegóły ograniczeń i wykrytych/poprawionych problemów: release-0.3.0.md.

## Aktualizacja 0.2.0

Aktualna kompilacja Release: 0 błędów, 0 ostrzeżeń. 38 testów xUnit zaliczonych (14 nowych testów danych dysków, systemu, topologii i sieci względem pierwszych 24). Parsery sprawdzają mountpointy ze spacjami, konwersję KiB, przepełnienie, brak poleceń, identyczne core IDs na różnych socketach, IPv4/IPv6/MAC, DNS i uptime.

Rzeczywiste SSH potwierdziło normalny EOF, klasyfikację nagłego zerwania połączenia i zwalnianie sesji z nieodczytanym strumieniem. Nagłe zerwanie testowano przez zakończenie wyłącznie procesu testowego sshd danej sesji, bez zatrzymywania listenera. Ponowne połączenie w tej samej kontrolce Avalonia oraz odzyskanie po błędnym passphrase przeszły. SFTP i monitoring otwarte podczas reconnect uruchamiają się ponownie. Zamykanie kontrolki podczas zdalnego sleep przeszło. Monitoring zwrócił rzeczywiste dyski, interfejsy i topologię; nie są to dane demonstracyjne.

Wynik: artifacts/network-probe/reliability.txt. Nowe obrazy: session-disconnected.png, session-reconnected.png, server-info.png i sftp.png. Pierwszy restart testów trafił na zmieniony jednorazowy klucz sshd po restarcie WSL — blokada działała; runner dostał nową bazę testową, bez zmiany polityki zaufania aplikacji.

Poniżej zachowano wyniki wcześniejszego etapu 0.1.0 jako historię.

## Kompilacja i testy automatyczne

.NET SDK 10.0.401 lokalnie, Windows x64. Build Debug/Release przeszedł z 0 błędów i 0 ostrzeżeń. 24 testy xUnit przeszły: Quick Connect i odrzucanie haseł/komend w URL, ścieżki POSIX i traversal, różnicowe CPU, MemAvailable, walidacja profili, SQLite zapis/odczyt/edycja/usuwanie, rekurencyjna zmiana folderu i blokada usuwania niepustego, transakcyjny import, nowe identyfikatory importu, eksport bez sekretów, ustawienia/historia, trwałe klucze hostów i ochrona przed zastąpieniem, DPAPI zapis/odczyt/szyfrowanie/usuwanie na Windows.

Wynik TRX: `artifacts/test-results/unit.trx`. Test DPAPI nie jest dowodem kompatybilności Secret Service ani KWallet.

## Rzeczywiste połączenia z Ubuntu WSL

Izolowany OpenSSH 9.6p1 w Ubuntu 24.04 WSL2, port 22222, testowy klucz Ed25519 z passphrase. Wykonano narzędziem NetworkProbe:

- poprawne uwierzytelnienie kluczem z passphrase i wykonanie komendy SSH;
- interaktywny PTY, ANSI i resize;
- dwie równoczesne sesje terminala, zamknięcie jednej bez rozłączenia drugiej i nowe połączenie;
- rzeczywisty monitoring systemu, CPU z dwóch próbek, RAM i df;
- SFTP: tworzenie katalogu, upload/download 1 MiB, Unicode/spacje w nazwie, porównanie pełnych bajtów, chmod, rename, usunięcie pliku/katalogu;
- odmowa niezatwierdzonego nadpisania i zatwierdzone atomowe nadpisanie przez POSIX rename;
- anulowanie pobierania 8 MiB z zachowaniem oryginalnego pliku i usunięciem lokalnego .part;
- użycie zapisanego klucza w kolejnych odrębnych połączeniach SSH/SFTP;
- blokowanie zmienionego fingerprintu.

Pierwszy test anulowania wykrył wyjątki w callbackach postępu. Naprawiono przez użycie asynchronicznego API SSH.NET i bezpiecznych callbacków; ponowny pełny test zakończył się bez tych wyjątków. Wynik nie jest deklaracją testów hasła ani innych dystrybucji.

## Interfejs i uruchomienie

Windows: uruchomiono rzeczywistą aplikację desktopową; proces odpowiadał, tytuł okna ServerDesk. Renderowanie Avalonia Headless + Skia sprawdziło oba motywy oraz terminal podłączony do prawdziwego SSH. Zapisano `main-dark.png`, `main-light.png`, `terminal-ssh.png` i `terminal-nano.png` w artifacts/ui-smoke. Wizualnie sprawdzono pełnoekranowe nano, Unicode/ANSI, i poprawiony rozmiar PTY. Pierwszy render wykrył auto-start lokalnego cmd oraz niewłaściwy rozmiar początkowy; oba problemy naprawiono.

Linux: wydanie self-contained uruchomiono w Ubuntu WSLg/XWayland. xwininfo potwierdziło okno ServerDesk 1320×840; proces działał przez kontrolowany okres testu. Początkowy brak libICE/libSM rozwiązano przez izolowane biblioteki testowe, a kod zawiera czytelny preflight brakujących zależności. To test uruchomienia GUI, nie pełny test interakcji z Linuxa.

## Niewykonane lub ograniczone

- RDP do Windows, konto lokalne/AD, certyfikaty/NLA i schowek: niewykonane, brak serwera testowego RDP. Adaptery skompilowane; obraz w osobnym oknie, osadzanie niedostępne.
- SSH hasłem, jump host, wiele formatów kluczy: niewykonane.
- Ubuntu spoza WSL, Debian, Kubuntu, Fedora, Wayland natywny, Windows 10: niewykonane.
- Secret Service, KWallet: niewykonane.
- htop/vim/MC/fish/zsh: niewykonane; nano i bash sprawdzono.
- Graficzna obsługa drag & drop, wielomonitorowość i DPI: implementacja bez ręcznego testu systemowego.
- Dwie równoczesne sesje SSH sprawdzono na poziomie transportu; wiele zakładek z UI, zerwanie sieci, długotrwałe sesje i wycieki pamięci wymagają dalszej weryfikacji.
- Instalacja/odinstalowanie na czystych systemach, podpisywanie, MSI/AppImage/RPM: niewykonane. Pakowanie i inspekcja DEB nie są testem instalacji.

## Kolejny etap

Testy RDP i Secret Service/KWallet na docelowych systemach, scenariusze wielu jednoczesnych sesji i utraty sieci, UI drag & drop, czyste instalacje, następnie proof of concept natywnego bridge FreeRDP. Szczegóły ograniczeń w roadmap.md.



