# ServerDesk 0.22.1 — poprawki bezpieczeństwa

Przegląd i opis skutków: [security-review-0.22.1.md](../reports/security-review-0.22.1.md).

- VNC: limity danych przed alokacją, kontrola formatu i rozmiaru pulpitu, obsługa EOF w Raw i zamykanie połączenia z bezczynnym serwerem. Obsługiwane są RFB 3.3/3.7/3.8; jawna metoda uwierzytelniania pozostaje wymagana.
- VNC nie negocjuje ZRLE/Tight; używa Raw/CopyRect/ZLib. Możliwe większe zużycie pasma. Transmisja klasycznego VNC nadal nie jest szyfrowana — potrzebny jest VPN lub istniejący tunel.
- Import: do 32 poziomów i 1024 znaków w ścieżce folderu oraz 10 000 folderów wraz z przodkami; niepoprawne/null dane odrzucane przed zapisem.
- Windows: systemowa ścieżka mstsc i ładowanie ATL z System32.
- SFTP: wspólna walidacja nazw docelowych, odrzucanie aliasów i nazw urządzeń Windows, kontrola lokalnego katalogu docelowego.
- Zmiana celu, konta lub metody logowania profilu usuwa stare zapisane poświadczenie i wymaga jego ponownego podania. Edycja wyglądu zachowuje hasło. Niedostępny magazyn blokuje zapis zmienionego celu.
- Terminal: jawna blokada zdalnych żądań odczytu/nadpisania schowka; ręczne kopiowanie/wklejanie pozostaje dostępne.

Weryfikacja: 319/319 xUnit, testy interfejsu poświadczeń i schowka, VNC na loopback oraz regresja UI. NuGet audit z zależnościami przechodnimi nie zgłosił znanych podatności. Ograniczenia i zakres nieprzeprowadzonych testów są w raporcie bezpieczeństwa.
