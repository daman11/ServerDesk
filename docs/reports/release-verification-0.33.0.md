# ServerDesk 0.33.0 — zakres weryfikacji, 2026-10-16

Wydanie obejmuje nawigację i bezpieczne zatwierdzanie ścieżek SFTP opisane w [raporcie funkcji](sftp-navigation-2026-10-16.md). Nowe testy wraz z dotychczasowymi: 512/512, bez pominiętych. Układ PL/EN przy 280/520/1000 px i rzeczywiste OpenSSH/SFTP sprawdzono na Windows oraz Ubuntu/WSL. Windows dodatkowo przeszedł regresję dużych list, protokołów, cyklu życia/chrome oraz utraty sieci w SFTP. Szczegóły i granice pomiarów są w raporcie funkcji.

Przed zamrożeniem obowiązują pełne bramki dystrybucji: locked restore/Release build, macierz UI obu systemów, advisory NuGet, manifest i natywny host RDP w opublikowanym EXE bez logowania do serwera, położenie okna, rzeczywisty restart, teksty Setup, izolowana instalacja/upgrade/uninstall, porównanie ZIP/DEB/TAR z payloadami i ponowna kompilacja/testy rozpakowanych źródeł. Domyślna rejestracja Apps/Start menu jest pomijana, jeżeli istnieje instalacja użytkownika. Zwykłe dane i poświadczenia użytkownika nie są fixture.

Payloady powstają z czystego commita. Kontrola wymaga wersji EXE 0.33.0.0 i jego SHA w ProductVersion, zgodności dokumentacji, notices, wykluczenia prywatnych plików oraz kompletności manifestów. Zestaw zawiera dziesięć plików: Setup/ZIP Windows, DEB/TAR Linux, źródła MIT, notatki, ten zakres z wynikiem bramek, metadane, licencję i SHA256SUMS. Źródła są porównywane z plikami Git przy anotowanym tagu v0.33.0. Poprzednie zamrożone wydania pozostają niezmienione.

Końcowe wyniki bramek dystrybucji są dopisywane po ich wykonaniu do VERIFICATION.md zestawu; szczegółowe dowody pozostają w artifacts/reports/release-0.33.0/. Sam dokument zakresu nie potwierdza wykonania nowych testów sprzętu ani CI na GitHub. Wydanie i tag są lokalne.

Publiczna beta MIT. Bez nowych zależności aplikacji i migracji danych. Nie jest to pełne zakończenie pierwotnego MVP: pozostają fizyczne Serial/klawiatury/IME/mieszane DPI, szersza macierz systemów i RDP oraz wielogodzinne pomiary. Brak podpisu zaufanego wydawcy i automatycznej aktualizacji. [Ograniczenia](../known-issues.md).
