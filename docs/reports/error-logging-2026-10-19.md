# Stabilność obsługi błędów i logowanie — 2026-10-19

Celowany przyrost po 0.43.0, bez migracji, zmiany protokołów ani wydania.

## Ustalenia i zmiany

- `MainWindow.Safe` mógł otwierać kilka modalnych okien z równoczesnych callbacków, próbować wyświetlać błąd po zamknięciu właściciela i sam kończyć się wyjątkiem, gdy prezentacja dialogu zawiodła. Teraz każde zdarzenie jest logowane, ale działa najwyżej jeden ogólny dialog. Zakończenie zadania po zamknięciu właściciela nie otwiera okna. Błąd prezentacji jest odrębnie rejestrowany. Po zamknięciu komunikatu kolejne błędy mogą być pokazane normalnie.
- Ogólny komunikat otrzymuje kopiowalny losowy `ErrorId`, zgodny z wpisem w logu. Dla nazwanych delegatów zapisuje nazwę operacji; dla lambd korzysta z nazwy wywołującego lub stałej akcji menu/przycisku zamiast samego `<BuildTree>b__...`. Błędy w tle otrzymują nazwę miejsca wywołania. Nie dodaje adresów, komend ani poświadczeń jako kontekstu.
- Ustawienia, historia, kopie bazy, zarządzanie zaufaniem SSH oraz zapisy opcjonalnych modułów miały wpisy zawierające tylko typ wyjątku albo pomijały jego zapis. Zastąpiono je wspólnym raportowaniem z typami/kodami/stosami przyczyn. Anulowanie zachowuje dotychczasowe odrębne zachowanie. Błąd oczekiwania na zakończenie narzędzi podczas zamykania przechodzi przez `Safe`, zamiast uciekać z asynchronicznego callbacku okna.
- Globalny nieobsłużony wyjątek zapisuje diagnostykę na poziomie Fatal; nie jest oznaczany jako naprawiony. Nieobserwowany błąd zadania zapisuje Warning i zostaje zaobserwowany. Główna pętla rejestruje awarię przed zamknięciem loggera i kończy proces kodem 1; wyjście i błąd kończący proces opróżniają bufor. Błąd zwalniania kontenera usług jest rejestrowany przed opróżnieniem.
- Każdy wpis plikowy ma PID; start dodaje build, system i runtime. Logger używa współdzielonego zapisu, rotacji dziennej oraz po osiągnięciu 4 MiB i retencji 14 plików. Limit może zostać przekroczony o jeden wpis; to liczba plików, nie dni. Istniejący podgląd/eksport obsługuje nazwy segmentów.
- `ExceptionDiagnostics` pomija `Message` i `Data`, zachowując zwykłe stosy oraz kody SQLite/socket/SSH. Formatowanie ma limit 64 KiB i 16 wyjątków, a wadliwy getter `StackTrace` nie powoduje wtórnej awarii. Duży wynik ma znacznik obcięcia. Szczegóły lokalnych błędów plikowych z wcześniejszych ścieżek nadal mogą zawierać ścieżki; nie jest to automatyczna anonimizacja całych logów.

## Weryfikacja

- Celowane testy jednostkowe `ExceptionDiagnosticsTests`, `OperationErrorTests`, `SshErrorMessagesTests`, `LocalizationTests`: **37/37 PASS**. Obejmują m.in. wadliwy formatter, nadmierny stos, łańcuch 100 przyczyn i brak sentinelów `Message`/`Data` w diagnostyce.
- `tools/ServerDesk.UiSmoke --error-logging`: **PASS**, PL/EN. Jedno okno dla równoczesnych błędów synchronicznych/asynchronicznych, odrębne ID w logach i zgodność ID komunikatu, ponowne pokazanie po zamknięciu, anulowanie bez błędu, zadanie kończące się po zamknięciu właściciela, brak dostępu do magazynu haseł. Sprawdza także klasyfikację Fatal/Warning i zaobserwowanie błędu zadania.
- Ten sam harness zapisuje rzeczywiste pliki przez dwa współdzielące logger-y, wymusza rotację z małym limitem, sprawdza retencję, obecność końcowych zdarzeń/ID/stosów, brak prywatnego sentinela oraz widoczność segmentów przez `ApplicationLogFiles`. To dwie instancje loggera w jednym procesie, nie akceptacja wielu procesów na udziale sieciowym.
- Kompilacja harnessu Release: **0 ostrzeżeń, 0 błędów**. Zrzuty okna błędu PL/EN w `artifacts/reports/error-logging-2026-10-19/`.
- Regresje `--configuration-export` i `--application-logs`: **PASS**, PL/EN. Zachowano komunikaty eksportu, pliki po błędzie i możliwość ponowienia; podgląd logów nadal wyszukuje błędy, eksportuje cały plik, obsługuje anulowanie/niedostępny katalog i mieści akcje w małym oknie.
- Natywny EXE z outputu harnessu, `--published-startup`: **PASS**, trzy osobne procesy z własną bazą, korekcja pozycji, maksymalizacja, powrót po utracie monitora i zwykłe zamknięcie z kodem 0. Trzy wpisy startu zawierają PID/build/OS/runtime; brak ERR/FTL. Inicjalizacja loggera poprzedza Avalonia, a jego zamknięcie jest w `finally` punktu wejścia. Nie wywoływano awarii procesu na siłę.
- Nowy scenariusz `--error-logging` dodano do przyszłego CI Windows/Linux. Wyniki lokalne dotyczą Windows.
- Kontrola zawartości repozytorium: **PASS**, 791 plików; `git diff --check`: **PASS**. Zasoby Pomocy PL/EN mają zgodne ID i nowe instrukcje wyszukiwania identyfikatora błędu. Build deweloperski: `artifacts/dev-harness/ServerDesk.App.exe`.

Wykorzystano wyłącznie losowe katalogi i bazy testowe. Testy zdarzeń krytycznych wywołują handler z atrapami; nie powodują rzeczywistej awarii procesu. Nie wykonano pełnej macierzy protokołów, testu wielodniowego ani natywnej akceptacji Linuxa. Bez podniesienia wersji, pakowania wydania i publikacji.
