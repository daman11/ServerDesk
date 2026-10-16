# ServerDesk 0.23.0 — Public Beta

Pierwsze publiczne wydanie ServerDesk, bezpłatne i open source na licencji **MIT**. Aplikacja łączy SSH, SFTP, monitoring, RDP, VNC i Serial z podziałem obszaru pracy, odpinaniem kart oraz personalizacją. Interfejs jest dostępny po polsku i angielsku; zmiana języka wymaga restartu.

## Pliki do pobrania

- `ServerDesk-0.23.0-win-x64-Setup.exe` — zalecany instalator Windows dla bieżącego użytkownika.
- `ServerDesk-0.23.0-win-x64.zip` — pełna paczka Windows do rozpakowania.
- `serverdesk_0.23.0_amd64.deb` — pakiet Linux Ubuntu/Debian.
- `ServerDesk-0.23.0-linux-x64.tar.gz` — archiwum Linux.
- `ServerDesk-0.23.0-source.zip` — zamrożony kod źródłowy z testami, skryptami i licencją MIT.
- `SHA256SUMS.txt`, `release.json`, `VERIFICATION.md` — sumy, metadane oraz zakres faktycznej weryfikacji.

Instalator tworzy skrót w menu Start i wpis na liście aplikacji. Odmawia aktualizacji, gdy pliki programu są używane; nie zamyka aktywnych sesji na siłę. Deinstalacja usuwa tylko pliki programu i zachowuje dane użytkownika. Pełne paczki zawierają .NET oraz teksty licencji zależności, w tym czcionki Inter i komponentów natywnych.

## Aktualizacja

Zakończ sesje, zamknij ServerDesk i wykonaj kopię katalogów danych. Instalator Windows uruchom dla istniejącego katalogu, DEB aktualizuj przez apt. ZIP/TAR rozpakuj do nowego katalogu. Eksport JSON nie zawiera haseł ani prywatnych kluczy. Nie otwieraj bazy po migracji starszą wersją programu.

Instrukcja PL: `docs/getting-started.md`; English: `docs/getting-started.en.md` wewnątrz paczek. Dane i prywatność: `docs/privacy.md`.

## Ograniczenia beta

Windows 11 x64 jest głównym środowiskiem testów. Linux sprawdzano na Ubuntu/WSLg; pełna macierz fizycznych dystrybucji, urządzeń Serial i monitorów DPI nadal wymaga testów. Linux RDP korzysta z osobno instalowanego FreeRDP. VNC nie ma szyfrowania TLS ani weryfikacji tożsamości serwera — używaj VPN lub bezpiecznego tunelu. Szczegóły: `docs/known-issues.md` w paczkach.

Paczki nie mają podpisu zaufanego wydawcy; Windows może wyświetlić SmartScreen. Pobieraj je wyłącznie z zaufanego źródła i porównaj sumy. Nie ma automatycznych aktualizacji, konta chmurowego ani telemetrii. Kolejne wersje będą publikowane jako oddzielne, kompletne paczki. Nie jest to deklaracja gotowości 1.0 ani certyfikacja bezpieczeństwa.

## English

First public **MIT-licensed beta**, with a per-user Windows installer, portable Windows ZIP, Linux DEB/TAR.GZ and a source snapshot. Includes dependency notices, checksums and a verification report. Quit and back up data before updating; uninstall preserves user data. No automatic updater or telemetry. Unsigned binaries; use a trusted source. Linux desktop, hardware and mixed-DPI coverage is limited. VNC is unencrypted: use a VPN or secure tunnel. Read `docs/known-issues.md` and the English getting-started guide included in the packages.
