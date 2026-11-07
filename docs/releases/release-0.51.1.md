# ServerDesk 0.51.1 Public Beta

2026-11-07 · Windows x64 / Linux x64 · ServerDesk code MIT.

## Zmiany

- **RDP na Windows:** ponowne dopasowanie obrazu do obszaru karty po zmianie
  monitora, DPI, rozmiaru lub okna. Lokalne skalowanie pozostaje dostępne przy
  odrzuceniu dynamicznej rozdzielczości. Jawne wyłączenie skalowania jest zachowane.
- **Bitwarden / Vaultwarden:** zapamiętywane wyłączenie okna przy starcie oraz
  ikona klucza na głównym pasku do późniejszego odblokowania i zarządzania sejfem.
- **SSH:** niezabezpieczony lokalny klucz nie otwiera pytania o hasło. Wklejanie
  wieloliniowych komend zachowuje backslashe, zaznaczenie i tryb bracketed paste.
- **Odzyskiwanie:** czytelniejsze komunikaty, gdy systemowy magazyn nie udostępni
  treści własnych komend lub kolekcji. Odnośniki i pozostałe dane są zachowane.
- **Network Tools:** bezpieczne logi z identyfikatorem, właściwe komunikaty
  lokalnego zapisu i eksportu oraz możliwość kopiowania pełnego opisu błędu.
- **Monitoring Linuxa:** odrzucanie błędnych procentów dysków, adresów IP i masek
  bez utraty pozostałych parametrów; poprawna próbka przywraca wskaźniki.

## Installation / update

Windows: close ServerDesk and run `ServerDesk-0.51.1-win-x64-Setup.exe`.
The current-user installer preserves saved data and offers a desktop shortcut.
Existing installations can use the app's update check; installation remains
user-confirmed. Portable users should extract the complete ZIP into a new folder.

Linux: use `serverdesk_0.51.1_amd64.deb` or the complete TAR.GZ. X11/XWayland,
system libraries, Secret Service for remembered passwords and installed X11
FreeRDP for RDP remain required. Linux update installation is manual.

Previous packages/tags are frozen. This beta has no Authenticode signature.
Compare downloads with SHA256SUMS; checksums do not authenticate a publisher.
The separately bundled Bitwarden OSS client retains its GPL notices/source.

## Verification and limits

[Verification](../reports/release-verification-0.51.1.md) separates final release
gates from earlier focused evidence. The four-hour Windows/headless test passed
on its recorded earlier commit; it is not a fresh multi-hour test of 0.51.1.
RDP geometry checks use the real Windows control and available monitors, but
no logged-in bitmap. The active-session/mixed-DPI confirmation remains open.

This is Public Beta. Native KDE/KWallet, physical keyboard/IME and mixed DPI,
Serial hardware, wider WAN/server compatibility and clean-VM installer
registration retain the [remaining 1.0 work](../stable-release-plan.md).
Reverse RDP file export uses the clipboard/helper rather than one continuous
gesture. SQLite backups still exclude separately protected secrets/plugin content.
