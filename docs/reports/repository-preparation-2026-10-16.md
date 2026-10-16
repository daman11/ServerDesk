# Przygotowanie repozytorium — 2026-10-16

Pierwszy commit importuje źródła wersji 0.31.1. Oznaczono go niezmiennym tagiem `v0.31.1`. Archiwum wygenerowane z tego tagu porównano bajtowo przez SHA-256 z manifestem zamrożonego wydania: dokładnie 475 plików, bez dodatkowych plików. Starsze wersje zachowują dokumentację i lokalne paczki, bez dorabiania historii commitów.

Porządki zachowują trzy warstwy kodu aplikacji. 58 historycznych dokumentów przeniesiono do `docs/releases/` oraz `docs/reports/`, poprawiono linki i dodano indeks dokumentacji. Źródła mają reguły końców linii i edytora; upstream notices oraz grafiki z przypiętymi hashami zachowują oryginalne bajty. Wykluczono lokalne SDK, narzędzia, paczki, bazy, logi i materiał poświadczeń. Dane autora są wyłącznie w lokalnej konfiguracji i metadanych commitów.

Dodano CI Windows/Ubuntu z pełnymi SHA oficjalnych akcji, uprawnieniem `contents: read` i checkout bez zachowywania poświadczeń. Są szablony zgłoszeń/pull requestów oraz konfiguracja Dependabot. SDK 10.0.401, publiczne źródło NuGet i lockfile wszystkich dziesięciu projektów umożliwiają odtwarzalny restore. Główne rozwiązanie nadal zawiera aplikację i testy; dodatkowe harnessy buduje się jawnie.

## Weryfikacja lokalna

- Release build rozwiązania i **489/489 testów**, zero błędów i ostrzeżeń kompilatora.
- Eksport wyłącznie z indeksu Git do nowego katalogu: brak lokalnego `.tools` i poprzednich artefaktów, poprawny check repozytorium, ponowny locked restore/build i **489/489 testów**. Korzystał z zewnętrznego zainstalowanego SDK oraz współdzielonego cache NuGet. Publikacja Windows z tego eksportu zawiera nowe ścieżki notatek i raportu oraz poprawny manifest.
- Narzędzia UiSmoke i RdpProbe kompilują się bez ostrzeżeń; NetworkProbe publikuje się dla Linux przy zablokowanym grafie zależności. Lokalizacja oraz macierz przypomnień/restartu PL→EN i EN→PL przechodzą na nowym harnessie.
- Rzeczywisty OpenSSH/SFTP w Ubuntu/WSL: alias `serverdesk_fixture`, klucz z passphrase, PTY/ANSI/resize, monitoring, EOF, utrata połączenia, backpressure, równoległe terminale, reconnect oraz SFTP Unicode/checksum/chmod/rename/delete/overwrite/cancel. Zmieniony klucz hosta jest blokowany. Tymczasowe sshd zatrzymano. Nie zmieniano kont systemowych ani nie używano poświadczeń użytkownika.
- `actionlint 1.7.12` akceptuje workflow; sprawdzono składnię wszystkich skryptów PowerShell i Bash. `check-repository.ps1` sprawdza zawartość, ignorowane pliki, linki Markdown, przenośne ścieżki, przypięcie Actions i hashe notices.
- `gitleaks 8.30.1` sprawdza historię i przygotowany diff z redagowaniem wyników. Domyślne reguły pozostają włączone; dwa dokładne, opisane wyjątki obejmują publiczną stałą fixture SNMP i niesekretną linię konstruktora UI. Nie znaleziono rzeczywistego materiału poświadczeń. Narzędzia pobrano z oficjalnych wydań i sprawdzono opublikowane sumy.

Surowe logi są lokalnie w ignorowanym `artifacts/git-*`. Nie są częścią źródeł. Repozytorium pozostaje lokalne, bez `origin` i bez pushu. Pełne CI na runnerach GitHub wymaga przyszłego utworzenia repozytorium i pierwszego pushu; nie zastępuje go walidacja lokalna. Nie jest to ponowna akceptacja prywatnego RDP, sprzętu Serial ani fizycznych monitorów.
