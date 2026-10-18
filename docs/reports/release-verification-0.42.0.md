# ServerDesk 0.42.0 — zakres weryfikacji, 2026-10-18

Public Beta, wydanie zbierające zmiany od 0.41.0. Końcowy commit, wyniki paczek oraz sumy podaje `VERIFICATION.md` w folderze wydania. Dowody lokalne: `artifacts/reports/release-0.42.0/`.

## Nietypowe przypadki

- Dwanaście nowych przypadków regresyjnych: nazwy zakończone LF/CRLF, znormalizowane ścieżki z `.` i `..`, alias celu i źródła, alias z kolejnym aliasem w ścieżce celu oraz dozwolony eksport przez alias poza logami; Windows Junction i rzeczywiste dowiązania katalogów Unix.
- Rotacja między podglądem i eksportem: brak starego pliku zachowuje istniejącą kopię, ponowny odczyt widzi nowy plik. Podgląd pliku sparse 32 MiB pozostaje ograniczony; brak katalogu docelowego nie tworzy go ani nie zmienia logu. Dwanaście równoległych podglądów/eksportów zachowuje bajty i nie zostawia stagingu.
- Przed poprawką odtworzono dwa błędy walidacji na Windows i cztery na Linux. Następnie poprawiono ścisłe dopasowanie całej nazwy i kontrolę fizycznej ścieżki katalogu. Testy operują wyłącznie na swoich katalogach tymczasowych.
- Dotychczasowe próby obejmują też blokady plików, odmowy SQLite, brak opcjonalnych katalogów, trwałe usuwanie profilu i odczyt po restarcie procesu, migracje i kontrolowane awarie własnych procesów. Nie używają zapisanych połączeń ani poświadczeń maintenera.

## Bramki wydania

Zaliczone: 849/849 testów jednostkowych na Windows oraz 849/849 na Ubuntu/WSL, kompilacje bez ostrzeżeń i błędów. Pełny zestaw UI: 40/40 scenariuszy Windows i 41/41 Linux. Dodatkowo na obu systemach zaliczono zapis/odtworzenie układu po nagłym zakończeniu procesu. DataProbe: 7/7 na każdym systemie, osobno rzeczywisty ENOSPC/SQLITE_FULL z ponowieniem na tmpfs 8 MiB.

Audyt NuGet obejmuje cztery projekty, bez zgłoszonych advisory i diagnostyki; 23 fixture bramki audytu zaliczone. Kontrole repozytorium, workflow i historii Git zaliczone. Skaner błędnie uznał literalną listę nazw protokołów w historycznym raporcie ergonomii za klucz API; wyjątek jest ograniczony do dokładnie tego dopasowania i ścieżki.

Macierz ujawniła dwa nieaktualne oczekiwania harnessu (dawny nagłówek wielkimi literami i przyciski filtrów zastąpione listą). Dostosowano je i powtórzono scenariusze na obu systemach. Uzupełniono metadane ikon dwóch akcji tagów; następnie powtórzono kontrolę prezentacji i filtrów na Windows/Linux. Test zewnętrznego edytora w lokalnym SDK Linux powtórzono z poprawnym `DOTNET_ROOT` dla procesu potomnego. Nie zmienia to wymagań samodzielnych paczek.

Locked restore, kompilacje Release, pełny zestaw testów jednostkowych Windows/Linux, bieżąca lista UI CI rozszerzona o nowe moduły oraz network-tools/app-polish. Testy danych DataProbe i ograniczony Linux tmpfs. Audyt zależności i regresje jego bramki, kontrola treści repozytorium i przypiętych licencji.

Weryfikacja końcowych paczek obejmuje natywny start i host RDP Windows bez logowania, restart EXE, tekst instalatora, aktualizację z 0.41.0 i naprawy, pełne payloady ZIP/TAR/DEB, eksport źródeł i SHA-256. Rejestracja instalatora jest celowo pominięta na komputerze maintenera. Wyniki liczbowe i końcowy commit są zapisywane dopiero po ukończeniu bramek w dołączonym `VERIFICATION.md`.

Zaliczono również start samodzielnej paczki Linux i widoczne okno WSLg na własnym PID/danych. Obejrzano podgląd logów PL/EN w Graphite/Light przy 920×680 i 600×480. Są to obrazy headless, nie akceptacja monitora fizycznego.

## Granice

Brak prób natywnego Kali/KDE/Wayland, czystej VM z rejestracją instalatora, fizycznego Serial/klawiatury/mieszanego DPI, pełnej macierzy protokołów i wielogodzinnego pilotażu. Użytkownik odłożył VMware do przygotowania maszyn. WSL i automatyczny headless nie potwierdzają tych środowisk. Native save picker/folder launcher mają testy z wstrzykniętą odpowiedzią, nie pełną akceptację systemowego dialogu. Rozwiązywanie stałych dowiązań nie daje gwarancji wobec procesu złośliwie zmieniającego je równolegle. Audyt advisory nie jest pełnym audytem bezpieczeństwa. Publiczne CI, podpis Windows i publikacja zdalna nie są zaliczone.

[Plan stabilnej wersji](../stable-release-plan.md), [macierz platform](../platform-support.md), [szczegóły logów](application-logs-2026-10-18.md), [usuwanie profili](profile-deletion-2026-10-18.md).
