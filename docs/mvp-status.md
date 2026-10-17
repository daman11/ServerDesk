# Pierwotne MVP — wydanie 0.39.1

0.39.1 obejmuje poniższe poprawki robocze po 0.39.0 oraz dziesięć zadań ochrony eksportów/importu, trwałości danych Network Tools, zachowania wyników diagnostyki i opisu RDP. [Zakres testów i ograniczenia](reports/release-verification-0.39.1.md). Nadal Public Beta; pełna akceptacja systemów i sprzętu pozostaje otwarta.

Sześć kolejnych zadań roboczych po 0.39.0 obejmuje blokadę przenoszenia usuniętego wpisu, zachowanie metadanych przy zmianie nazwy, akceptację tej samej nazwy, transakcyjne sprawdzanie pustego folderu, spójny odczyt eksportu i próby rollback/retry importu po błędach zapisu. Windows: 56 celowanych testów oraz dwa scenariusze UI w PL/EN. [Zakres i granice](reports/configuration-consistency-2026-10-17.md); nie zamyka to pełnej macierzy 1.0.

Kolejny krok roboczy po 0.39.0 zabezpiecza eksport konfiguracji: gotowy JSON jest zapisywany do pliku tymczasowego, a dopiero po zamknięciu zastępuje wybrany plik. Błąd lub anulowanie nie opróżnia poprzedniego eksportu; komunikaty i ponowienie sprawdzono w PL/EN. [Testy i ograniczenia](reports/configuration-export-2026-10-17.md).

Trzy kolejne poprawki robocze po 0.39.0 zapewniają atomowy zapis profilu i folderów, przenoszenie do nowego folderu oraz rekurencyjną zmianę nazwy drzewa wraz z ikonami i przypisaniami. Kontrolowane błędy SQLite sprawdzają zachowanie starego stanu i udane ponowienie. [Zakres testów](reports/profile-atomicity-2026-10-17.md); szersze próby braku miejsca i awarii procesu pozostają otwarte.

Zmiana robocza po 0.39.0 rozwija odzyskiwanie danych: w ustawieniach bezpieczeństwa jest lista 20 najnowszych kopii bazy z datą, rozmiarem, schematem i powodem, kopiowaniem ścieżki i dostępem do folderu. Odczyt metadanych nie otwiera bazy ani nie zmienia kopii. [Obsługa i przywracanie](database-backups.md).

0.39.0 dodaje wbudowane pluginy Monitoring Linux i Komendy z przełącznikami po zapisie bez restartu. Rdzeń SSH/SFTP/RDP zachowuje niezależność od dodatków. [Wydanie](releases/release-0.39.0.md), [obsługa](plugins.md), [testy](reports/plugins-2026-10-17.md).

0.38.0 osadza Linux RDP w karcie X11/XWayland i dodaje graficzne hasło/certyfikat. Zawiera również opisane poniżej zmiany rozwijane po 0.37.0. [Bieżące wydanie](releases/release-0.38.0.md), [weryfikacja](reports/release-verification-0.38.0.md), [model zaufania i ograniczenia RDP](rdp-integration.md).

0.37.0 stabilizuje dane, SFTP i przeglądarkę połączeń: sprawdzone kopie SQLite, ręczne odzyskiwanie SFTP, wskazówki błędów i plików częściowych oraz zachowanie zaznaczenia i zwiniętych folderów. [Wydanie](releases/release-0.37.0.md), [zakres weryfikacji](reports/release-verification-0.37.0.md).

Zmiany monitoringu po 0.37.0: odczyt sieci bez JSON na starszym iproute2, domyślne bramy IPv6 obok IPv4 i pomijanie niespójnego przedziału CPU zamiast fałszywego 0%. Celowane testy obejmują parsery, własny OpenSSH, PL/EN i powrót monitoringu po zerwaniu jego transportu. [Dokładny zakres](reports/monitoring-compatibility-2026-10-16.md); bez deklaracji pełnej macierzy dystrybucji.

Kolejny etap roboczy ogranicza tworzenie ukrytych wierszy i menu w drzewie połączeń oraz usuwa komunikat nakładający się na puste foldery. Import, filtrowanie i sortowanie 5000 profili/200 folderów sprawdzono na Windows. [Zakres i pomiary](reports/browser-scalability-2026-10-16.md); fizyczne gesty, głębsze drzewa i długie sesje pozostają w planie 1.0.

Punkt 5 otrzymuje dynamiczne dopasowanie dialogów do ekranów/DPI, test ustawień PL/EN i klawiatury na Windows/Linux, natywną regresję startu Windows i 10-minutowy pomiar ośmiu sesji. [Wyniki i granice](reports/platform-ux-2026-10-16.md), [macierz i akceptacja Kali/KDE/Wayland](platform-support.md). Sprzęt, portfele i docelowe desktopy nadal wymagają rzeczywistej akceptacji.

0.36.2 uzupełnia ikony i ujednolica nazwy istniejących modułów oraz dodaje audyt kompletności akcji. Nie rozszerza zakresu protokołów MVP.

0.36.1 porządkuje wygląd komend, ustawień i wspólnych list. Dodaje kontrolę układu małego okna z większą czcionką; zakres istniejących funkcji MVP opisano poniżej.

0.36.0 realizuje wspólny panel Command Snippets i Smart Cheat Sheet: 165 wariantów, 134 operacje, 10 kategorii, offline/SSH/manual/pin, szyfrowanie szablonów, parametry, JSON, ulubione i kontrolowane wysyłanie do konkretnego połączenia. RDP/VNC/Serial nie otrzymują dostępu do powłoki. [Obsługa](commands.md), [testy i granice](reports/commands-2026-10-16.md).

0.35.0 rozwija równoległą pracę w terminalach: trzy lub cztery panele, niezależne grupy kart, pełne menu dokowania i zapamiętywanie obu proporcji. Nowe ustawienia obszaru pracy nie rozłączają sesji. [Zakres weryfikacji](reports/four-panel-workspace-2026-10-16.md).

Nowości 0.34.0: **Ustawienia → Terminal/SFTP** — kształt i miganie kursora, historia 0–100 000 wierszy przy następnym połączeniu, lokalny folder startowy i domyślny widok obu paneli. Podgląd/anulowanie nie kasują bufora, a zapis nie zmienia otwartego SFTP. [Testy i granice](reports/extended-settings-2026-10-16.md).

Ta lista odnosi się do pierwotnej specyfikacji ServerDesk i jej kryteriów gotowości (§21). Nie zalicza dodatkowych Network Tools, VNC ani Serial jako zastępstwa funkcji SSH/SFTP/Server Info/RDP. Historyczny audyt 0.4.0 pozostaje zapisem tamtego stanu.

| Kryterium pierwotnego MVP | Implementacja i potwierdzony zakres | Pozostała weryfikacja |
|---|---|---|
| Uruchomienie Windows/Linux | Natywna aplikacja Avalonia; Windows x64 Setup/ZIP, Linux x64 DEB/TAR. Gotowe EXE i okno WSLg sprawdzane w procesie wydania. | Oddzielne Windows 10, Kubuntu, Fedora, Debian i fizyczne środowiska X11/Wayland. |
| Dodanie, zapis, edycja i foldery serwerów | SQLite, foldery/podfoldery, ulubione, historia, naturalne sortowanie nazw/IP, wyszukiwanie, duplikowanie i Quick Connect. | Etap roboczy na Windows: import/widok 5000 profili i 200 folderów. Pozostają dalsza skala/głębokość, wiele własnych grafik, pełna ręczna macierz gestów i wielogodzinne pomiary. |
| Połączenie SSH i interaktywny terminal | **0.28.0:** rzeczywisty OpenSSH z hasłem i kluczem/passphrase; vim/nano z zapisem UTF-8, top/htop/MC, bash/zsh/fish, 28 przypadków inputu i schowek, trzy rozmiary PTY, EOF/reconnect oraz utrata transportu. Escape Windows i kopiowanie bez zaznaczenia poprawione; otwarcie PTY i resize poza UI. Windows i Ubuntu/WSL. | Inne implementacje/wersje SSH, fizyczne układy klawiatury/AltGr/IME, mysz terminalowa i pełna macierz rozszerzonych protokołów klawiatury. |
| Kilka terminali i zakładki | Oddzielne transporty i terminale, karty, dwa panele, osobne okna, zmiana kolejności i przywracanie bez automatycznego logowania. 0.30.0: poprawione zwalnianie źródeł anulowania, pełne sprzątanie także usuniętych kart, reconnect i przenoszenie działającego terminala między oknami. | Wielogodzinne sesje i duża liczba terminali; ograniczone pomiary oraz dokładny zakres opisuje raport 0.30.0. |
| Przeglądanie plików SFTP | Dwa panele, metadane/ikony, ukryte pliki, filtrowanie/sortowanie, zaznaczanie grup, katalogi, rename, move, chmod i trwałe usuwanie z podglądem. | Sprawdzono rzeczywisty katalog 5000 wpisów i anulowanie odczytu; pozostają większe katalogi i serwery inne niż OpenSSH. |
| Upload/download i katalogi | Rzeczywiste transfery i porównanie bajtów Windows/Linux, limit dwóch aktywnych zadań, postęp/prędkość, nadpisywanie za zgodą, cancel/retry i raportowanie pominięć. | 0.29.0: sprawdzone silent loss upload/download, zerwane nadpisanie, zachowanie oryginałów i retry. Pozostają dalsze fazy/WAN i wznawianie; zdalny .part może pozostać. |
| Dowiązania symboliczne | **0.27.0:** rozpoznanie, szczegóły rzeczywistego celu, przejście do katalogu, tworzenie zdalnego dowiązania oraz osobne kopiowanie zawartości wybranego celu po potwierdzeniu. Zwykła rekurencja pomija dowiązania. | Przenoszenie samych obiektów dowiązań między platformami pozostaje poza obsługiwanym trybem; nie jest utożsamiane z kopiowaniem zawartości. |
| Server Info, rzeczywiste CPU/RAM/dyski | Zbieranie przez SSH i standardowe /proc/polecenia bez agenta/sudo; CPU z kolejnych próbek, RAM/swap, dyski, sieć, system, wykres i ograniczanie pracy nieaktywnych sesji. 0.31.0: próbka zachowuje poprawne sekcje przy braku df/ip/lscpu lub niedostępnym CPU/RAM; osobne komunikaty, walidacja liczników i nowy punkt odniesienia po przerwie. | Pozostałe dystrybucje i szersza macierz uprawnień/odmian poleceń; kontrolowane fixture nie zastępuje testu fizycznej dystrybucji. |
| Skonfigurowanie i uruchomienie RDP | Windows: osadzony natywny host i wcześniejsze rzeczywiste logowanie lokalne. Linux 0.38.0: X11 FreeRDP osadzony w karcie, okna hasła i certyfikatu, XWayland w sesji Wayland. | Osadzony framebuffer Linux, AD/Microsoft, pełny schowek/input/utrata sieci i fizyczne mieszane DPI. Test hosta EXE nie oznacza nowego testu logowania. |
| Bezpieczne przechowywanie poświadczeń | Windows DPAPI, Linux Secret Service, opcjonalny zapis, powiązanie z tożsamością profilu; JSON i SQLite bez sekretów. GNOME Keyring zweryfikowano w izolacji. | KWallet/Secret Service w rzeczywistym KDE; inne sesje keyringu. |
| Import/eksport między platformami | Wersjonowany JSON bez haseł i kluczy prywatnych, foldery/ikony/personalizacja, nowe identyfikatory importowanych profili i atomowa walidacja. | Dodatkowe rzeczywiste migracje konfiguracji użytkowników i bardzo duże importy. |
| Ciemny i jasny motyw, wygodny UI | Pięć motywów, czcionka/gęstość/akcent, ikony/grafiki, PL/EN, ustawienia z wyszukiwaniem, responsywność i poprawianie położenia okna poza ekranem. | Czytniki ekranu, zmiany motywu OS w pracy, fizyczne DPI i pozostałe kombinacje sprzętu. |

## Wykonany etap

Wydanie 0.33.0 rozwija przeglądanie SFTP: Wstecz/Dalej, nadrzędny/domowy, skróty, osobne historie do 100 wpisów i zatwierdzanie ścieżek. Błędny odczyt przywraca poprzednie katalogi; operacje nie używają niezatwierdzonego tekstu. Windows/Linux przeszły rzeczywiste testy OpenSSH/SFTP oraz kontrolę wąskich paneli. [Weryfikacja](reports/sftp-navigation-2026-10-16.md).

Wydanie 0.32.0 dodaje lokalne wyszukiwanie w dostępnym buforze SSH/Serial: Ctrl+Shift+F, wielkość liter, całe słowa, nawigację, powrót do bieżącego tekstu i oznaczenie nieaktualnych wyników po nowym wyjściu. Trafienie przez zawinięcie jest liczone raz. Testy obejmują bufor, małe panele i rzeczywiste SSH; nie zastępują fizycznej macierzy klawiatury i portów Serial. [Weryfikacja](reports/terminal-search-2026-10-16.md).

Wydanie 0.32.0 dodaje **Ustawienia → Bezpieczeństwo → Zaufane klucze SSH**. Użytkownik może sprawdzić i skopiować odcisk oraz usunąć jeden wpis po potwierdzeniu. Usuwanie porównuje także odcisk, aby nie usunąć klucza zmienionego w innym oknie. Zmieniony klucz pozostaje blokowany, a po usunięciu zaufania następne połączenie wymaga ponownej weryfikacji. [Zakres i testy](reports/ssh-trust-management-2026-10-16.md).

Zmiany 0.32.0 przenoszą filtrowanie i sortowanie list od 2000 wpisów poza UI. Szybkie zmiany są łączone, nieaktualne obliczenia anulowane, a reconnect i zamknięcie czekają na ich zakończenie. Test widoków obejmuje po 50 000 wpisów w obu panelach na Windows i Linux/WSL, a rzeczywisty OpenSSH — katalog 5000 plików oraz utratę transportu i anulowanie transferu. Nie oznacza to testu odczytu 50 000 zdalnych plików ani wielogodzinnej sesji. [Weryfikacja zmian](reports/sftp-large-file-views-2026-10-16.md).

Etap 0.31.0 przenosi przygotowanie klucza z passphrase oraz pełny odczyt monitoringu poza UI. Server Info zachowuje dostępne sekcje przy brakach narzędzi i praw odczytu, pokazuje niedostępne dane bez fikcyjnego zera oraz czyści stary wykres i parametry. Testy obejmują odczyt rzeczywistych danych przez ograniczony PATH i własne pliki z odmową dostępu, powrót danych oraz PL/EN. Zachowano poprawki cyklu życia i dokowania z 0.30.0. [Zakres](releases/release-0.31.0.md).

## Następne prace nad pierwotnym zakresem

1. Dłuższe sesje, setki profili i większe drzewa SFTP; pomiary responsywności/pamięci i utrata sieci podczas transferów.
2. Kubuntu/KWallet oraz pozostałe docelowe systemy i sprzęt: Windows 10, fizyczny X11/Wayland, klawiatury/IME i mieszane DPI.
3. Rozszerzenie macierzy osadzonego RDP, akceptacja KDE/XWayland oraz docelowy framebuffer dla natywnego Wayland.
4. Pozostałe serwery SSH/SFTP, fizyczne gesty/mysz terminalowa i szersza macierz protokołów klawiatury.

Nie ogłaszamy pełnego MVP za zakończone na podstawie samego istnienia kontrolek lub zielonej kompilacji. Wyniki tego etapu i granice testów zapisuje [raport weryfikacji](reports/release-verification-0.36.2.md).

Poprawka 0.31.1 naprawia kodowanie instalatora oraz dodaje przypomnienia w docelowym języku i bezpieczny restart z ustawień/okna głównego. [Zakres poprawki](releases/release-0.31.1.md).
