# Checklista kandydata do wydania

Punkt 6 [planu 1.0](stable-release-plan.md) jest nadal otwarty. Ta lista służy do przygotowania konkretnego RC; wykonane testy poprzedniego Setupu nie potwierdzają gotowości późniejszych commitów ani nowej paczki.

## Identyfikacja kandydata

- Wersja / commit / docelowe systemy: do ustalenia dla konkretnego RC.
- Bieżące wydanie: [0.51.0 Public Beta](reports/release-verification-0.51.0.md). Udostępnione VM Windows 11/Ubuntu 26 otrzymały wcześniej [osobną macierz roboczą](reports/vm-mvp-2026-10-19.md); jej przerwania i ograniczenia pozostają jawne. VM nie są ponawiane w tym wydaniu na prośbę maintenera. Nie zalicza to całej macierzy 1.0 ani końcowego RC.
- [ ] Czysty końcowy commit, nowy numer wersji i zgodne przewodniki, notatki oraz raport.
- [ ] Paczki pochodzą z tego commita; EXE ma właściwy ProductVersion; źródła i SHA-256 odpowiadają paczkom.
- [ ] Jawne granice wsparcia i znane ograniczenia; testy WSL/headless nie zastępują natywnej akceptacji.

## Bramki techniczne

- [ ] Locked restore, Release build i pełne właściwe testy na deklarowanych systemach według [procedury wydania](releasing.md).
- [ ] Aktualne `scripts/audit-dependencies.ps1`: brak advisory oraz diagnostyki, pełny zestaw projektów. Zachować datę, surowy raport i podsumowanie; nie uznawać niedostępnego źródła za wynik czysty.
- [ ] Zgodne licencje i oryginalne notice/ikony; kontrole zawartości Windows/Linux i archiwum źródeł.
- [ ] Start/restart gotowego EXE, natywny host RDP i tekst skompilowanego instalatora.
- [ ] Świeża instalacja, aktualizacja ze wskazanych starszych wydań i naprawa niepełnej instalacji; zachowanie danych i obcych plików, odmowa zajętego katalogu, pełne usunięcie własnego payloadu.
- [ ] Instalacja i odinstalowanie z rejestracją Apps/menu Start na czystym koncie lub VM; `SKIP` w lokalnym raporcie nie zalicza tej próby.
- [ ] Historyczne dane/migracje i reprezentatywne systemy w odrębnej macierzy; nie zastępować ich nową pustą bazą.

Instalacyjny harness przyjmuje `-Installer`, `-Payload` (katalog matching ZIP), opcjonalny `-PreviousInstaller`, `-Report` i `-SkipRegistration`. Tworzy własne katalogi w `artifacts/` i uruchamia aplikacje wyłącznie z jednorazowym `SERVERDESK_DATA_HOME`. Na komputerze maintenera stosuj `-SkipRegistration`; test rejestracji należy do czystego środowiska. Przed uruchomieniem sprawdź źródło i sumy używanych instalatorów. Nie wskazuj prywatnej bazy ani istniejącego katalogu instalacji jako fixture.

## Publikacja i pilotaż

- [ ] Wybrane repozytorium GitHub, prywatny kanał zgłoszeń bezpieczeństwa i uzupełnione SECURITY.md; bez wymyślonych adresów.
- [ ] Po autoryzowanym pushu zielone Actions na rzeczywistych runnerach, ochrona main i przegląd ustawień repozytorium. Lokalny YAML nie jest dowodem wykonania CI.
- [ ] Wybrana grupa pilotażowa i zapisane wyniki RC: wersja/commit, środowisko, kroki i niesekretne dowody. Nie zbierać baz, haseł ani kluczy.
- [ ] Przejrzane błędy krytyczne bezpieczeństwa, utraty danych, zawieszania i połączeń; brak nierozwiązanych krytycznych zgłoszeń w uzgodnionym zakresie.
- [ ] Decyzja o podpisie Windows i sposobie dystrybucji; nie traktować sumy SHA-256 jako potwierdzenia wydawcy.
- [ ] Osobna zgoda na publikację. Nowy anotowany tag i `check-release-state.ps1 -RequireCurrentRelease` po zamrożeniu paczek; istniejące tagi/pliki pozostają niezmienne.

## Dostępne dowody robocze

[Przygotowania z 2026-10-17](reports/rc-preparation-2026-10-17.md) obejmują 0.39.1 → 0.40.0 i trzy rodzaje napraw Setupu na istniejącym Windows z izolacją katalogów. Audyt/bramka sprawdzane są lokalnie; nowa konfiguracja CI czeka na uruchomienie po publikacji repozytorium. Nie wykonano w tym kroku pełnej macierzy, testów czystej VM, publicznego pilotażu ani nowego wydania.
