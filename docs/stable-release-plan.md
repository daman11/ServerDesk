# Droga do ServerDesk 1.0

Punkt wyjścia: 0.37.0 Public Beta. Funkcje rdzenia i dodatków istnieją; stan implementacji oraz granice wcześniejszych testów opisuje [MVP](mvp-status.md). Poniższa lista jest bieżącym planem stabilizacji, a nie deklaracją gotowości 1.0.

Małe kroki kończą się commitem, celowanymi sprawdzeniami i EXE do uruchomienia z folderu. Pełna macierz i paczki są przeznaczone dla większych etapów, RC i wydania 1.0.

## 1. Dane i odzyskiwanie — w toku

- [x] Spójna ręczna kopia SQLite, także z zatwierdzonymi danymi WAL; sekcja ustawień PL/EN.
- [x] Sprawdzona kopia przed migracją starego schematu; odmowa migracji przy błędzie kopii.
- [x] Komunikat o uszkodzonej/niedostępnej/nowszej bazie i zamknięcie bez zapisu układu zamiast automatycznego resetu.
- [x] Celowany test odtworzenia profili, folderów i ustawień w osobnym katalogu; [instrukcja i ograniczenia](database-backups.md).
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
- [ ] Profile: większe drzewa oraz rzeczywiste gesty drag and drop i fizyczna klawiatura. Automatyczne testy drzewa i sortowania nie zastępują tych prób.
- [x] Monitoring, zmiany robocze po 0.37.0: fallback starszego iproute2 bez JSON, bramy IPv4/IPv6 oraz odrzucanie niespójnej próbki CPU bez fałszywego 0%. Celowane testy parserów i własny OpenSSH z kontrolą PL/EN; [zakres i ograniczenia](reports/monitoring-compatibility-2026-10-16.md).
- [ ] Monitoring: reprezentatywne dystrybucje, uprawnienia i pozostałe formaty/braki narzędzi. Test wrappera iproute2 nie zastępuje rzeczywistych starszych dystrybucji ani akceptacji routingu IPv6.
- [ ] Sesje: wielogodzinna praca, powtarzane otwieranie/reconnect/zamykanie i pomiary zasobów.

## 3. RDP, VNC i Serial — do domknięcia akceptacji

- [ ] Uzupełnić [macierz RDP](rdp-test-matrix.md): lokalne/AD, certyfikaty, schowek w obie strony, pliki, fokus, fizyczne klawisze i awarie sieci.
- [ ] Wyjaśnić lub rozszerzyć pełny ekran osadzonego RDP; obecnie pełny ekran dotyczy klienta zewnętrznego. Zweryfikować zewnętrzne FreeRDP na natywnym desktopie Linux.
- [ ] Rzeczywiste implementacje VNC i długie sesje; jawnie zachować ograniczenie braku TLS/identyfikacji serwera.
- [ ] Fizyczne adaptery Serial: parametry, flow control i odłączanie w trakcie pracy.

## 4. Dodatkowe moduły — do domknięcia akceptacji

- [ ] Network Tools: rzeczywiste IPv6/VPN/DNS/ICMP, urządzenia SNMP i wybudzanie WoL; wielokrotne anulowanie/zamykanie kart.
- [ ] Komendy: zgodność na reprezentatywnych dystrybucjach i rzeczywistym Windows SSH; quoting, parametry i ochrona tożsamości celu po reconnect.
- [ ] Ponowny przegląd bezpieczeństwa nowych modułów, eksportów i logów.

## 5. Systemy, sprzęt i UX — do domknięcia

- [ ] Ustalić dokładną listę wspieranych systemów 1.0 i wykonać jej akceptację. Pierwotny zakres: Windows 10/11, Ubuntu, Kubuntu, Debian i Fedora; WSLg nie zastępuje natywnego desktopu.
- [ ] GNOME/KDE, odblokowany/zablokowany keyring i KWallet; X11/Wayland w deklarowanym zakresie.
- [ ] Fizyczne monitory o różnych skalach i odłączenie monitora; ustawienia, PL/EN, motywy, większa czcionka, fokus i dostępność.
- [ ] Pomiary CPU/pamięci/uchwytów podczas długiej pracy i powtarzanych cykli sesji.

## 6. RC i dystrybucja — przed 1.0

- [ ] Aktualizacja ze wspieranych starszych wersji na czystych maszynach i naprawa niepełnej instalacji.
- [ ] Pełne bramki [wydania](releasing.md), aktualny audyt zależności, spójne przewodniki i licencje.
- [ ] Po decyzji o publikacji: GitHub/CI, kanał prywatnych zgłoszeń bezpieczeństwa i pilotaż RC. Podpis wydawcy Windows jest zalecany przed szeroką dystrybucją.
- [ ] Brak otwartych błędów krytycznych dotyczących bezpieczeństwa, utraty danych, zawieszania i podstawowych połączeń; zakończona macierz deklarowanego wsparcia.

Synchronizacja, bastion/forwarding, pluginy, nowe platformy, integracje hypervisorów, YAML i dalsze biblioteki producentów nie blokują 1.0. Zewnętrzne RDP na Linux jest dopuszczonym w pierwotnym MVP wariantem. Ograniczony zakres wsparcia musi być jawny; nie nadajemy statusu stabilnego konfiguracji, której nie sprawdziliśmy.
