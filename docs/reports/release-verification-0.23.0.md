# ServerDesk 0.23.0 Public Beta — weryfikacja wydania

Data: 2026-10-15. Licencja MIT. Windows x64 / Linux x64. Paczki bez podpisu zaufanego wydawcy.

| Kontrola | Wynik |
|---|---|
| Kompilacja rozwiązania Release | 0 błędów, 0 ostrzeżeń |
| xUnit | 335/335, bez pominięć |
| Czyste źródła z archiwum | Locked restore, kompilacja i ponownie 335/335 testów |
| Blokada zależności | Restore obejmuje win-x64 i linux-x64; tryb locked przechodzi |
| NuGet direct + transitive | 4 projekty; brak zgłoszonych znanych podatności, brak błędów źródeł audytu |
| UI Windows PL/EN | Ustawienia, zapis/anulowanie/restart, edytory, historia, polecenia, panele protokołów |
| Bezpieczeństwo/stabilność UI | Poświadczenia, blokada OSC52, zatrzymana ramka i uszkodzony pakiet VNC |
| VNC/Serial | Rzeczywisty VNC loopback: obraz, klawiatura, mysz, view-only, dokowanie, utrata sieci; walidacja Serial i błąd brakującego urządzenia |
| Opublikowany EXE Windows | 3 izolowane uruchomienia: korekta okna poza ekranem, maksymalizacja, odłączony monitor, czyste zamknięcie |
| Natywny host RDP w EXE | Utworzenie kontrolki, split, osobne okno, ponowne dokowanie, resize, zwolnienie HWND; bez zdalnego logowania |
| Setup Windows | Instalacja w ścieżce ze spacjami i polskimi znakami; rzeczywisty start z własną bazą |
| Setup — aktywny proces | Aktualizacja i deinstalacja odmawiają pracy; proces i pliki pozostają nienaruszone |
| Setup — aktualizacja/usuwanie | Wszystkie pliki zgodne z payloadem; zachowana baza, ustawienia i dodatkowe pliki użytkownika |
| Integracja Windows | Wpis aplikacji HKCU i skrót menu Start utworzone/usunięte prawidłowo; test nie zastępuje istniejącej instalacji użytkownika |
| Linux TAR/DEB | 323 pliki payloadu zgodne bajtowo; wersja, amd64, prawa wykonywania, właściciel root, launcher i desktop |
| Natywny Linux | Końcowy EXE tworzy nowe rzeczywiste okno w Ubuntu/WSLg z jednorazową bazą |
| Licencje i czystość paczek | Pełne odwołania notices, Inter OFL, Skia/HarfBuzz/ANGLE, runtime .NET; brak kluczy, baz, poświadczeń, logów testowych i symboli PDB |
| Źródła / sumy | Kod, zasoby, testy i skrypty z allowlist; manifest plików źródłowych, release.json i SHA256SUMS |

Testy używały osobnych katalogów, bazy i serwerów loopback. Nie logowano się do prywatnego serwera RDP ani nie używano rzeczywistych haseł użytkownika. Linux sprawdzono pod WSLg, bez instalacji DEB do systemu gospodarza: zweryfikowano jego zawartość i sam program. Nie wykonano fizycznego testu apt na osobnym Ubuntu.

To publiczna beta, a nie certyfikacja bezpieczeństwa lub zakończona macierz zgodności. Nadal nieweryfikowane szeroko: Windows 10, samodzielne dystrybucje Linux, KWallet, urządzenia Serial, mieszane DPI, konta domenowe/Microsoft RDP, wszystkie formaty schowka, gesty fizycznego D&D i wielogodzinne obciążenia. VNC pozostaje bez szyfrowania TLS i wymaga VPN/tunelu. Szczegóły: docs/known-issues.md.

Źródłowy zestaw dowodów lokalnych: release-build-0230.txt, release-lock-0230.txt, release-nuget-audit-final-0230.json, release-ui-tests-0230.txt, release-rdp-host-0230.json, release-installer-test-0230.txt, release-linux-content-0230.txt, release-native-linux-0230.txt i source-build-0230/. Logi techniczne nie są dołączane do publicznych paczek, aby nie przenosić lokalnych ścieżek i danych testowych.

English: Release build and the clean-source test run pass (335/335). Windows installer lifecycle, actual application startup, native RDP host, UI/protocol fixtures and Linux package content/native WSLg startup pass. Unsigned public beta; no full physical OS/hardware/DPI matrix or penetration-test certification. No private RDP credentials were used. Checksums detect corruption, not publisher identity. See the known-issues and getting-started documents included in each package.
