# ServerDesk 0.52.0 Public Beta

2026-11-14 · Windows x64 / Linux x64 · ServerDesk code MIT.

## Zmiany

- **Zbiorcze tagi:** kliknij prawym przyciskiem połączenie lub tło listy →
  Edytuj tagi wielu połączeń. Wybierz serwery, dodaj/usuń tagi i sprawdź
  podgląd. Zapis obejmuje całą partię; błąd lub nieaktualne tagi wycofują zmiany.
  Kolory, pozostałe pola, poświadczenia i sesje zostają zachowane.
- **Lista połączeń:** Kopiuj adres i Ctrl+C kopiują sam host albo port Serial.
- **SFTP:** wyczyszczenie filtra oddaje mu fokus; Enter/↓ przechodzą do listy
  bez otwierania pliku. Pomoc prawidłowo kieruje fokus do widocznego spisu tematów.
- **Logi:** F3/Shift+F3 i Enter/Shift+Enter przechodzą po wynikach z zawijaniem,
  Escape w polu czyści wyszukiwanie, a Zawijaj wiersze ułatwia czytanie długich
  błędów. Eksport zachowuje oryginalny pełny plik.
- **Linux DEB:** poprawione zależności ICU, w tym 77 i 78. Regresja korzysta
  z izolowanego resolvera APT; brak ICU nadal blokuje instalację.

## Installation / update

Windows: close ServerDesk and run `ServerDesk-0.52.0-win-x64-Setup.exe`.
The current-user installer preserves saved data and offers a desktop shortcut.
The app's update check can offer this release; installing remains user-confirmed.
Portable users should extract the complete ZIP into a new folder.

Linux: use `sudo apt install ./serverdesk_0.52.0_amd64.deb` or the complete TAR.GZ.
This DEB supersedes 0.51.1 and the local packaging-only repair 0.51.1-1. System
libraries, X11/XWayland, Secret Service for remembered secrets and an installed
X11 FreeRDP client remain required. Linux update installation is manual.

Previous files/tags remain frozen. This is unsigned Public Beta; compare
downloads with SHA256SUMS. Checksums do not authenticate a publisher. ServerDesk
is MIT; the separate Bitwarden OSS client retains its GPL notices and source.

## Verification and limits

[Verification](../reports/release-verification-0.52.0.md) distinguishes final
release checks from earlier focused evidence. The original four-hour soak belongs
to its recorded earlier commit; it is not a new multi-hour 0.52.0 result.

Linux DEB metadata checks do not certify a Kali desktop or ICU-78 runtime loading.
Native KDE/KWallet, physical mixed DPI/input/IME, live RDP bitmap/resize, Serial
hardware, wider WAN/server cases, clean-VM installer registration and pilot use
retain the [remaining 1.0 work](../stable-release-plan.md). Reverse RDP file export
uses its clipboard/helper workflow. SQLite backups exclude separately protected
secrets and plugin contents.
