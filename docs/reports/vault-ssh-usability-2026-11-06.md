# Vault and SSH usability — 2026-11-06

Development changes after 0.51.0. The released tag/packages remain frozen.
No database schema, dependency lock, vault credential lifetime or upstream
component changes. A local Windows development build is used for review.

## Changes

- The startup/account vault dialog offers a remembered startup opt-out. Save it
  when the dialog closes, including Skip and successful startup unlock. Changing
  only this preference retains the owned unlock. The main toolbar key opens the
  existing account/unlock dialog and is hidden when the plugin is disabled.
- Local SSH keys are parsed off the UI thread before accessing credentials. An
  unencrypted key returns an empty passphrase without opening a dialog or reading
  the system/vault password. Encrypted keys keep the masked credential flow.
  Explicit encrypted PKCS#8 envelopes are recognized before SSH.NET attempts
  empty-password decryption; missing/malformed keys retain local diagnostics.
- Ordinary SSH clipboard text normalizes CRLF/LF/CR while preserving backslashes,
  spaces, quotes and UTF-8 content. The remote bracketed-paste setting determines
  framing. Ctrl+Shift+V, Shift+Insert, the session menu and right-click paste use
  one path. Remote mouse tracking is respected. No Enter is appended; a newline
  already present in the copied text remains part of that text.

The emulator retains ownership of replacing existing selections and the separate
announced MIME-paste protocol. Those delegated paths are not claimed as verified
newline normalization. Serial and RDP clipboard behavior is unchanged.

## Focused evidence

- 37 selected unit tests passed: local key passphrase detection, SSH error
  messages and localization. Coverage includes RSA PEM, unencrypted PKCS#8 and
  Ed25519/OpenSSH, encrypted PKCS#8, missing/malformed keys and cancellation.
- PL/EN key-credential UI checks pass: ordinary and forced unencrypted-key
  requests access no credential store and open no prompt; encrypted keys request
  a masked passphrase, while malformed keys do not open a password dialog.
- PL/EN workspace vault checks pass: saved opt-out survives a fresh window,
  toolbar unlock works afterwards, re-enabling startup persists, plugin disable
  hides the shortcut and changing only startup behavior retains the unlock.
  Existing session/account/cancellation/serialization guards remain covered.
- A synthetic PTY records 2400 continuation lines with CRLF, LF and CR input,
  Unicode and literal backslashes, both bracketed and legacy framing, actual
  keyboard shortcuts and right click. Remote TUI mouse tracking is respected.
  The clipboard is not rewritten and a disconnected terminal receives no data.
- Focused terminal-focus and embedded-Help regression checks passed, including
  tab/split/SFTP focus isolation and the updated PL/EN instructions.
- Actual disposable OpenSSH/Bash checks pass for both bracketed and legacy paste:
  1200 UTF-8 continuation lines copied as CRLF produce the exact expected
  SHA-256. With no final newline, execution occurs only after explicit Enter.
  The disposable encrypted OpenSSH fixture key is correctly identified.

Initial test runs identified and corrected encrypted-PKCS#8 error classification
and an acceptance harness host-key callback overwritten by window setup. A
sandboxed testhost connection was unavailable; the same selected unit set passed
outside that restriction. No production connection, personal vault, saved
credential or VMware guest is used. The loopback SSH fixture is stopped after
the tests. Evidence remains in artifacts/reports/ssh-vault-2026-11-06.

No full platform/VM matrix or release packaging is claimed for this development
batch. Physical clipboard/IME and arbitrary remote shell configurations retain
their previous acceptance limits.
