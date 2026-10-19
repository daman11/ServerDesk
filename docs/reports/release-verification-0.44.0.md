# ServerDesk 0.44.0 — zakres weryfikacji

Public Beta, 2026-10-19. Wydanie zbiera kroki po 0.43.0: Pomoc offline, ergonomię SFTP, menu połączeń, kolory akcentu, bezpieczniejszą diagnostykę i integrację plików RDP Windows. Finalny commit i wyniki paczek są utrwalane w `VERIFICATION.md` w folderze wydania; wybrane dowody w `artifacts/reports/release-0.44.0/`.

## Zakres bramek wydania

Przygotowanie obejmuje locked restore, Release build i testy jednostkowe Windows/Linux, audyt NuGet i fixture bramki audytu, bieżącą macierz headless UI, DataProbe, natywne sprawdzenie hosta/OLE RDP bez logowania oraz start/restart gotowego Windows EXE. Instalator jest sprawdzany na własnym katalogu i danych, z pominięciem rejestracji na komputerze maintenera. Paczki Windows/Linux/źródeł przechodzą kontrolę wersji, treści, licencji i sum.

Wykonane kontrole przygotowawcze:

- Windows i Ubuntu 24.04.4/WSL: locked restore i Release build bez ostrzeżeń/błędów, po **959/959 testów jednostkowych**, bez pominiętych przypadków.
- Audyt NuGet: cztery projekty, bez zgłoszonych advisory, diagnostyki i niepełnych źródeł; **23/23** fixture bramki audytu. To zapytanie o znane advisory, nie certyfikat bezpieczeństwa.
- DataProbe: po osiem przypadków Windows/Linux — historyczne schematy 0–3 i migracja, przerwanie eksportu oraz trzy transakcje folderów, zachowanie danych i ponowienie. Osobno rzeczywisty ENOSPC na tmpfs 8 MiB, z zachowaniem oryginałów i udanym retry.
- Natywny host w gotowym Windows EXE: zachowanie HWND w split/3/4 panelach, odpięciu, dokowaniu i resize, następnie sprzątnięcie. Bez połączenia lub logowania do serwera.
- Gotowy EXE: trzy starty z korektą położenia, maksymalizacją i odłączonym monitorem; przycisk restartu z PL na EN i zachowaniem profilu; pełny reset na jednorazowej bazie z zachowaniem kopii. To próby własnych procesów, nie fizycznych monitorów.
- Kandydat Setup: poprawny tekst UTF-8 skompilowanej strony powitalnej, aktualizacja 0.43.0 → 0.44.0, świeża instalacja w ścieżce ze spacjami/polskimi znakami, odmowa zajętego katalogu, naprawa brakujących/uszkodzonych DLL i deinstalatora oraz zachowanie danych i obcych plików. Rejestracja Apps/menu Start pominięta. Początkowy runner błędnie sprawdzał kod GUI przed końcem procesu; poprawiono oczekiwanie i powtórzono kontrolę, bez zaliczania tego uruchomienia jako PASS.
- Kandydat DEB/TAR: zgodne wersje, architektura, uprawnienia, wszystkie bajty payloadu, launcher i wpis pulpitu. Końcowe paczki są odbudowywane z czystego commita i ponownie sprawdzane.
- Audyt prezentacji na Windows/Linux wykrył brak metadanych ikony dla przycisku „Wyczyść wyszukiwanie” w Pomocy. Uzupełniono wspólną mapę akcji; nieudany scenariusz i pozostałą część macierzy powtórzono pomyślnie po przebudowie harnessów. Pierwotne wyniki pozostają w dowodach i nie są liczone jako PASS.
- Historyczny test przenoszenia nieistniejącego profilu oczekiwał w oknie samego komunikatu. Obecna diagnostyka prawidłowo dodaje przetłumaczony identyfikator błędu. Test zachowuje ścisłe sprawdzenie komunikatu i dodatkowo wymaga ID w poprawnym formacie; nie zmieniono produkcyjnej obsługi błędu ani zasad przenoszenia.
- Dodatkowy scenariusz panelu RDP używał wyłącznie Windows `ComSpec` do własnego procesu zastępczego. Fixture otrzymał odpowiednik `/bin/sleep` na Linuxie. To test zarządzania własnym procesem bez serwera/logowania, nie test rzeczywistego FreeRDP. Funkcje drop/export OLE Windows są pominięte w macierzy Linux zamiast liczyć je jako zaliczone.
- Końcowa macierz UI: **52/52 Windows** (51 headless i natywny host/OLE RDP), **50/50 obsługiwanych scenariuszy Linux**. Obejmuje bieżącą listę CI oraz diagnostykę, schowek SFTP, Network Tools, ergonomię, Pomoc, akcenty, menu połączeń i właściwe panele/kontrolery RDP. Layout eksportu PL/EN sprawdzono dodatkowo na 360/640 px; funkcje Windows nie są zaliczeniem transferu na Linuxie.

Końcowy wynik macierzy UI i ponowionych bramek paczek podaje `VERIFICATION.md`. Wydanie jest zamrażane dopiero po ich zaliczeniu; sam opis zakresu nie zastępuje raportu końcowego.

## RDP: co pozostaje niepotwierdzone

Nowe profile domyślnie wybierają osadzenie, a lokalny zrzut do Windows RDP ma [wcześniejsze dowody na własnym serwerze](rdp-desktop-drop-2026-10-19.md). Nie powtarzamy rzeczywistego serwera ani VM w tym etapie.

Odwrotny mostek Ctrl+C → podgląd → uchwyt przeciągania jest **eksperymentalny**, z widoczną informacją PL/EN w oknie. Walidacja deskryptorów, granice strumieni, natywne vtable COM i układ mają lokalne testy. Próba bezpośredniego handlera Windows Shell zakończyła się timeoutem, bez utworzenia plików. Fizyczny zrzut do Eksploratora i rzeczywisty RDP → lokalny system nie są zaliczone. Nie wdrożono ciągłego gestu ze zdalnego Eksploratora ani zdalnego pomocnika. [Szczegółowy raport](rdp-clipboard-export-2026-10-19.md).

## Granice

Wyłącznie własne syntetyczne dane i katalogi. Bez zapisanych połączeń, haseł i plików maintenera. WSL/headless nie zastępują akceptacji natywnego Kali/KDE/KWallet, fizycznej klawiatury/IME, myszy, mieszanych DPI, adapterów Serial ani długiego pilotażu. Nie wykonujemy nowych prób VMware, rzeczywistych RDP/network ani rejestracji Setupu na czystej VM; wcześniejsze [wyniki VM](vm-mvp-2026-10-19.md) dotyczą swoich buildów. Brak publikacji zdalnej i zaufanego podpisu Windows.
