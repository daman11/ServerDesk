# Aktualizacje z GitHub Releases

Od **0.45.0** w Ustawienia → Bezpieczeństwo → Aktualizacje programu oraz w „O programie” jest przycisk **Sprawdź aktualizacje**. Od **0.45.1** sprawdzanie przy starcie jest domyślnie włączone; wcześniej zapisane wyłączenie pozostaje respektowane. Zmiana tej opcji zaczyna działać po zapisaniu, od następnego uruchomienia. Pobieranie i instalacja wymagają decyzji użytkownika.

Gdy jest dostępna nowa wersja, pasek w głównym oknie oferuje:

- **Aktualizuj** — otwiera okno pobierania, weryfikacji i instalacji.
- **Pomiń tę wersję** — zapamiętuje dokładny numer wydania i nie przypomina o nim przy kolejnych startach. Następna nowsza wersja znów wywoła powiadomienie. Ręczne sprawdzanie w ustawieniach nadal pozwala pobrać pominiętą wersję.
- **Przypomnij przy następnym uruchomieniu** — ukrywa pasek w bieżącym oknie, bez trwałego pomijania wersji.

W **0.45.0** sprawdzanie przy starcie było domyślnie wyłączone. Aby przetestować przejście **0.45.0 → 0.45.1**, włącz tę opcję, zapisz i uruchom program ponownie albo użyj ręcznego sprawdzenia. Rozbudowany pasek z trzema decyzjami jest częścią 0.45.1 i zadziała dla późniejszych wydań. Nie zmieniamy już opublikowanej 0.45.0.

Źródło jest wbudowane: [daman11/ServerDesk](https://github.com/daman11/ServerDesk/releases). Aplikacja odczytuje do 100 ostatnich wydań z publicznego API bez tokenu/konta użytkownika. Wybiera najwyższą nowszą wersję `vMAJOR.MINOR.PATCH`, pomija szkice, nigdy nie proponuje obniżenia wersji. Beta 0.x uwzględnia prerelease; stabilna wersja 1.x pomija prerelease. Nowe wydanie musi zawierać dokładnie jeden `ServerDesk-VERSION-win-x64-Setup.exe` i SHA-256 w polu `digest` GitHuba. Niepełne metadane blokują aktualizację.

## Pobranie i instalacja

Pobieranie pokazuje postęp i można je anulować. Instalator jest zapisywany w osobnym katalogu `updates/IDENTYFIKATOR` w katalogu danych aplikacji. Plik `.part` otrzymuje docelową nazwę dopiero po sprawdzeniu długości i SHA-256. Dozwolone są wyłącznie HTTPS, oficjalne repozytorium i przekierowania do wskazanych hostów zasobów GitHuba. Odpowiedzi/metadane, rozmiar instalatora, liczba przekierowań i czas operacji są ograniczone.

Instalacja z programu obsługuje **Windows x64 zainstalowany przez Setup**. Wersja ZIP i Linux korzystają z przycisku do Releases oraz ręcznej aktualizacji paczki; nie zmieniamy ich instalacji na inną. Pierwsze przejście ze starszej wersji bez aktualizatora do 0.45.0 wymaga uruchomienia nowego Setupu ręcznie.

Przed instalacją powstaje sprawdzona kopia SQLite. Nie zawiera ona haseł systemowego magazynu, prywatnych kluczy ani sekretów pluginów; istniejące dane pozostają w swoich katalogach. Użytkownik potwierdza zamknięcie sesji i transferów. Pomocnik jest uruchamiany z katalogu pobrania, czeka na zakończenie właściwej instancji (PID i czas startu), ponownie pobiera metadane tego wydania, sprawdza plik i uruchamia Setup bez podwyższania uprawnień, w tym samym katalogu instalacji. Nie zabija procesów. Po sukcesie uruchamia program ponownie.

Jeśli program się nie zamknie, metadane się zmienią, sieć przestanie działać lub instalator zgłosi błąd, pomocnik przerywa. Jego szczegóły ze StackTrace są w `updates/IDENTYFIKATOR/error.log`, wynik sukcesu w `result.txt`. Instalator i kopia pozostają do odzyskiwania. Setup ma dotychczasową ochronę przed zajętymi plikami. Nie jest transakcyjny: błąd dysku w trakcie kopiowania może wymagać ręcznego ponowienia Setupu. Stare katalogi aktualizacji można usunąć po potwierdzeniu poprawnej pracy; program nie usuwa automatycznie kopii ani innych danych.

## Zaufanie i prywatność

GitHub otrzymuje adres IP, User-Agent aktualizatora i żądania HTTP. Profile, adresy serwerów, hasła, historia i logi nie są przesyłane. Błędy aplikacji mają identyfikator i trafiają do jej lokalnych logów. Brak sieci nie blokuje startu ani połączeń SSH/SFTP/RDP.

Zaufanie opiera się na HTTPS oraz kontroli repozytorium GitHub. SHA-256 wykrywa uszkodzenie lub niezgodność pobranego pliku, **nie jest podpisem wydawcy** i nie chroni przed przejęciem konta repozytorium. Aktualizator nie ma jeszcze osobnego klucza podpisującego manifest ani Authenticode. Windows/SmartScreen nadal może zgłaszać nieznanego wydawcę. Zabezpiecz konto GitHub 2FA/passkey; nie wyłączaj ochrony systemu.

## Publikowanie przez maintenera

Kod i [wydanie 0.45.0 Public Beta](https://github.com/daman11/ServerDesk/releases/tag/v0.45.0) są publiczne od 2026-10-21. Lokalny `origin` wskazuje `https://github.com/daman11/ServerDesk.git`. Dla kolejnych wydań wykonaj najpierw [pełną procedurę wydania](releasing.md), przegląd historii/sekretów oraz sprawdzenie tagu. Nie dołączaj `.tools`, danych ani `artifacts` do historii Git.

Zainstaluj [oficjalne GitHub CLI](https://cli.github.com). Możesz użyć fine-grained PAT ograniczonego do `daman11/ServerDesk`, z Contents i Workflows = Read and write oraz wymaganym Metadata = Read. Podaj go CLI przez `GH_TOKEN` tylko na czas procesu albo przez bezpieczny magazyn poświadczeń. Token publikacji nie jest potrzebny aplikacji. Alternatywny `gh auth login` w przeglądarce może prosić o szerszy dostęp — przejrzyj uprawnienia przed zgodą. Nie umieszczaj tokenu w czacie, kodzie, URL ani skrypcie.

```powershell
# Tylko podgląd celu i plików; nic nie wysyła.
pwsh -File scripts/publish-github.ps1
# Po przeglądzie i świadomej decyzji publikacji:
pwsh -File scripts/publish-github.ps1 -Publish
```

Skrypt wymaga czystego `main`, bieżącego anotowanego tagu, dziesięciu sprawdzonych paczek i właściwego origin. Wysyła `main` i wyłącznie bieżący tag, tworzy szkic, dołącza pliki, porównuje rozmiary/SHA-256 z GitHubem i dopiero publikuje. Błąd wysyłki pozostawia szkic; nie zastępuje istniejącego wydania ani tagu. Pierwsza publikacja jest osobną decyzją — przygotowanie lokalnego wydania nie upoważnia do pushu.

Alternatywnie użyj strony Releases: tag `vVERSION` na końcowym commicie, wszystkie dziesięć plików z `artifacts/releases/VERSION`, prerelease dla 0.x, publikacja dopiero po pełnym uploadzie. Nie używamy `/releases/latest`, ponieważ nie obejmuje wydań beta. Patrz [API wydań](https://docs.github.com/en/rest/releases/releases) i [metadane zasobów](https://docs.github.com/en/rest/releases/assets).
