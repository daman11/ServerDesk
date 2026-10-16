# ServerDesk 0.24.0 — weryfikacja MVP

Data: 2026-10-15. Public Beta, MIT. Kolejny etap po zamrożonej 0.23.0.

## Wykonane kontrole

- Kompilacja Release: 0 błędów, 0 ostrzeżeń. **357/357 xUnit**, bez pominięć; 22 nowe przypadki dotyczą planu usuwania, zakresu, limitów, dowiązań, zmiany metadanych, anulowania, błędu i nowych plików po podglądzie.
- Windows i Linux/WSLg: rzeczywiste dialogi Avalonia PL/EN; podgląd bez mutacji, pełne ścieżki, liczniki, anulowanie/błąd skanowania, jawne potwierdzenie, brak domyślnego przycisku usuwania, raport częściowego wykonania. Dark/Light, tekst 16, 480×440 z przewijaniem treści i widocznymi akcjami.
- Linux: fizyczne dowiązania do pliku, katalogu i brakującego celu w jednorazowym katalogu. Usunięto wpisy, zachowano cel; odrzucono dowiązanie w ścieżce nadrzędnej.
- Własny OpenSSH/SFTP z klienta Windows i Linux: drzewo z ukrytymi plikami; dowiązania do pliku/katalogu oraz zerwane. Potwierdzono zachowanie celów, zmianę nazwy samego linku, odmowę chmod na linku i bezpieczne usunięcie pojedynczego linku.
- Rzeczywista podmiana katalogu na dowiązanie po podglądzie zatrzymuje plan bez usunięcia plików obu zachowanych drzew.
- Rzeczywiste anulowanie po jednym potwierdzonym usunięciu oraz zamknięcie własnego połączenia SFTP. Licznik wynosi 1; drugi plik pozostaje. Bez automatycznej próby ponowienia.
- Integracja panelu: wybór wielu plików i skrót Delete; nazwa serwera w dialogu; zmiana aktywnego panelu i zdalnej ścieżki po podglądzie nie przekierowuje operacji.
- Regresja grup SFTP: Ctrl/Shift, Ctrl+A/Escape, utrzymanie wyboru, filtrowanie/sortowanie, początek D&D, 1000 wirtualizowanych wpisów, PL/EN i małe widoki.
- Regresja rzeczywistych transferów: 1 MiB round trip, checksum, ochrona przed nadpisaniem, anulowanie zadania w kolejce i podczas pytania o nadpisanie, ponowienie do pierwotnego celu, odzyskanie po błędzie i limit 50 zakończonych wpisów.
- Regresja bezpieczeństwa i stabilności UI: unieważnianie poświadczeń przy zmianie celu, blokada OSC52, odzyskanie nieczytelnego hasła PL/EN, VNC zatrzymane w połowie ramki oraz uszkodzony pakiet.
- Opublikowany EXE Windows: trzy osobne uruchomienia z izolowaną bazą, korekta okna poza ekranem, maksymalizacja i odłączony monitor; natywny host RDP z przenoszeniem między panelami/oknami i poprawnym zwolnieniem. Bez logowania do prywatnego RDP.
- Instalator 0.24.0: instalacja w ścieżce ze spacjami i polskimi znakami, start z własną bazą, odmowa aktualizacji/deinstalacji uruchomionego programu, zgodność wszystkich zainstalowanych plików, zachowanie danych i dodatkowych plików po usunięciu. Test domyślnej rejestracji/skrótu **pominięto**, aby zachować istniejącą instalację użytkownika; ten mechanizm był sprawdzony w 0.23.0.
- Linux: rzeczywiste nowe okno końcowego programu w Ubuntu/WSLg. TAR/DEB: wszystkie 324 pliki zgodne z payloadem, poprawna wersja/amd64, prawa wykonywania, właściciel root, launcher i desktop. DEB nie był instalowany do systemu gospodarza.
- Archiwa: licencje/notices, brak kluczy, baz, poświadczeń, logów i PDB. Zestaw źródeł oraz sumy kontrolne sprawdzono osobno przy składaniu wydania.
- Zamrożone pliki **0.23.0 pozostały bajtowo niezmienione** (sprawdzono pełny SHA256SUMS). Własny serwer testowy SSH zatrzymano po testach.

## Granice weryfikacji

Nie jest to pełny audyt bezpieczeństwa ani test wszystkich systemów. Nadal otwarte: Windows 10, samodzielne dystrybucje Linux/KWallet, fizyczne Serial, szeroka macierz RDP/schowka/DPI, gesty D&D na wszystkich platformach i wielogodzinne obciążenia. Nie dodano zależności runtime, a wersje pakietów są bez zmian; audyt NuGet bazowego 0.23.0 jest zachowany, nie przedstawiamy go jako nowego audytu.

Usuwanie jest trwałe, nietransakcyjne i bez cofania. Limit: 20 000 wpisów / 64 poziomy. Metadane nie zapewniają izolacji od równoległych zmian przez inne procesy. Po awarii sieci ostatnie żądanie może mieć nieznany wynik; licznik oznacza potwierdzone operacje. Pozostałe ograniczenia w docs/known-issues.md oraz docs/release-0.24.0.md.

Dowody lokalne: mvp-tests-final-0240.txt, mvp-deletion-final-0240.txt, mvp-deletion-network-final-0240.txt, mvp-delete-linux-final-0240.txt, mvp-ui-regression-0240.txt, mvp-sftp-regression-0240.txt, mvp-setup-test-0240.txt, mvp-package-linux-test-0240.txt, mvp-rdp-host-0240.json, mvp-startup-0240.txt, mvp-native-linux-0240.txt. Logi techniczne i dane fixture nie są publikowane w paczkach.

English: 357 tests pass. Windows/Linux UI and real OpenSSH tests cover bounded batch/recursive deletion, link preservation, changed ancestors, partial cancellation/disconnection and captured session/scope. Existing SFTP/security/stability regressions pass. Installer, native startup/RDP host and package-content checks pass; registration was deliberately skipped to preserve the existing user installation. No full OS/hardware/DPI or concurrent hostile filesystem-mutation guarantee. No private RDP credentials used. Version 0.23.0 remains unchanged.
