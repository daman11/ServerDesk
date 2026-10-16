# Bezpieczeństwo i stabilność — ServerDesk 0.22.2

Data: 2026-10-15. Rozwinięcie [audytu 0.22.1](security-review-0.22.1.md), bez nowych bibliotek i bez logowania do prywatnego serwera RDP. Testy pracowały na odizolowanych plikach, bazach, własnych serwerach VNC na loopback oraz osobnym D-Bus/Secret Service.

## Interfejs odporny na zatrzymaną transmisję VNC

Odbiornik biblioteki utrzymuje blokadę bufora od początku do końca ramki. Dotychczas odczyt obrazu z wątku UI czekał na tę samą blokadę. Serwer, który przesłał tylko fragment obrazu i pozostawił otwarte połączenie, mógł więc zatrzymać interfejs aplikacji. Test przed poprawką odtworzył oczekiwanie.

`VncFramebuffer.Read` próbuje teraz uzyskać dostęp bez czekania. Gdy ramka nadal jest odbierana, kontrolka zachowuje ostatnią skopiowaną bitmapę. Nie odczytuje bufora w trakcie zapisu i może obsługiwać zdarzenia, zmienić rozmiar lub rozłączyć sesję. Po zwolnieniu bufora odbiornik wywołuje następną aktualizację. Możliwe jest pominięcie ramek pośrednich przy dużym obciążeniu; nie wprowadzono drugiej pełnej kopii bufora sieciowego.

Test UI na Windows i Linux potwierdza zajętą blokadę odbiornika przy realnym połączeniu z serwerem milczącym w połowie ramki, wykonanie zadania dispatchera, zmianę szerokości okna i rozłączenie przyciskiem. Test jednostkowy dodatkowo sprawdza, że po zwolnieniu blokady bufor można ponownie odczytać. Sześć kolejnych połączeń i zamknięć przy takim serwerze kończy się zwolnieniem sesji.

## Wiarygodny stan po utracie VNC i natychmiastowym końcu sesji

Biblioteka mogła zgłosić Closed przed ustawieniem InterruptionCause. W efekcie uszkodzony pakiet był przedstawiany jako normalne zamknięcie. Adapter zapamiętuje teraz pierwszy błąd odbiornika/nadajnika przed przekazaniem go bibliotece. Do prezentacji trafia wyłącznie typ błędu, bez danych sesji.

Dodatkowo obserwację końca VNC/Serial uruchamia się po ustawieniu początkowej prezentacji połączenia. Natychmiastowa awaria nie może zostać nadpisana późniejszym komunikatem Połączono. Test jednostkowy i rzeczywisty test UI z nadmierną długością skompresowanego pakietu potwierdzają stan awarii i brak zablokowanych przycisków.

## Zapis i odczyt poświadczeń Windows

Wcześniejszy zapis bezpośrednio do pliku docelowego mógł uszkodzić poprzednią wersję przy awarii zapisu. Nowy zapis:

1. Sprawdza limit sekretu i szyfruje DPAPI CurrentUser poza wątkiem UI.
2. Tworzy unikalny plik tymczasowy w tym samym katalogu, zawierający wyłącznie szyfrogram.
3. Kończy zapis i opróżnia bufor pliku; przed podmianą sprawdza anulowanie.
4. Podmienia cel i sprząta własny plik tymczasowy po błędzie lub anulowaniu.

Testy błędu po częściowym zapisie, anulowania przed publikacją, zablokowanego celu i nieudanego pierwszego zapisu potwierdzają zachowanie oryginału i sprzątanie. Czytelnik widzi poprzedni kompletny plik do chwili publikacji nowego. Bufory z jawnym hasłem są zerowane w finally.

Test równoległych podmian odtworzył konflikt rename w Windows. `CredentialStore` kolejkuje teraz odczyt/zapis/usuwanie w swojej instancji, z anulowaniem oczekiwania. Test dwunastu aktualizacji i odczytów nie widzi uszkodzonego szyfrogramu. Usunięcie hasła zlecone po rozpoczętym zapisie czeka na ten zapis, a następnie usuwa wpis, zamiast pozwalać mu odżyć.

Odczyt ma limit 128 KiB szyfrogramu, sekret do 64 KiB UTF-8 i ścisłe dekodowanie UTF-8. Nieczytelny lub uszkodzony plik daje kontrolowany błąd odczytu; plik pozostaje na miejscu. Anulowanie użytkownika jest przekazywane dalej. DPAPI i format istniejących poprawnych wpisów pozostają zgodne z poprzednim wydaniem.

To ochrona przed przerwanym zapisem i współbieżnością w jednej instancji magazynu, nie pełna transakcja między wieloma procesami. Awaria systemu/pliku nie musi gwarantować trwałości ostatniego zapisu na każdym nośniku. Po awarii całego procesu może pozostać zaszyfrowany plik `.part`; nie jest on automatycznie wczytywany jako poświadczenie.

## Odzyskanie dostępu po błędzie magazynu

Kontrolowany błąd odczytu otwiera zwykły dialog logowania z wyjaśnieniem PL/EN i zamaskowanym polem. Zapamiętaj domyślnie pozostaje wyłączone. Można anulować, użyć ręcznie wpisanego hasła tylko w sesji albo jawnie zastąpić zapisany wpis. Oryginalny plik nie jest usuwany przy samym błędzie odczytu.

Testy UI w obu językach i na obu systemach sprawdzają wszystkie trzy ścieżki, brak automatycznego zapisu, zamaskowanie pola i ponowny odczyt po jawnym zastąpieniu. Jednostkowe testy Windows używają rzeczywiście uszkodzonego/przesadnie dużego pliku oraz DPAPI. Log zawiera typ przyczyny; nie zapisuje hasła ani odpowiedzi backendu.

## Granice procesów Secret Service Linux

Wcześniejsze ReadToEndAsync nie ograniczało pamięci zajmowanej przez stdout/stderr. Odczyt jest teraz ograniczony do 64 Ki znaków stdout i 8 Ki znaków stderr, a zwrócony sekret dodatkowo do 64 KiB UTF-8. Nie ma obcinania hasła do limitu: nadmierny wynik zostaje odrzucony.

Oba strumienie są czytane równolegle ze stdin i oczekiwaniem na koniec procesu. Przekroczenie limitu anuluje pozostałe operacje, kończy własny proces z jego drzewem i zwraca kontrolowany błąd. Sekret jest przekazywany przez stdin; argumenty i logi nie zawierają jego wartości. Nadal obowiązuje limit czasu, a jawne anulowanie użytkownika nie jest zamieniane w brak hasła.

Rzeczywisty Secret Service w izolowanej sesji D-Bus przeszedł zapis/odczyt/aktualizację/usunięcie, Unicode i końcowe białe znaki, brak jawnego sekretu w plikach keyringu oraz brak usługi. Osobne procesy testowe odtworzyły nadmierny stdout, nadmierny stderr, wynik Unicode przekraczający limit bajtów, brak odpowiedzi i zablokowanie stdin. Testy potwierdzają szybkie zakończenie i zatrzymanie własnego backendu. Test nie zmienia HOME ani magazynu użytkownika: korzysta z prywatnych katalogów XDG i jednorazowego D-Bus.

## Wyniki i ograniczenia

- 335/335 xUnit, czyli 16 nowych przypadków względem 0.22.1. Build rozwiązania i narzędzia UI: 0 błędów, 0 ostrzeżeń.
- Dowody przed poprawkami: `artifacts/stability-before-0222.txt`; pełny wynik: `artifacts/stability-final-build-tests-0222.txt`.
- Windows UI: `artifacts/stability-ui-0222.txt`; Linux: `artifacts/stability-linux-0222.txt`, w tym Secret Service, VNC i wcześniejsze zabezpieczenia schowka/poświadczeń.
- Komunikaty logowania zostały sprawdzone także wizualnie po polsku i angielsku.
- Szersze testy rzeczywistych serwerów, długich sesji, fuzzingu i lokalnych wyścigów plikowych nadal są potrzebne. Nie przeprowadzono nowego pełnego audytu zależności; wersje pakietów są niezmienione od kontroli 0.22.1.
- VNC pozostaje bez TLS/VeNCrypt i własnego tunelu SSH. VPN lub istniejący tunel nadal jest potrzebny do ochrony transmisji. Paczki nie mają podpisu wydawcy. To wydanie nie usuwa wszystkich ograniczeń opisanych w poprzednim audycie.
