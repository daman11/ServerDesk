# Droga do ServerDesk 1.0

Aktualny krok RDP: [eksport plików przez schowek i lokalny uchwyt przeciągania](reports/rdp-clipboard-export-2026-10-19.md). Po decyzji maintenera wykonujemy implementację i testy lokalne, bez uruchamiania VM. Pełna akceptacja RDP w etapie C pozostaje otwarta, w tym fizyczne wejście i awarie rzeczywistego transportu.

Akceptacja VMware z 2026-10-19 obejmuje udostępnione Windows 11 Test i Ubuntu 26 Test. Rzeczywiste środowiska, wyniki, przerwania i otwarte próby opisuje [raport MVP w VM](reports/vm-mvp-2026-10-19.md). Jest to etap roboczy po 0.43.0, bez nowego RC; nie zastępuje sprzętu, pełnej macierzy RDP ani czterogodzinnej próby.

0.43.0 domyka kolejną partię etapów B/C: błędy zapisu a hasła profili, równoległe magazyny pluginów, migracja do schematu 4 i odzyskiwanie po cichej utracie SSH/SFTP. [Zakres i granice](reports/release-verification-0.43.0.md). Pozostałe odmiany serwerów, WAN, sprzęt i rzeczywiste suspend/resume wymagają dalszej akceptacji; nie otwieramy ponownie zakończonych kontrolowanych prób.

Punkt wyjścia: 0.44.0 Public Beta, zbierające Pomoc, ergonomię SFTP, diagnostykę i integrację RDP. Eksport RDP przez schowek jest eksperymentalny; ciągły gest ze zdalnego Eksploratora pozostaje niewdrożony. [Bieżąca weryfikacja](reports/release-verification-0.44.0.md). Funkcje rdzenia i dodatków istnieją; stan implementacji oraz granice wcześniejszych testów opisuje [MVP](mvp-status.md). Poniższa lista jest bieżącym planem stabilizacji, a nie deklaracją gotowości 1.0.

0.42.0 zbiera usprawnienia tagów, motywów, edytora SFTP i komend, dodaje informacje o aplikacji oraz logi w ustawieniach. Obejmuje naprawę usuwania profili i próby rotacji logów, aliasów katalogów, równoległych eksportów i dużych plików. [Zakres bieżącej weryfikacji](reports/release-verification-0.42.0.md). Testy VMware, sprzętu i pilotaż pozostają otwarte.

Małe kroki kończą się commitem, celowanymi sprawdzeniami i EXE do uruchomienia z folderu. Pełna macierz i paczki są przeznaczone dla większych etapów, RC i wydania 1.0.

## Kolejność następnych prac — 2026-10-17

**Aktualizacja wykonawcza: 0.41.0 Public Beta** zbiera usprawnienia drzewa/ikon, narzędzia RC oraz poprawkę ochrony sekretów kolekcji. Rozszerzono migracje/odtworzenie schematów 0–2, przerwanie procesów i rzeczywisty brak miejsca na izolowanym tmpfs. [Zakres i granice](reports/release-verification-0.41.0.md). Na prośbę maintenera testy VMware są odłożone do przygotowania dostępu; pozostałe próby sprzętu, długiej pracy i pilotażu nadal są otwarte. Nie nadaje to statusu stabilnego 1.0.

Etapy A–F wyznaczają kolejność wykonania. Punkty 1–6 poniżej pozostają szczegółową listą postępu według obszarów. Przed 1.0 priorytetem są poprawki istniejących funkcji; kolejne moduły odkładamy poza ten cykl.

### A. Ustalić zakres i macierz wydania

- Zapisać konkretne wersje Windows/Linux, architekturę x64, pulpit, X11/XWayland oraz wymagane biblioteki i klienta FreeRDP w [macierzy platform](platform-support.md). Oddzielić środowiska wspierane od eksperymentalnych.
- Zacząć akceptację od Windows 11 i natywnego Linuxa, w tym zgłoszonego Kali/KDE/Wayland. Pierwotne cele Windows 10, Ubuntu, Kubuntu, Debian i Fedora wymagają osobnego wyniku albo uzgodnionej zmiany zakresu; nie przypisywać im stabilnego wsparcia przez podobieństwo do WSL.
- Do każdego wspieranego środowiska przypisać scenariusze, wykonawcę i dowody. Automatyzację wykonujemy na własnych fixtures; próby fizycznych klawiszy, monitorów, portfela i Serial wymagają dostępnego stanowiska maintenera/testera.
- Potwierdzić zakres funkcji: rdzeń SSH/SFTP/profile/RDP, obecne VNC/Serial oraz wbudowane dodatki. Ograniczenia, np. VNC bez TLS, Linux RDP przez XWayland i brak transferu plików gestem do Linux RDP, muszą pozostać jawne w instrukcji i opisie wydania.

**Warunek zakończenia:** jedna lista wspieranych konfiguracji i scenariuszy bez nieustalonych pozycji. Brak dostępnego sprzętu oznacza otwartą akceptację lub uzgodniony węższy zakres, a nie wynik PASS.

### B. Domknąć ochronę danych i odzyskiwanie

- Rozszerzyć próby zapisu/importu/eksportu o brak miejsca, odmowę uprawnień, blokady i awaryjne zakończenie procesu w kontrolowanych fazach. Używać wydzielonego dysku/katalogu testowego, nigdy zapełniania dysku użytkownika.
- Sprawdzić zachowanie poprzednich danych, komunikat PL/EN, ponowienie po usunięciu problemu oraz pozostałości plików tymczasowych. Nie obiecywać odporności na odcięcie zasilania wyłącznie na podstawie zamknięcia procesu.
- Przygotować reprezentatywne zestawy danych ze wspieranych starych schematów/wydań: profile, zagnieżdżone foldery, ikony, ustawienia i układ. Sprawdzić migrację, kopię sprzed migracji i ręczne odtworzenie; nowa pusta baza nie zastępuje takiej próby.
- Osobno zweryfikować powiązania poświadczeń, własnych komend i kolekcji po imporcie/zmianie profilu/odtworzeniu. Sprawdzić zablokowany i niedostępny magazyn systemowy oraz łączenie bez zapamiętania hasła.

**Warunek zakończenia:** brak utraty lub cichego resetu wcześniej zatwierdzonych danych w sprawdzonych awariach, udane ponowienie i udokumentowane odtworzenie. Kopia SQLite nadal nie jest pełną kopią sekretów i pozostałych magazynów.

### C. Zamknąć scenariusze codziennej pracy i awarii sesji

Przyrost ergonomii SFTP po 0.43.0: kopiowanie tekstowych nazw/ścieżek również z zachowanego widoku offline, filtrowanie z klawiatury oraz spójna blokada nieaktualnych operacji. [Weryfikacja](reports/sftp-clipboard-2026-10-19.md). Próby rzeczywistych łączy i pozostałe warunki tego etapu pozostają otwarte.

- SSH: fizyczne AltGr/układy/IME, mysz, aplikacje terminalowe, uśpienie/wznowienie oraz ponowne połączenie. Sprawdzić kilka sesji i trzy/cztery panele bez mieszania celu wejścia.
- SFTP: upload/download i katalogi, nadpisanie, anulowanie, brak miejsca/quota oraz utrata sieci w różnych fazach; ochrona oryginału i porównanie SHA-256 po ponowieniu. Uwzględnić serwer inny niż OpenSSH i pozostawione `.part`.
- RDP: uzupełnić [macierz](rdp-test-matrix.md) dla deklarowanych Windows/Linux: hasło/certyfikat, schowek w obie strony i obsługiwane pliki, klawiatura/fokus, rozmiar panelu/dokowanie oraz utrata sieci. Konta AD/Microsoft wymagają własnego stanowiska, jeśli mają wejść do potwierdzonego zakresu.
- VNC i Serial: rzeczywisty serwer oraz fizyczny adapter, błędne parametry, rozłączenie i ponowienie. Zakresu tych protokołów nie rozszerzać przy okazji stabilizacji.
- Monitoring, Komendy i Network Tools: reprezentatywne systemy/urządzenia, quoting i tożsamość celu, Start/Stop/zamykanie oraz wyłączenie pluginu bez przerwania SSH/SFTP i bez utraty zapisanej zawartości.

**Warunek zakończenia:** scenariusze z etapu A mają wynik i dowód; błąd jednego połączenia/modułu nie zawiesza aplikacji, nie kieruje danych do innej sesji i pozwala bezpiecznie zamknąć lub ponowić operację.

### D. Sprawdzić długą pracę, platformy i spójność UI

- Zaplanować pierwszą próbę długotrwałą: co najmniej 4 godziny z ośmioma sesjami oraz 100 cykli otwarcia/ponowienia/zamknięcia, osobno na Windows i deklarowanym Linuxie. To cel przyszłej próby, nie opis wykonanego testu; wcześniejsze 10 minut nie zamyka tego zadania.
- Zmierzyć pamięć, CPU, uchwyty/wątki lub odpowiedniki Linux przed rozgrzaniem, podczas pracy i po zamknięciu sesji. Przed próbą zapisać środowisko i progi regresji względem pomiaru bazowego; wyjaśnić narastanie zasobów między cyklami.
- Sprawdzić duże drzewa/własne grafiki, sortowanie i wyszukiwanie, rzeczywiste przeciąganie serwerów/plików/kart, podziały i osobne okna. Zachować dotychczasowe wyniki 5000 profili jako pomiar ograniczony do opisanej konfiguracji.
- Na natywnym Linuxie przejść instalację, Secret Service/KWallet, zablokowanie/odblokowanie portfela oraz osadzone RDP pod XWayland. Nie zastępować tej próby testem Xvfb/WSLg.
- Wykonać przegląd PL/EN, pięciu motywów, większej czcionki, klawiatury i dostępności. Szczególnie sprawdzić ustawienia, dialogi, nakładanie tekstu, restart języka i położenie okien.
- Fizycznie sprawdzić monitory 100%/150%/200%, przenoszenie i odłączenie ekranu. Przy obecnie potwierdzonym ekranie 100% pozostałe skale nadal wymagają stanowiska testowego.

**Warunek zakończenia:** brak zawieszania, niewyjaśnionego przyrostu zasobów po powtarzanych cyklach, niedostępnych istotnych kontrolek i okien poza ekranem; raporty dla każdej zadeklarowanej konfiguracji.

### E. Przygotować i sprawdzić kandydata RC

- Po domknięciu wcześniejszych etapów wykonać przegląd bezpieczeństwa poświadczeń, zaufania hostów/certyfikatów, importów, wykonywania komend, eksportów i logów. Aktualny audyt zależności jest osobną bramką, nie dowodem pełnego bezpieczeństwa.
- Zamrozić zakres i commit kandydata; ustalić jego numer zgodny z narzędziami wersjonowania. Przygotować paczki i raport według [procedury wydania](releasing.md) oraz [checklisty RC](rc-checklist.md).
- Uruchomić pełne właściwe testy Windows/Linux, locked restore, Release build, audyt, kontrole licencji/zasobów/źródeł i gotowych plików. Dawne wyniki nie zaliczają nowego commita ani payloadu.
- Na czystym koncie/VM sprawdzić świeży Setup, rejestrację Apps/menu Start, aktualizację ze wskazanych wspieranych wersji z rzeczywistą zawartością testową, naprawę i deinstalację bez kasowania danych użytkownika.
- Sprawdzić instalację i start paczek Linux na deklarowanych systemach, zależności natywne, instrukcje PL/EN i listę ograniczeń. Zebrać wersję/commit/środowisko oraz wyniki PASS/FAIL/SKIP; SKIP nie zamyka wymaganej próby.

**Warunek zakończenia:** kompletne paczki RC z sumami, pełnymi wymaganymi wynikami i bez błędów blokujących; gotowa instrukcja instalacji, aktualizacji i odzyskiwania.

### F. Pilotaż, decyzja o 1.0 i publikacja

- Udostępnić RC wybranym testerom po decyzji maintenera o dystrybucji. Ustalić okres i listę zadań pilotażu oraz zebrać odtwarzalne zgłoszenia bez haseł, kluczy i prywatnych baz.
- Poprawić błędy blokujące; po zmianach sprawdzić regresje, a końcowy kandydat musi ponownie spełnić wszystkie wymagane bramki. Nie zmieniać po cichu wcześniej udostępnionych paczek.
- Ustalić GitHub, prywatny kanał bezpieczeństwa, miejsce pobierania i decyzję o podpisie Windows. Uruchomienie zdalnego CI i publikacja wymagają autoryzowanego pushu; sama gotowa konfiguracja Actions nie zalicza CI.
- Dopiero po spełnieniu definicji gotowości przygotować **1.0.0**, changelog, instrukcje, pakiety Windows/Linux/źródeł i sumy SHA-256. Commit, ProductVersion, paczki i nowy anotowany tag `v1.0.0` muszą być zgodne; zakończyć `scripts/check-release-state.ps1 -RequireCurrentRelease`.
- Zdalna publikacja wymaga osobnej instrukcji użytkownika. Po wydaniu prowadzić zgłoszenia i poprawki 1.0.x; nowe funkcje planować w następnym cyklu.

**Warunek zakończenia:** zaakceptowany pilotaż i gotowa, jednoznacznie identyfikowalna wersja 1.0.0; publikacja dopiero po decyzji maintenera.

## Co dokładnie blokuje status stabilny

- **P0:** utrata danych, ujawnienie sekretu, obejście zaufania albo wykonanie operacji na niewłaściwym serwerze. Każdy nierozwiązany przypadek blokuje wydanie.
- **P1:** awaria/zawieszenie aplikacji, niedziałający podstawowy scenariusz deklarowanej funkcji lub brak bezpiecznego odzyskania po typowej awarii. Każdy nierozwiązany przypadek blokuje wydanie.
- Pozostałe błędy muszą mieć ocenę, opis wpływu i świadomą decyzję o odłożeniu. Udokumentowanie ograniczenia nie zmienia błędu P0/P1 w akceptowalny problem kosmetyczny.
- Wszystkie wymagane wiersze uzgodnionej macierzy mają PASS na końcowym kandydacie; wymagane FAIL/SKIP i brak dowodu pozostawiają etap otwarty. Nie deklarujemy testów każdej dystrybucji i każdego urządzenia, tylko jawny, sprawdzony zakres.
- Zaliczone są bramki techniczne, odtworzenie/aktualizacja, długa praca i pilotaż, a dokumentacja wsparcia odpowiada faktycznym wynikom. Gotowości nie mierzymy liczbą nowych funkcji ani samą zmianą numeru wersji.

## Najbliższa partia wykonawcza

Po 0.43.0 nie powtarzać jako nowych zaległości zaliczonych prób odmowy zapisu profilu, blokady SQLite, przerwania zapisu komend, równoległych kolekcji i cichej utraty SSH/SFTP. Ich zakres opisuje [raport 0.43.0](reports/release-verification-0.43.0.md); migracje, przerwanie procesu i ENOSPC mają również wcześniejsze dowody. Następna partia to szersza akceptacja RDP, innych serwerów/WAN i długiej pracy. Po przygotowaniu przez maintenera maszyn i dostępu uzupełnić natywne A/C/D, KWallet, fizyczne suspend/resume oraz instalację z rejestracją na czystej VM. Pełny przenośny backup sekretów i pilotaż pozostają oddzielnymi zagadnieniami.

Ten dokument ustala plan; nie zalicza tych przyszłych prób. Aktualizacja samego planu wymaga kontroli treści/linków i commita, bez buildu aplikacji, zmiany wersji ani nowego wydania.

## 1. Dane i odzyskiwanie — w toku

- [x] Krok roboczy po 0.44.0: kontrola kopii obejmuje wymagane tabele/kolumny i wszystkie zapisane profile, z walidacją tożsamości, JSON/modelu i budżetem 32 MiB na rekord. Kontrola pozostawia kopię bez zmian; nie odczytuje sekretów i nie zalicza pełnego odtworzenia pozostałych magazynów. [Zakres i granice](reports/backup-content-2026-10-21.md).
- [x] 0.43.0: atomowa edycja tożsamości profilu z rotacją lokalnego powiązania hasła, zachowanie hasła przy odmowie zapisu/usunięcia w SQLite, ostrzeżenie po niepotwierdzonym sprzątaniu i kontrola nieaktualnego edytora. Eksport/import/duplikat nie przenoszą powiązania. Schemat 4 z kopią chroni przed starszym klientem ignorującym to pole.
- [x] 0.43.0: kontrola równoległych zapisów Komend i transakcyjne indeksy/historia Network Tools między oknami; błąd/anulowanie po części chunków zachowuje poprzednią bibliotekę. Rzeczywista blokada pisarza SQLite, odmowa katalogu Unix i ponowienie na własnych danych. [Zakres](reports/release-verification-0.43.0.md).
- [x] Spójna ręczna kopia SQLite, także z zatwierdzonymi danymi WAL; sekcja ustawień PL/EN.
- [x] Zmiana robocza po 0.39.0: przegląd 20 najnowszych kopii w ustawieniach, data/rozmiar/schemat/powód, kopiowanie ścieżki i otwieranie folderu; pomijanie plików częściowych. Lista metadanych nie potwierdza zawartości kopii i nie podmienia bazy.
- [x] Sprawdzona kopia przed migracją starego schematu; odmowa migracji przy błędzie kopii.
- [x] Komunikat o uszkodzonej/niedostępnej/nowszej bazie i zamknięcie bez zapisu układu zamiast automatycznego resetu.
- [x] Celowany test odtworzenia profili, folderów i ustawień w osobnym katalogu; [instrukcja i ograniczenia](database-backups.md).
- [x] 0.40.0: ręczna kontrola SQLite i zgodności schematu wybranej kopii w ustawieniach, bez modyfikacji pliku, migracji ani przywracania; komunikaty PL/EN dla poprawnych, uszkodzonych i niedostępnych kopii. Nie zastępuje próby odtworzenia ani weryfikacji sekretów. [Testy i granice](reports/backup-verification-2026-10-17.md).
- [x] Zapis profilu oraz tworzenie zagnieżdżonych folderów w jednej transakcji; kontrolowany błąd zapisu zachowuje poprzedni stan, ponowienie działa.
- [x] Przenoszenie profilu razem z tworzeniem folderu docelowego w jednej transakcji; brak częściowo utworzonego drzewa po błędzie.
- [x] Rekurencyjna zmiana nazwy folderu obejmuje także brakujących rodziców celu, ikony i przypisania profili w jednej transakcji. [Testy trzech poprawek i granice](reports/profile-atomicity-2026-10-17.md).
- [x] Eksport konfiguracji do pliku nie opróżnia poprzedniego JSON-a przed zapisem; kontrolowane błędy, anulowanie i blokada pliku zachowują poprzednie dane, a ponowienie działa. [Zakres testów i ograniczenia](reports/configuration-export-2026-10-17.md).
- [x] Przenoszenie usuniętego wpisu nie tworzy folderów; niezmieniona nazwa jest akceptowana, a zmiana nazwy nie gubi dodatkowych pól profilu ani konkurującej edycji.
- [x] Sprawdzenie zawartości folderu i usuwanie współdzielą transakcję; próba z konkurującym zapisem chroni nowy profil, a starsze niejawne foldery nadrzędne zachowują obsługę.
- [x] Eksport czyta profile/foldery/ikony z jednego obrazu bazy; błędy zapisu importu wycofują całość i pozwalają na retry bez zmiany lokalnych ikon. [Sześć zadań, dowody i ograniczenia](reports/configuration-consistency-2026-10-17.md).
- [x] 0.39.1: eksport Komend i Network Tools przez zamknięty plik tymczasowy, zachowanie poprzedniego pliku przy błędzie/anulowaniu; prywatne uprawnienia nowych plików Unix.
- [x] 0.39.1: odrzucanie powtórzonych pól konfiguracji przed zapisem oraz odczyt/walidacja/import poza wątkiem UI.
- [ ] Pozostałe fazy przerwania operacji, blokady, odmowy dostępu i dodatkowe historyczne zestawy danych; próby 0.41.0 poniżej zamykają tylko wymieniony zakres.
- [x] 0.41.0: reprezentatywne dane schematów 0–2, kopia/migracja/odtworzenie, przerwanie własnego procesu podczas eksportu i transakcji zmiany drzewa oraz rzeczywisty ENOSPC/SQLITE_FULL na tmpfs 8 MiB z udanym retry. Pięć testów ochrony indeksu i sekretu kolekcji. Nie zamyka awarii zasilania, każdej fazy operacji ani spójności pełnego backupu magazynów. [Zakres](reports/release-verification-0.41.0.md).
- [ ] Dalsza weryfikacja zgodności bazy z osobnym magazynem poświadczeń, komend i kolekcji; kopia bazy nie jest pełnym backupem tych danych.

## 2. Rdzeń MVP — do domknięcia akceptacji

- [x] 0.51.0: krótkie pliki tymczasowe SFTP dla długich nazw oraz kontrola rozmiaru/daty źródła przed podmianą celu. Własny OpenSSH, PL/EN, zachowanie poprzednich celów, sprzątanie i SHA-256 po retry. Kontrola metadanych nie zamyka wszystkich równoległych zmian, quota ani WAN. [Zakres wydania](reports/release-verification-0.51.0.md).
- [x] 0.51.0: wskazanie serwera zapisanej karty na liście, z wyczyszczeniem filtrów i rozwinięciem przodków; szybkie/usunięte wpisy pozostają bez zmian. Rozwijanie/zwijanie wszystkich folderów z zapamiętaniem zwykłego stanu i tymczasowym stanem wyszukiwania; 32 poziomy w PL/EN.
- [x] 0.43.0: limit bezczynności transferu i odczytu katalogu SFTP, produkcyjna kontrola cichego SSH, pomijanie zajętego kanału Komend bez fałszywego rozłączenia, zachowanie kontekstu SFTP przy reconnect i jawne retry z SHA-256. Cztery równoległe SSH/16 reconnect i zamykanie cichego łącza sprawdzone na własnym OpenSSH Windows/Linux. Nie zalicza fizycznego suspend/resume, wszystkich serwerów ani wielogodzinnej pracy.
- [x] Regresja kolejki SFTP: zatrzymane odświeżanie listy nie zajmuje slotów kopiowania; błąd odświeżania zachowuje sukces transferu; anulowanie zadania oczekującego i ponowienie po utracie SSH do pierwotnego celu sprawdzone na własnym OpenSSH.
- [x] Ręczne odzyskiwanie wykrytego zerwania połączenia przeglądania SFTP przez Odśwież/F5 bez resetu terminala, historii i filtrów; odmowa zmienionego klucza hosta i anulowanie przy zamknięciu sprawdzone na własnym OpenSSH.
- [x] Komunikaty błędów transferów PL/EN z instrukcjami ponowienia i poprawne chmod; rzeczywista odmowa zapisu oraz zniknięcie źródła zachowują istniejące cele, a retry po naprawie uprawnień kończy się zgodnymi bajtami na własnym OpenSSH. [Zakres i ograniczenia](reports/transfer-errors-2026-10-16.md).
- [x] Ostrzeżenie o niepotwierdzonym sprzątaniu `.part` z dokładną ścieżką, zachowane także po udanym retry; bez automatycznego usuwania starszych plików częściowych. Sprzątanie podmienionego dowiązania zachowuje jego cel.
- [x] Filtr błędów/anulowanych/ostrzeżeń i wyszukiwanie pełnych opisów/ścieżek w Transferach; czytelny komunikat o anulowaniu i ponowieniu od początku w PL/EN.
- [x] Celowana macierz przerwania uploadu i anulowania downloadu na własnym OpenSSH: zachowanie oryginałów, SHA-256 po retry, cicha utrata sieci, zwolnienie slotów i ręczne sprzątanie tylko wskazanego `.part`. [Zakres i ograniczenia](reports/sftp-interruption-2026-10-16.md).
- [ ] SSH: fizyczna klawiatura/AltGr/IME, zaznaczanie i mysz, aplikacje pełnoekranowe, uśpienie i reconnect.
- [ ] SFTP: rzeczywiste WAN, brak miejsca/quota, pozostałe fazy przerwania, inne serwery SFTP i pozostałe `.part` po awaryjnym zamknięciu procesu. Powyższe celowane próby nie zamykają pełnej macierzy.
- [x] Profile: zachowanie zaznaczonego folderu przy sort/reload, usuwanie zaznaczenia ukrytego profilu i blokada Enter przed przebudową; PL/EN.
- [x] Profile: tymczasowe rozwijanie wyników wyszukiwania bez nadpisywania zwykłego stanu folderów; sortowanie i zwinięcie odtwarzane po restarcie.
- [x] Profile, zmiany robocze po 0.37.0: odroczone tworzenie wierszy serwerów i menu folderów; dostępne puste foldery bez nakładającego się komunikatu. PL/EN, ikony, sortowanie i filtrowanie na 5000 profilach/200 folderach sprawdzone na Windows; [pomiary i granice](reports/browser-scalability-2026-10-16.md).
- [ ] Profile: dalsza skala i głębokość drzew, duża liczba własnych grafik oraz rzeczywiste gesty drag and drop i fizyczna klawiatura. Automatyczne testy 5000 profili nie zastępują tych prób ani wielogodzinnego pomiaru zasobów.
- [x] Pięć zadań roboczych po 0.40.0: nowe wpisy w zaznaczonym folderze; zmiana nazwy z zachowaniem ikon, wyboru i zwiniętych gałęzi; usuwanie pierwotnie potwierdzonego folderu bez starych stanów po odtworzeniu; upuszczanie profili w folderze/katalogu głównym; lokalne skróty F2/Delete/Ctrl+D/Enter. Celowana automatyzacja obejmuje PL/EN, głębokość 32 i zdarzenia drag and drop, bez deklaracji fizycznej akceptacji. [Zakres](reports/browser-workflow-2026-10-17.md).
- [x] Krok roboczy po 0.40.0: niezmienione grafiki profilu są ponownie używane przy zmianie metadanych; podmiana/usunięcie/odłączenie zwalnia stare bitmapy. Celowana próba obejmuje 96 różnych grafik, 2880 zmian metadanych, rzeczywisty edytor i błędne starsze dane. Nie zamyka dalszej skali drzew ani wielogodzinnych pomiarów. [Zakres](reports/profile-artwork-2026-10-17.md).
- [x] Monitoring, zmiany robocze po 0.37.0: fallback starszego iproute2 bez JSON, bramy IPv4/IPv6 oraz odrzucanie niespójnej próbki CPU bez fałszywego 0%. Celowane testy parserów i własny OpenSSH z kontrolą PL/EN; [zakres i ograniczenia](reports/monitoring-compatibility-2026-10-16.md).
- [ ] Monitoring: reprezentatywne dystrybucje, uprawnienia i pozostałe formaty/braki narzędzi. Test wrappera iproute2 nie zastępuje rzeczywistych starszych dystrybucji ani akceptacji routingu IPv6.
- [ ] Sesje: wielogodzinna praca, powtarzane otwieranie/reconnect/zamykanie i pomiary zasobów.

## 3. RDP, VNC i Serial — do domknięcia akceptacji

- [ ] Uzupełnić [macierz RDP](rdp-test-matrix.md): lokalne/AD, certyfikaty, schowek w obie strony, pliki, fokus, fizyczne klawisze i awarie sieci.
- [x] 0.39.1: jednoznaczny opis pełnego ekranu klienta w osobnym oknie; osadzony pulpit korzysta z rozmiaru panelu, bez utraty zapisanej preferencji.
- [ ] Zweryfikować osadzone FreeRDP na natywnym KDE/XWayland; test izolowany Xvfb nie zastępuje akceptacji pulpitu.
- [ ] Rzeczywiste implementacje VNC i długie sesje; jawnie zachować ograniczenie braku TLS/identyfikacji serwera.
- [ ] Fizyczne adaptery Serial: parametry, flow control i odłączanie w trakcie pracy.

## 4. Dodatkowe moduły — do domknięcia akceptacji

- [x] 0.39.1: zachowanie uszkodzonych indeksów historii/kolekcji bez cichego resetu; walidacja payloadów kolekcji, pól null i zapamiętanych DNS/OID/WoL przed zastosowaniem.
- [x] 0.39.1: awaria opcjonalnego zapisu historii/preferencji nie blokuje diagnostyki i nie usuwa jej wyników; ostrzeżenie PL/EN. [Dziesięć zadań i granice prób](reports/release-verification-0.39.1.md).
- [x] 0.40.0: Stop/Escape anuluje oczekujące obliczenie podsieci; ręczny Start usuwa stare przeliczenie z kolejki. Zamknięcie karty czeka na jej operacje i chroni przed spóźnionym wczytaniem kolekcji; wielokrotne Restart/Stop i anulowanie HTTP sprawdzane na własnym loopback. [Zakres i ograniczenia](reports/network-cancellation-2026-10-17.md).
- [ ] Network Tools: rzeczywiste IPv6/VPN/DNS/ICMP, urządzenia SNMP i wybudzanie WoL; wielokrotne anulowanie/zamykanie kart.
- [ ] Komendy: zgodność na reprezentatywnych dystrybucjach i rzeczywistym Windows SSH; quoting, parametry i ochrona tożsamości celu po reconnect.
- [ ] Ponowny przegląd bezpieczeństwa nowych modułów, eksportów i logów.

## 5. Systemy, sprzęt i UX — do domknięcia

- [x] Jawna [macierz środowisk i checklista natywnej akceptacji](platform-support.md), w tym podana konfiguracja Kali 2026.3/KDE Plasma 6.7.4/Wayland/100%; odczytowy preflight Linux bez ingerencji w portfel użytkownika.
- [x] Dialogi reagują na zmiany ekranów/DPI i odzyskują pierwotne limity po powrocie na większy ekran. Automatyczna próba PL/EN, pięciu motywów, tekstu 16, symulacji 100/150/200/100%, klawiatury i zwalniania zamkniętych okien; [zakres](reports/platform-ux-2026-10-16.md).
- [x] Ograniczona 10-minutowa próba Windows: osiem rzeczywistych sesji na własnym OpenSSH, 36 reconnect, SFTP/dokowanie i zapis CPU/pamięci/uchwytów/wątków oraz stanu po zamknięciu. Nie zastępuje wielogodzinnego soak ani próby Linux.
- [ ] Ustalić dokładną listę wspieranych systemów 1.0 i wykonać jej akceptację. Pierwotny zakres: Windows 10/11, Ubuntu, Kubuntu, Debian i Fedora; WSLg nie zastępuje natywnego desktopu.
- [ ] GNOME/KDE, odblokowany/zablokowany keyring i KWallet; X11/Wayland w deklarowanym zakresie.
- [ ] Fizyczne monitory o różnych skalach i odłączenie monitora; ustawienia, PL/EN, motywy, większa czcionka, fokus i dostępność.
- [ ] Pomiary CPU/pamięci/uchwytów podczas długiej pracy i powtarzanych cykli sesji.

## 6. RC i dystrybucja — przed 1.0

- [x] Sześć zadań roboczych po 0.40.0: automatyzacja aktualizacji starszego Setupu, napraw brakujących plików, uszkodzonych DLL i brakującego deinstalatora, bramka raportu audytu NuGet oraz spójne instrukcje RC/GitHub. Rzeczywista próba 0.39.1 → 0.40.0 i naprawy w izolowanych katalogach Windows; 23 kontrolowane decyzje audytu oraz aktualny wynik czterech projektów. [Dokładny zakres i ograniczenia](reports/rc-preparation-2026-10-17.md).
- [ ] Aktualizacja ze wspieranych starszych wersji na czystych maszynach i naprawa niepełnej instalacji.
- [ ] Pełne bramki [wydania](releasing.md), aktualny audyt zależności, spójne przewodniki i licencje.
- [ ] Po decyzji o publikacji: GitHub/CI, kanał prywatnych zgłoszeń bezpieczeństwa i pilotaż RC. Podpis wydawcy Windows jest zalecany przed szeroką dystrybucją.
- [ ] Brak otwartych błędów krytycznych dotyczących bezpieczeństwa, utraty danych, zawieszania i podstawowych połączeń; zakończona macierz deklarowanego wsparcia.

Do konkretnego kandydata stosuj [checklistę RC](rc-checklist.md). Zakończone próby lokalne nie zaliczają czystych maszyn, rejestracji pominiętej w fixture ani przyszłych paczek z nowszego commita.

Synchronizacja, bastion/forwarding, zewnętrzne pluginy, nowe platformy, integracje hypervisorów, YAML i dalsze biblioteki producentów nie blokują 1.0. Od 0.38.0 Linux RDP korzysta z osadzenia X11/XWayland. Natywny backend Wayland pozostaje dalszym zakresem. Ograniczony zakres wsparcia musi być jawny; nie nadajemy statusu stabilnego konfiguracji, której nie sprawdziliśmy.

Od 0.39.0 Monitoring Linux i Komendy są przełączanymi pluginami wbudowanymi. Zewnętrzne paczki pluginów nadal pozostają dalszym zakresem; [kontrakt i obsługa](plugins.md).
