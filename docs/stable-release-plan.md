# Droga do ServerDesk 1.0

Punkt wyjścia: 0.40.0 Public Beta. Funkcje rdzenia i dodatków istnieją; stan implementacji oraz granice wcześniejszych testów opisuje [MVP](mvp-status.md). Poniższa lista jest bieżącym planem stabilizacji, a nie deklaracją gotowości 1.0.

Małe kroki kończą się commitem, celowanymi sprawdzeniami i EXE do uruchomienia z folderu. Pełna macierz i paczki są przeznaczone dla większych etapów, RC i wydania 1.0.

## 1. Dane i odzyskiwanie — w toku

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
- [ ] Szersze testy przerwanego zapisu, braku miejsca, blokad i aktualizacji historycznych zestawów danych.
- [ ] Dalsza weryfikacja zgodności bazy z osobnym magazynem poświadczeń, komend i kolekcji; kopia bazy nie jest pełnym backupem tych danych.

## 2. Rdzeń MVP — do domknięcia akceptacji

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

- [ ] Aktualizacja ze wspieranych starszych wersji na czystych maszynach i naprawa niepełnej instalacji.
- [ ] Pełne bramki [wydania](releasing.md), aktualny audyt zależności, spójne przewodniki i licencje.
- [ ] Po decyzji o publikacji: GitHub/CI, kanał prywatnych zgłoszeń bezpieczeństwa i pilotaż RC. Podpis wydawcy Windows jest zalecany przed szeroką dystrybucją.
- [ ] Brak otwartych błędów krytycznych dotyczących bezpieczeństwa, utraty danych, zawieszania i podstawowych połączeń; zakończona macierz deklarowanego wsparcia.

Synchronizacja, bastion/forwarding, zewnętrzne pluginy, nowe platformy, integracje hypervisorów, YAML i dalsze biblioteki producentów nie blokują 1.0. Od 0.38.0 Linux RDP korzysta z osadzenia X11/XWayland. Natywny backend Wayland pozostaje dalszym zakresem. Ograniczony zakres wsparcia musi być jawny; nie nadajemy statusu stabilnego konfiguracji, której nie sprawdziliśmy.

Od 0.39.0 Monitoring Linux i Komendy są przełączanymi pluginami wbudowanymi. Zewnętrzne paczki pluginów nadal pozostają dalszym zakresem; [kontrakt i obsługa](plugins.md).
