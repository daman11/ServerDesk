# ServerDesk 0.41.0 — zakres weryfikacji, 2026-10-17

Kandydat do dalszej akceptacji 1.0, nadal Public Beta. Obejmuje dotychczasowe zmiany drzewa/ikon/grafik, ochronę kolekcji Network Tools oraz rozszerzenie prób danych i instalatora. Pełny końcowy commit, wyniki paczek i SHA-256 zapisuje `VERIFICATION.md` w katalogu wydania; dowody lokalne znajdują się w `artifacts/reports/stable-preparation-2026-10-17/`.

## Zakres testów

- Pełne testy jednostkowe: 784/784 Windows i 784/784 Ubuntu/WSL, bez pominiętych; kompilacje bez ostrzeżeń i błędów. Nowe próby DataProbe: 7/7 na każdym systemie oraz osobny ENOSPC/SQLITE_FULL. Audyt obejmuje cztery projekty bez zgłoszonych advisory/diagnostyki, a jego bramka ma 23 zaliczone fixture. Skan historii Git nie wykrył sekretów; kontrole te mają opisany niżej ograniczony zakres.
- Nowe pięć testów kolekcji wymusza odmowę zapisu indeksu SQLite i niedostępne sprzątanie magazynu. Sprawdza zachowanie poprzednich sekretów/indeksu, pierwotnego błędu, anulowanie, nieznany identyfikator i udane ponowienie. Magazyn jest pamięciowym fixture; systemowe poświadczenia użytkownika nie są odczytywane.
- DataProbe na Windows i Ubuntu/WSL: trzy migracje schematów 0–2 oraz odtworzenie ich kopii, starszy JSON z nieznanym polem, historia, zaufanie hosta, puste foldery, ustawienia i zachowanie odnośnika do brakującego magazynu komend. Import otrzymuje nową tożsamość bez przenoszenia odnośnika do sekretu.
- DataProbe przerywa wyłącznie własne procesy: eksport po zapisie części stagingu oraz trzy serie transakcyjnych zmian nazwy drzewa z 32 profilami. Oryginalny eksport, tożsamości profili, foldery i ikony pozostają spójne; ponowienie i kopia po odzyskaniu działają. Przerwanie procesu nie symuluje odcięcia zasilania.
- Rzeczywisty ENOSPC/SQLITE_FULL na jednorazowym tmpfs 8 MiB w WSL: zachowanie oryginalnego eksportu i profilu, brak częściowego drzewa, odmowa kopii i udane ponowienie po zwolnieniu miejsca. Test nie zapełnia dysku hosta ani danych użytkownika. Osobny skrypt montuje/odmontowuje wyłącznie własny tmpfs.
- Locked restore, Release build i pełne testy jednostkowe Windows/Linux; pełna lista UI CI plus network-tools/app-polish i linux-desktop na Linuxie. Bramka audytu NuGet, kontrola repozytorium, licencji, oryginalnych ikon i workflow.
- Końcowy zestaw UI: 30/30 scenariuszy Windows i 31/31 Linux. Na Windows powtórzono całą macierz po poprawce kolekcji; kontrola ustawień i gestów pozostaje automatyczna/headless.
- Paczki z końcowego czystego commita: natywny host RDP Windows bez logowania, start/restart gotowego EXE, tekst instalatora, aktualizacja 0.40.0 → 0.41.0 i naprawy w izolowanych katalogach; pominięta rejestracja na komputerze maintenera. Kontrola TAR/DEB, wszystkich bajtów payloadu, źródeł i SHA-256.
- Rejestr własnych plików instalatora jest zapisywany jako UTF-8/LF zgodnie z repozytorium; końcowe pakowanie nie zmienia jego bajtów po commicie. Zależności aplikacji pozostają bez zmian; nowy harness ma śledzony lockfile.
- Obejrzano reprezentatywne obrazy ustawień w wąskim oknie 680×500 z większym tekstem i katalog symboli w jasnym motywie. Pozostają rzeczywistymi renderami headless, nie akceptacją fizycznego monitora.

## Otwarte warunki 1.0

Maintener odłożył testy VMware do czasu przygotowania maszyn i dostępu. Utworzona pełna kopia testowa została zatrzymana i usunięta; żaden wynik VM nie jest zaliczony. Testy Ubuntu/WSL nie potwierdzają natywnego KDE/GNOME/Wayland, a kontrola hosta RDP bez logowania nie potwierdza pełnej macierzy RDP.

Pozostają czyste maszyny i rejestracja instalatora, sprzęt Serial, fizyczna klawiatura i monitory DPI, reprezentatywne serwery/urządzenia, wielogodzinna praca oraz pilotaż. Dostępny audyt advisory nie zastępuje pełnego przeglądu bezpieczeństwa. Publiczne CI nie zostało uruchomione; brak zdalnej publikacji, podpisu Windows i wybranego prywatnego kanału zgłoszeń. [Plan](../stable-release-plan.md), [macierz](../platform-support.md), [checklista RC](../rc-checklist.md).
