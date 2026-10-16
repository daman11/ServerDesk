# ServerDesk 0.31.1 — weryfikacja poprawki, 2026-10-16

Test starego Setup 0.31.0 odczytał przez Win32 rzeczywiste napisy okna powitalnego i odtworzył uszkodzone separatory oraz polskie znaki. Kompilacja NSIS używa teraz `/INPUTCHARSET UTF8`; `Unicode true` dotyczy formatu wynikowego EXE, nie odczytu źródeł.

489/489 testów jednostkowych Windows, bez pominiętych. Nowe przypadki obejmują język docelowy bez zmiany globalnej kultury, zachowanie argumentów formatu, oddzielne argumenty procesu dla apphost/dotnet, nieprawidłowe przekazanie uruchomienia, oczekiwanie na żywego poprzednika i odróżnienie zakończonego/ponownie użytego PID.

UiSmoke Windows i Ubuntu/WSL, PL→EN i EN→PL: podgląd bez zapisu, anulowanie, odrzucenie niepoprawnych ustawień także przy „Zapisz i uruchom ponownie”, zapis i ponowne otwarcie, ukrywanie przypomnienia, przywrócenie bieżącego języka. Rzeczywisty loopback VNC pozostaje połączony po anulowaniu restartu lub błędzie uruchomienia nowej instancji. Potwierdzony restart uruchamia callback jeden raz, kończy sesję i zamyka główne okno przez istniejącą ścieżkę shutdown. Windows dodatkowo sprawdza obecność zapisanej karty w snapshot po restarcie.

Macierz ustawień: 680×500, 760×700 i 960×720, cztery kategorie, tekst 16, Dark/Light/Graphite; kontrola nawigacji klawiaturą i wskaźnikiem oraz granic kontrolek. Główne okno 900×600 zachowuje pasek tytułu, przypomnienie i przestrzeń sesji bez nakładania. Obejrzano zrzuty obu języków.

Natywny `--published-restart` na Windows naciska przycisk własnego okna testowego i uruchamia rzeczywisty EXE 0.31.1. Przed wyjściem starego procesu nowy nie otwiera okna ani nie rozpoczyna logowania startu. Po wyjściu wczytuje English z tej samej bazy, zachowuje profil z polskimi znakami i zamyka się z kodem 0. Jest to zdarzenie przycisku w harnessie, nie fizyczny gest. Osobny test uruchamia EXE trzykrotnie z poprzednim położeniem poza ekranem, maksymalizacją i odłączonym monitorem. Natywny host RDP zachowuje HWND podczas split/floating/redock/resize i zwalnia go po zamknięciu; test nie loguje się do prywatnego serwera. Natywny proces Linux tworzy okno w stanie IsViewable pod WSLg. Pełny handoff procesu GUI sprawdzono na Windows; Linux ma macierz UI i natywny start.

Dystrybucję sprawdzają `scripts/test-installer-text.ps1` (rzeczywiste teksty skompilowanego Setup bez instalowania), `scripts/test-installer.ps1` (instalacja, upgrade, blokada zajętych plików, deinstalacja i zachowanie danych), `scripts/test-release-content.ps1` oraz `scripts/test-linux-release.py` (zgodność payloadów, notices, wykluczenia plików prywatnych i sumy). Błąd blokuje zamrożenie zestawu. Raporty odtwarzalnych kontroli pozostają w `artifacts/mvp-0311-*`, a zamrożony zestaw zawiera raport oraz SHA-256.

Audyt NuGet 2026-10-16: kod 0, źródło api.nuget.org, cztery projekty, brak zgłoszonych pakietów podatnych i problemów źródła. To kontrola advisory, nie pełny audyt bezpieczeństwa. Zależności są niezmienione. Kompilacja rozwiązania, harness Windows i payloady nie zgłosiły ostrzeżeń ani błędów.

Zakres tej poprawki nie powtarza testów WAN/RDP ani wielogodzinnych sesji z poprzednich etapów. Odtwarzanie kart pozostaje bez automatycznego logowania. Linux poza Ubuntu/WSLg, fizyczne DPI/klawiatury i sprzęt Serial nadal wymagają akceptacji. Brak nowych zależności i zmian formatu danych. [Ograniczenia bety](../known-issues.md).
