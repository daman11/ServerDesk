# ServerDesk 0.36.2 — zakres weryfikacji, 2026-10-16

Zakres: ikony i opisy całej istniejącej aplikacji. Nowy scenariusz presentation-consistency sprawdza produkcyjne wywołania fabryk akcji, brak nieznanych/fallbackowych symboli, geometrię i odrębność ikon układów, osobną kolumnę skrótów, menu główne/folderu/serwera/kart/terminala/SFTP/komend oraz cztery protokoły i dziewięć narzędzi sieciowych. Obejmuje PL/EN, Graphite/Light, 900/1320 px i czcionkę 16, zgodność nazw dostępności, ikon w palecie akcji oraz niezmienność danych użytkownika i wartości żądań. icons sprawdza SHA oryginalnych SVG, galerię wektorów, kolory i zapisywanie/aktualizację ikon profili.

Przed zamrożeniem wymagane są locked restore, Release build, wszystkie testy jednostkowe, macierz 16 scenariuszy CI UI Windows/Linux oraz dodatkowe network-tools/app-polish, advisory NuGet, manifest i natywny host RDP opublikowanego EXE bez logowania, start/pozycja okna/restart, kodowanie tekstów Setup, izolowana instalacja/upgrade/uninstall, zgodność ZIP/TAR/DEB i kompilacja/testy źródeł po rozpakowaniu ZIP. Rozszerzenie workflow nie oznacza wykonania GitHub Actions; wyniki są lokalne.

Faktyczne wyniki i liczniki dopisywane są po wykonaniu do VERIFICATION.md paczki. Dowody: artifacts/reports/release-0.36.2/. Zestaw zawiera dziesięć plików z wersją 0.36.2 i SHA źródłowego commita. Poprzednie 190 zamrożonych plików pozostaje niezmienione. Istniejąca instalacja/skrót lub aktywna kopia użytkownika są zachowywane; rejestrację Apps/Start pomijamy przy istniejącym wpisie.

To weryfikacja prezentacji i regresji istniejących funkcji. Bez nowych zależności, migracji bazy, testu rzeczywistego logowania RDP, fizycznego Serial/IME ani mieszanych monitorów DPI. [Ograniczenia](../known-issues.md).
