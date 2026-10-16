# ServerDesk 0.34.0 — zakres weryfikacji, 2026-10-16

Wydanie rozbudowuje ustawienia terminala i SFTP. Weryfikację funkcji i jej granice opisuje [raport](extended-settings-2026-10-16.md). Zachowanie starszych preferencji, walidację i atomowy zapis sprawdzają testy jednostkowe; układ PL/EN, podgląd/anulowanie i rzeczywisty OpenSSH/SFTP — izolowane harnessy Windows oraz Ubuntu/WSL.

Przed zamrożeniem wymagane są locked restore/Release build, testy jednostkowe, macierz UI obu systemów, advisory NuGet, manifest i natywny host RDP w opublikowanym EXE bez logowania, położenie okna, rzeczywisty restart, teksty Setup, izolowana instalacja/upgrade/uninstall, porównanie ZIP/DEB/TAR z payloadami oraz kompilacja/testy rozpakowanych źródeł. Rejestracja Apps/Start menu jest pomijana przy istniejącej instalacji użytkownika. Zwykłe dane i poświadczenia użytkownika nie są fixture.

Payloady powstają z czystego commita. Kontrola wymaga wersji EXE 0.34.0.0 i jego SHA w ProductVersion, zgodności dokumentacji/notices, wykluczenia prywatnych danych i zgodności manifestów. Zestaw ma dziesięć plików: Setup/ZIP Windows, DEB/TAR Linux, źródła MIT, notatki, weryfikację, metadane, licencję i SHA256SUMS. Źródła są porównywane z Git przy anotowanym tagu v0.34.0. Poprzednie wydania pozostają niezmienione.

Końcowe wyniki bramek są dopisywane po wykonaniu do VERIFICATION.md zestawu. Dowody pozostają w artifacts/reports/release-0.34.0/. Dokument zakresu sam nie potwierdza nowych testów sprzętu ani uruchomienia GitHub CI. Wydanie i tag są lokalne.

Publiczna beta MIT. Nie jest to pełne zamknięcie pierwotnego MVP: pozostają fizyczne Serial/klawiatury/IME/mieszane DPI, dalsza macierz systemów i RDP oraz wielogodzinne pomiary. Brak podpisu zaufanego wydawcy i automatycznej aktualizacji. [Ograniczenia](../known-issues.md).
