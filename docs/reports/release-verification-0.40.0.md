# ServerDesk 0.40.0 — zakres weryfikacji, 2026-10-17

Wydanie obejmuje personalizację interfejsu/listy połączeń/kart, sprawdzanie zapisanych kopii SQLite oraz anulowanie i zamykanie akcji Network Tools. Zakres implementacji i testów celowanych: [personalizacja](customization-2026-10-17.md), [kopie bazy](backup-verification-2026-10-17.md), [anulowanie](network-cancellation-2026-10-17.md).

Bramki wydania: locked restore, Release build, pełne testy jednostkowe Windows/Linux, bieżąca lista UI z CI oraz network-tools/app-polish, audyt NuGet, kontrola repozytorium/linków. Końcowe paczki powstają z czystego commitu; sprawdzane są natywny host RDP Windows bez logowania, start/restart gotowego EXE, rzeczywisty tekst instalatora, izolowana instalacja/aktualizacja/deinstalacja, Linux TAR/DEB, zawartość źródeł i SHA-256. Szczegółowe końcowe wyniki/commit zapisuje `VERIFICATION.md` w katalogu wydania, a wybrane dowody pozostają w `artifacts/reports/release-0.40.0-2026-10-17/`.

Locked restore i kompilacja Release Windows/Linux oraz obu harnessów: bez ostrzeżeń i błędów. Testy jednostkowe: 776/776 na każdym systemie, bez pominiętych. Audyt NuGet czterech projektów nie zwrócił podatności ani błędów źródeł; nie jest to pełny audyt bezpieczeństwa. Kontrola treści, linków i wzorców poświadczeń obejmuje 649 plików. Zależności i locki pozostają bez zmian.

Pełna bieżąca macierz UI: 28/28 scenariuszy Windows i 29/29 Linux. Obejmuje listę CI wraz z network-tools/app-polish i dodatkowym linux-desktop. Nowe opcje personalizacji, kontrola kopii i anulowanie diagnostyki przeszły PL/EN; sprawdzono reprezentatywne zrzuty ustawień w małym i dużym oknie. Próby DPI w tej macierzy są symulowane.

Nie deklarujemy nowej akceptacji logowania do rzeczywistego serwera RDP, fizycznego Serial, Kali/KDE/Wayland, mieszanych monitorów DPI ani wielogodzinnego soak. Próby korzystają z własnych katalogów, baz i fixture loopback, bez osobistych połączeń i poświadczeń. Brak nowych zależności i migracji bazy; schema3. Nadal Public Beta, bez podpisu Authenticode. Pozostałe prace: [plan 1.0](../stable-release-plan.md), [znane ograniczenia](../known-issues.md).
