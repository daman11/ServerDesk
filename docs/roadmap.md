Bieżące priorytety i postęp stabilizacji: [Droga do wersji 1.0](stable-release-plan.md). Pierwotne MVP: aktualna tabela kryteriów i brakującej weryfikacji znajduje się w [mvp-status.md](mvp-status.md). Poniższe wpisy opisują historię etapów; dawne listy nie są aktualną listą braków.

0.37.0: sprawdzone kopie SQLite i ochrona startu, odzyskiwanie SFTP i kolejka/ostrzeżenia `.part`, stabilne zaznaczenie oraz tymczasowe rozwijanie wyszukiwania w panelu połączeń. [Wydanie](releases/release-0.37.0.md).

0.36.2: kompletność ikon, zgodne etykiety PL/EN i opisy dostępności, osobne skróty menu oraz regresyjny audyt akcji. [Wydanie](releases/release-0.36.2.md).

0.36.1 dopracowuje istniejący interfejs: panel komend, nagłówek i sekcje ustawień, wspólne karty oraz zaznaczenie list w motywach. [Wydanie](releases/release-0.36.1.md).

Wydanie 0.36.0: wspólny panel Command Snippets / Smart Cheat Sheet, 165 wariantów, offline i kontekst SSH, bezpieczne parametry, ulubione i szyfrowane szablony. [Obsługa i rozszerzanie](commands.md), [testy i ograniczenia](reports/commands-2026-10-16.md).

Wydanie 0.35.0: trzy/cztery panele, niezależne grupy kart i dalsze ustawienia obszaru pracy. [Testy i ograniczenia](reports/four-panel-workspace-2026-10-16.md).

Wydanie 0.34.0: sześć kategorii ustawień, kursor i limit historii SSH/Serial oraz katalog startowy i domyślny widok SFTP. Zmiany zachowują bieżący bufor i wybory otwartej przeglądarki. [Zakres](reports/extended-settings-2026-10-16.md).

Wydanie 0.33.0: dopracowana nawigacja SFTP i potwierdzane ścieżki — niezależne historie paneli, Wstecz/Dalej, nadrzędny/domowy, obsługa błędnego odczytu i ochrona celów operacji. [Zakres](reports/sftp-navigation-2026-10-16.md).

Wydanie 0.32.0: lokalne wyszukiwanie historii SSH/Serial, logiczne trafienia przez zawinięcia, kontrola skrótów i osobny pasek bez zasłaniania zawartości. [Zakres](reports/terminal-search-2026-10-16.md).

Wydanie 0.32.0: zarządzanie zapisanym zaufaniem SSH w ustawieniach, bez automatycznego akceptowania zmienionych kluczy. [Testy i zakres](reports/ssh-trust-management-2026-10-16.md).

Wydanie 0.32.0: filtrowanie i sortowanie dużych list SFTP w tle, anulowanie starszych wyników i bezpieczne zamykanie. Sprawdzono widoki po 50 000 wpisów na panel Windows/Linux oraz rzeczywisty katalog 5000 plików OpenSSH. [Zakres pomiarów](reports/sftp-large-file-views-2026-10-16.md). Paczki 0.31.1 pozostają zamrożone.

Wydanie 0.29.0 usprawnia większe listy połączeń oraz transfery przy utracie sieci: ograniczony postęp/bufor uploadu, anulowanie, zachowanie oryginałów i retry. Testy obejmują 1000 profili oraz rzeczywisty katalog 5000 plików. [Zmiany](releases/release-0.29.0.md).

Wydanie 0.28.0 dopracowuje terminal SSH: Escape na Windows, bezpieczne kopiowanie bez zaznaczenia, negocjacja PTY i kolejka resize poza interfejsem oraz rzeczywiste testy hasła/klawiatury/aplikacji. [Zmiany](releases/release-0.28.0.md).

Wydanie 0.27.0 rozwija SFTP: nawigacja/szczegóły i tworzenie dowiązań, jawne kopiowanie zawartości celu po potwierdzeniu, ochrona celu transferu i ograniczona rekurencja. [Instrukcja](releases/release-0.27.0.md).

Wydanie 0.26.0 odświeża ekran startowy, dodaje wyszukiwanie ustawień i czytelne nagłówki aktywnego panelu SFTP. Układy dostosowują się do szerokości, czcionki i odstępów. [Instrukcja](releases/release-0.26.0.md).

Wydanie 0.25.2 naprawia sortowanie połączeń i plików: naturalne nazwy, liczbowe IPv4/IPv6/porty, kierunek i zapis preferencji, zgodność widoku Ostatnie z wyborem oraz klikalne kolumny SFTP. [Instrukcja](releases/release-0.25.2.md).

Wydanie 0.25.1 dopracowuje wspólne dialogi, ustawienia, paletę poleceń i obsługę wyników/celów Network Tools. [Zmiany](releases/release-0.25.1.md).

Wydanie 0.25.0 realizuje moduł MVP Network Tools: dziewięć narzędzi standalone/context-aware, istniejące karty/dokowanie, historia, eksport i bezpieczne kolekcje. [Instrukcja, zakres i limity](network-tools.md). Serial pozostaje osobną, dotychczasową funkcją.

Wydanie 0.24.1 usuwa nakładanie tekstu przywróconych kart i rozdziela Transfery od filtrów połączeń. [Zmiany](releases/release-0.24.1.md).

Wydanie 0.24.0 dodaje usuwanie grup i niepustych katalogów lokalnie/SFTP z podglądem, przerwaniem, raportem częściowym i ochroną dowiązań. [Zakres i ograniczenia](releases/release-0.24.0.md).

Wydanie 0.23.0 otwiera publiczną betę na MIT: instalator Windows, wersjonowane paczki, źródła i kontrola publikacji. [Procedura kolejnych wydań](releasing.md) i [ograniczenia beta](known-issues.md).

Wydanie 0.22.2 wzmacnia magazyn poświadczeń i obsługę zatrzymanych/uszkodzonych połączeń VNC. [Zmiany i ograniczenia](reports/security-stability-0.22.2.md).

Wydanie 0.22.1 poprawia zabezpieczenia VNC, SFTP, importu, RDP i poświadczeń. [Raport bezpieczeństwa](reports/security-review-0.22.1.md) oddziela poprawki od pozostałych ograniczeń.

Wydanie 0.22.0 dodaje zaznaczanie grup i zbiorcze transfery SFTP, zachowanie wyboru, liczniki oraz zabezpieczenia planu transferów. Szczegóły: [release-0.22.0.md](releases/release-0.22.0.md).

Wydanie 0.21.0 odświeża wygląd aplikacji: boczna nawigacja ustawień, układ pól dostosowany do szerokości, wspólne karty i nagłówki, motywy Dark/Light, menu oraz panele sesji. Szczegóły: [release-0.21.0.md](releases/release-0.21.0.md).

Wydanie 0.20.1 dodaje widoczne przypomnienia o restarcie po zmianie języka i porządkuje wygląd ustawień, ekranu startowego oraz filtrów. Szczegóły: [release-0.20.1.md](releases/release-0.20.1.md).

Wydanie 0.20.0 dodaje angielski interfejs i wybór Polski / English w ustawieniach, stosowany po restarcie. Szczegóły: [release-0.20.0.md](releases/release-0.20.0.md).

Wydanie 0.19.1 naprawia uruchamianie okna poza ekranem i zapis jego stanu. Szczegóły: [release-0.19.1.md](releases/release-0.19.1.md).

Wydanie 0.19.0 dodaje anulowanie połączeń i wspólną historię czterech protokołów z zapamiętanym celem, filtrami i bezpiecznym ponawianiem. Szczegóły: [release-0.19.0.md](releases/release-0.19.0.md).

Wydanie 0.18.0 dodaje VNC w zakładce i Serial w terminalu, ich profile, filtry i szybkie URI. Zakres i niewykonane testy sprzętowe/serwerowe: [release-0.18.0.md](releases/release-0.18.0.md).

Wydanie 0.17.0 porządkuje edytor serwera, dodaje walidację pól i podgląd oraz filtry protokołu, wyszukiwanie metadanych i liczniki. Szczegóły: [release-0.17.0.md](releases/release-0.17.0.md).

Wydanie 0.16.0 dodaje pliki przez schowek RDP, konfigurację klawiatury, autoreconnect i poprawki DPI/drag. Wykonane oraz pozostałe komórki testów: [rdp-test-matrix.md](rdp-test-matrix.md). Nie oznacza to zakończenia całej macierzy.

# Stan i dalsza praca

Stan po 0.22.0: SSH/SFTP/monitoring i podstawowe zarządzanie profilami mają działającą implementację i rzeczywiste testy na Ubuntu/WSL z klienta Windows. Wdrożono redesign i poprawki wskazane przez audyt, ikony, personalizację oraz dwa panele/dokowanie. Wydanie 0.8.0 dodało wyszukiwarkę sesji i przywracanie kart bez automatycznego logowania. Wydanie 0.9.0 dodało automatyczny zapis układu, odzyskiwanie po awarii procesu i zabezpieczenia zamykania oraz weryfikację rozmiaru transferów SFTP. Wydanie 0.10.0 dopracowało główne okno i paski narzędziowe. Wydanie 0.10.1 naprawia wskaźnik ładowania wychodzący poza sesję i upraszcza oznaczenie aktywnej karty. Wydanie 0.11.0 dodaje dolny pasek parametrów, ikony folderów i wspólny nagłówek okna. Wydanie 0.12.0 usuwa kolizje nagłówka i parametrów oraz rozszerza ustawienia personalizacji. Wydanie 0.13.0 dodaje niezależne filtry/sortowanie SFTP, skróty i kolejkę z ponowieniami oraz rozliczaniem pominięć. Wydanie 0.14.0 dodaje diagnostykę NLA/TLS i sterowanie klientem RDP; na prywatnym serwerze Windows potwierdzono logowanie lokalnego konta oraz obraz przez FreeRDP/WSLg. Wydanie 0.15.0 dodaje osadzony RDP Windows z rzeczywistym logowaniem, skalowaniem i ciągłością sesji przy dokowaniu/split view. Wydanie 0.15.1 naprawia manifest Windows wymagany przez osadzone RDP i dodaje testy gotowego EXE oraz odzyskiwanie przez klienta zewnętrznego. Anulowanie transferów i skrócenie pliku podczas pobierania sprawdzono na rzeczywistym OpenSSH.

Raport bazowy: [requirements-audit-0.4.0.md](reports/requirements-audit-0.4.0.md). Zrealizowane poprawki: wydania [0.5.0](releases/release-0.5.0.md), [0.6.0](releases/release-0.6.0.md), [0.7.0](releases/release-0.7.0.md), [0.8.0](releases/release-0.8.0.md), [0.9.0](releases/release-0.9.0.md), [0.10.0](releases/release-0.10.0.md), [0.10.1](releases/release-0.10.1.md), [0.11.0](releases/release-0.11.0.md), [0.12.0](releases/release-0.12.0.md), [0.13.0](releases/release-0.13.0.md), [0.14.0](releases/release-0.14.0.md), [0.15.0](releases/release-0.15.0.md) i [0.15.1](releases/release-0.15.1.md). Nie traktujemy pełnego MVP ani całego redesignu jako zakończonych.

Pozostają:

- osadzony framebuffer RDP na Linux/Wayland; rzeczywiste mstsc oraz konta AD/Microsoft; schowek, pełna macierz klawiszy i fullscreen na Windows/Linux (lokalne konto/NLA/obraz zweryfikowano w osadzonym komponencie Windows oraz przez FreeRDP/WSLg);
- rzeczywiste SSH hasłem, dodatkowe formaty kluczy, vim/htop/fish/zsh/MC oraz szersza macierz klawiszy;
- Kubuntu/KWallet, Fedora, Debian, Windows 10 oraz niezależne X11/Wayland;
- gesty D&D serwerów/plików/zakładek na systemach docelowych;
- pełna macierz stanów szablonów kontrolek, czytniki ekranu i dalsza walidacja formularzy;
- dalsza walidacja pozostałych formularzy i ergonomia dużych list SFTP;
- Follow System ze zmianą ustawienia OS podczas pracy, wiele monitorów i mieszane DPI;
- rozszerzenia opcjonalne obecnego dokowania: więcej niż dwie grupy i zagnieżdżone podziały; dodatkowe formaty grafik;
- zachowywanie obiektów dowiązań między platformami (0.27.0 obsługuje jawne kopiowanie zawartości wybranego celu) oraz dalsze testy równoległych zmian i dużych drzew;
- sprzątanie zdalnego .part po utracie sieci, serwery bez POSIX rename, utrata sieci podczas transferu;
- testy setek serwerów, dużych katalogów, wielogodzinnych sesji, pamięci i wycieków;
- AppImage/RPM (warunkowe), podpisy zaufanego wydawcy i dalszy przegląd zgodności notices upstream przy zmianach zależności.

Synchronizacja, bastion, forwarding i pluginy są zakresem przyszłym.
