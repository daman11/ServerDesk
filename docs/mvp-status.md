# Pierwotne MVP — wydanie 0.44.0

Krok roboczy po 0.44.0: **Sprawdź kopię** kontroluje również wymagane tabele/kolumny i zapisane profile, bez migracji, zmian pliku, połączeń sieciowych i odczytu magazynu haseł. Weryfikuje tożsamości, JSON i model połączenia; zachowuje obsługę historycznych formatów. [Zakres i granice](reports/backup-content-2026-10-21.md). Jest to część ochrony danych; nie zastępuje pełnego odtworzenia sekretów ani akceptacji sprzętu i RDP.

0.44.0 zbiera opisane poniżej kroki po 0.43.0 oraz Pomoc offline i diagnostykę. Eksport RDP przez schowek jest eksperymentalny; bezpośredni gest ze zdalnego Eksploratora do systemu lokalnego nie jest wdrożony. Nie zamyka to akceptacji 1.0. [Wydanie i weryfikacja](reports/release-verification-0.44.0.md).

Krok roboczy po 0.43.0: nowe RDP domyślnie w karcie; bezpośredni zrzut plików na Windows RDP, fizyczny gest do odbiornika i osiem kolejnych transferów z porównaniem sum. Tekst i obraz sprawdzone przed i po zrzutach. Pełna akceptacja Eksploratora, AltGr/IME w RDP i mieszanego DPI pozostaje otwarta. [Zakres i ograniczenia](reports/rdp-desktop-drop-2026-10-19.md).

Akceptacja w udostępnionych VM Windows 11 Pro i Ubuntu 26.04.1/GNOME/Wayland: [wyniki z 2026-10-19](reports/vm-mvp-2026-10-19.md). Obejmuje natywne okno, schowek, magazyn haseł i rzeczywiste SSH/SFTP; zakres Windows, przerwania i częściowe RDP opisano oddzielnie. Nie jest to deklaracja pełnej gotowości 1.0.

Krok roboczy po 0.43.0 rozwija codzienną pracę w SFTP: kopiowanie nazw/pełnych ścieżek/katalogu jako tekst, także z zachowanego widoku po utracie łącza; Ctrl+Shift+C, Ctrl+F i powrót Escape do listy. Dostępność menu odpowiada paskowi narzędzi, a operacje na zaznaczeniu czekają na zakończenie filtrowania. Celowane testy PL/EN i regresja 50 000 wpisów nie zastępują prób WAN ani innych serwerów. [Zakres](reports/sftp-clipboard-2026-10-19.md).

Krok roboczy po 0.43.0 poprawia ergonomię SFTP: pełne szczegóły zadania z kopiowaniem, krótsze karty boczne i wybór czyszczenia udanych zadań albo całej zakończonej historii. Aktywne zadania i pliki nie są usuwane. [Zakres sprawdzenia](reports/transfer-ux-2026-10-19.md).

0.43.0 rozwija ochronę danych i odzyskiwanie SSH/SFTP: atomowe powiązanie hasła przy edycji celu, bezpieczną kolejność usuwania profilu, kontrolę równoległego zapisu pluginów, schemat 4 z kopią przed migracją, limity cichego łącza i zachowanie kontekstu SFTP po reconnect. Transfer nie jest samoczynnie ponawiany. [Dowody i granice](reports/release-verification-0.43.0.md). Nie zalicza fizycznego suspend/resume ani odłożonych VM, sprzętu i długiej pracy.

0.42.0 dodaje kolorowe tagi i filtrowanie, motywy, wybór edytora SFTP, ergonomię nawigacji i komend oraz informacje o aplikacji i logi w ustawieniach. Naprawia usuwanie zapisanych profili przy braku katalogu poświadczeń oraz eksport logów przez alias katalogu. [Bieżąca weryfikacja](reports/release-verification-0.42.0.md). Pozostaje Public Beta; testy maszyn i sprzętu odłożono na prośbę maintenera.

0.41.0 zbiera opisane poniżej zmiany robocze drzewa/ikon/grafik i narzędzi RC. Chroni sekrety kolekcji przed utratą przy błędzie indeksu oraz dodaje rzeczywiste próby migracji/odtworzenia, przerwania procesu i braku miejsca. Testy VMware/sprzętu odłożono na prośbę maintenera; status nadal Public Beta. [Weryfikacja i otwarte warunki 1.0](reports/release-verification-0.41.0.md).

Pięć zadań roboczych po 0.40.0 dotyczy przeglądarki połączeń: folder domyślny nowych wpisów, stabilna zmiana nazw drzewa, bezpieczne usuwanie folderów, przeciąganie także do katalogu głównego i skróty klawiatury. [Scenariusze i granice prób](reports/browser-workflow-2026-10-17.md). Nie zastępują sprzętowej akceptacji gestów ani pełnej macierzy 1.0.

Krok roboczy po 0.40.0, włączony do 0.41.0: ograniczenie ponownego dekodowania własnych grafik podczas zmiany metadanych profilu oraz bezpieczny fallback dla uszkodzonych/starszych danych. Test obejmuje 96 różnych podglądów, edytor i zwalnianie bitmap; nie oznacza pełnej akceptacji dużych drzew ani długich sesji. [Zakres i granice](reports/profile-artwork-2026-10-17.md).

0.40.0: **Ustawienia → Bezpieczeństwo → Zapisane kopie bazy → Sprawdź kopię** wykonuje kontrolę SQLite i zgodności schematu wybranego pliku. Bez przywracania danych, migracji, zapisu do kopii ani sprawdzania sekretów. [Obsługa](database-backups.md), [celowane testy i granice](reports/backup-verification-2026-10-17.md).

0.40.0: stabilizacja Start/Stop/Restart i zamykania kart Network Tools, w tym automatycznych obliczeń podsieci, opóźnionego odczytu kolekcji i okien zapisu. [Zakres celowanych prób](reports/network-cancellation-2026-10-17.md). Wydanie obejmuje też [personalizację interfejsu i kart](reports/customization-2026-10-17.md). [Weryfikacja wydania i ograniczenia](reports/release-verification-0.40.0.md).

0.39.1 obejmuje poniższe poprawki robocze po 0.39.0 oraz dziesięć zadań ochrony eksportów/importu, trwałości danych Network Tools, zachowania wyników diagnostyki i opisu RDP. [Zakres testów i ograniczenia](reports/release-verification-0.39.1.md). Nadal Public Beta; pełna akceptacja systemów i sprzętu pozostaje otwarta.

Sześć kolejnych zadań roboczych po 0.39.0 obejmuje blokadę przenoszenia usuniętego wpisu, zachowanie metadanych przy zmianie nazwy, akceptację tej samej nazwy, transakcyjne sprawdzanie pustego folderu, spójny odczyt eksportu i próby rollback/retry importu po błędach zapisu. Windows: 56 celowanych testów oraz dwa scenariusze UI w PL/EN. [Zakres i granice](reports/configuration-consistency-2026-10-17.md); nie zamyka to pełnej macierzy 1.0.

Kolejny krok roboczy po 0.39.0 zabezpiecza eksport konfiguracji: gotowy JSON jest zapisywany do pliku tymczasowego, a dopiero po zamknięciu zastępuje wybrany plik. Błąd lub anulowanie nie opróżnia poprzedniego eksportu; komunikaty i ponowienie sprawdzono w PL/EN. [Testy i ograniczenia](reports/configuration-export-2026-10-17.md).

Trzy kolejne poprawki robocze po 0.39.0 zapewniają atomowy zapis profilu i folderów, przenoszenie do nowego folderu oraz rekurencyjną zmianę nazwy drzewa wraz z ikonami i przypisaniami. Kontrolowane błędy SQLite sprawdzają zachowanie starego stanu i udane ponowienie. [Zakres testów](reports/profile-atomicity-2026-10-17.md); szersze próby braku miejsca i awarii procesu pozostają otwarte.

Zmiana robocza po 0.39.0 rozwija odzyskiwanie danych: w ustawieniach bezpieczeństwa jest lista 20 najnowszych kopii bazy z datą, rozmiarem, schematem i powodem, kopiowaniem ścieżki i dostępem do folderu. Odczyt metadanych nie otwiera bazy ani nie zmienia kopii. [Obsługa i przywracanie](database-backups.md).

0.39.0 dodaje wbudowane pluginy Monitoring Linux i Komendy z przełącznikami po zapisie bez restartu. Rdzeń SSH/SFTP/RDP zachowuje niezależność od dodatków. [Wydanie](releases/release-0.39.0.md), [obsługa](plugins.md), [testy](reports/plugins-2026-10-17.md).

0.38.0 osadza Linux RDP w karcie X11/XWayland i dodaje graficzne hasło/certyfikat. Zawiera również opisane poniżej zmiany rozwijane po 0.37.0. [Opis wydania 0.38.0](releases/release-0.38.0.md), [weryfikacja](reports/release-verification-0.38.0.md), [model zaufania i ograniczenia RDP](rdp-integration.md).

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
| Kilka terminali i zakładki | Oddzielne transporty i terminale, karty, do czterech paneli, osobne okna, zmiana kolejności i przywracanie bez automatycznego logowania. 0.30.0: poprawione zwalnianie źródeł anulowania, pełne sprzątanie także usuniętych kart, reconnect i przenoszenie działającego terminala między oknami. | Wielogodzinne sesje i duża liczba terminali; ograniczone pomiary oraz dokładny zakres opisuje raport 0.30.0. |
| Przeglądanie plików SFTP | Dwa panele, metadane/ikony, ukryte pliki, filtrowanie/sortowanie, zaznaczanie grup, katalogi, rename, move, chmod i trwałe usuwanie z podglądem. | Sprawdzono rzeczywisty katalog 5000 wpisów i anulowanie odczytu; pozostają większe katalogi i serwery inne niż OpenSSH. |
| Upload/download i katalogi | Rzeczywiste transfery i porównanie bajtów Windows/Linux, limit dwóch aktywnych zadań, postęp/prędkość, nadpisywanie za zgodą, cancel/retry i raportowanie pominięć. | 0.29.0: sprawdzone silent loss upload/download, zerwane nadpisanie, zachowanie oryginałów i retry. Pozostają dalsze fazy/WAN i wznawianie; zdalny .part może pozostać. |
| Dowiązania symboliczne | **0.27.0:** rozpoznanie, szczegóły rzeczywistego celu, przejście do katalogu, tworzenie zdalnego dowiązania oraz osobne kopiowanie zawartości wybranego celu po potwierdzeniu. Zwykła rekurencja pomija dowiązania. | Przenoszenie samych obiektów dowiązań między platformami pozostaje poza obsługiwanym trybem; nie jest utożsamiane z kopiowaniem zawartości. |
| Server Info, rzeczywiste CPU/RAM/dyski | Zbieranie przez SSH i standardowe /proc/polecenia bez agenta/sudo; CPU z kolejnych próbek, RAM/swap, dyski, sieć, system, wykres i ograniczanie pracy nieaktywnych sesji. 0.31.0: próbka zachowuje poprawne sekcje przy braku df/ip/lscpu lub niedostępnym CPU/RAM; osobne komunikaty, walidacja liczników i nowy punkt odniesienia po przerwie. | Pozostałe dystrybucje i szersza macierz uprawnień/odmian poleceń; kontrolowane fixture nie zastępuje testu fizycznej dystrybucji. |
| Skonfigurowanie i uruchomienie RDP | Windows: osadzony natywny host i wcześniejsze rzeczywiste logowanie lokalne. Linux 0.38.0: X11 FreeRDP osadzony w karcie, okna hasła i certyfikatu, XWayland w sesji Wayland. | Osadzony framebuffer Linux, AD/Microsoft, pełny schowek/input/utrata sieci i fizyczne mieszane DPI. Test hosta EXE nie oznacza nowego testu logowania. |
| Bezpieczne przechowywanie poświadczeń | Windows DPAPI, Linux Secret Service, opcjonalny zapis, powiązanie z tożsamością profilu; JSON i SQLite bez sekretów. GNOME Keyring zweryfikowano w izolacji. | KWallet/Secret Service w rzeczywistym KDE; inne sesje keyringu. |
| Import/eksport między platformami | Wersjonowany JSON bez haseł i kluczy prywatnych, foldery/ikony/personalizacja, nowe identyfikatory importowanych profili i atomowa walidacja. | Dodatkowe rzeczywiste migracje konfiguracji użytkowników i bardzo duże importy. |
| Ciemny i jasny motyw, wygodny UI | Gotowe motywy, czcionka/gęstość/akcent, ikony/grafiki, PL/EN, ustawienia z wyszukiwaniem, responsywność i poprawianie położenia okna poza ekranem. | Czytniki ekranu, zmiany motywu OS w pracy, fizyczne DPI i pozostałe kombinacje sprzętu. |

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
