# ServerDesk 0.47.0 verification

Date: 2026-10-22. Public Beta, MIT. Scope: native Bitwarden/Vaultwarden SSH Key items and the pending picker ergonomics changes. No new dependencies or database schema migration.

## Verification scope

- Locked restore and solution Release builds succeeded on Windows and Linux/WSL, with no warnings or errors. Unit/loopback suites: **1069 passed on each OS, 0 failed, 0 skipped**. Thirteen new key cases cover RSA/PKCS#8/Ed25519 parsing, malformed/encrypted/oversized payloads, metadata mismatch, owned-buffer cleanup, exact account/item/type identity, cancellation, persistence/export compatibility and refusal to fall back to a password or local key path.
- Dependency audit covered all five solution projects without reported advisories or diagnostics; all 23 audit-gate regression fixtures passed. Repository content/link/notice checks passed. No packages or lockfiles changed.
- Secret scanning includes Git history. The private-key rule is scoped to exclude only the exact dynamic PEM wrapper expression in the randomly generated test-key fixture; it does not exempt key literals or the rest of the test file. Final scan results are recorded in the local release verification.
- Full headless UI matrix: **47 Windows and 48 Linux scenarios passed**, including all current CI scenarios and Help, Network Tools and App Polish. Final focused PL/EN Bitwarden, Help, localization and presentation checks passed after the last text changes. Key-only search/binding does not read the private key; actual retrieval returns an owned key rather than a password. Abandoned picker results and cancelled/failed/closed sessions clear owned key buffers. No system credential reads/writes occur in vault-key connection acceptance.
- Real disposable OpenSSH acceptance passed on Windows and Linux for **RSA and Ed25519**: picker/unlock to SSH terminal, SFTP browser, queued upload, download/readback, monitoring, plugin disable without disrupting core SSH, remote EOF cleanup and a new unlock/fetch on reconnect. An unauthorized generated key was rejected. Scans of the isolated application data found no private-key canary. Vault CLI responses are synthetic; server keys/users/files are disposable. The loopback-only fixture was stopped after testing.
- DataProbe passed all eight historical-data/process recovery cases on each OS. Staged native Windows startup, off-screen/maximized placement, language restart, settings reset and embedded RDP HWND/OLE-host create/detach/repaint/disposal passed without a remote server. The staged self-contained Linux application started with isolated data in WSLg; this is a process-start check, not a desktop/input acceptance run.
- Staged Setup checks passed compiled Polish welcome text, upgrade from 0.46.0, busy refusal, complete byte verification, repair of missing/corrupt payload/uninstaller, desktop-shortcut choices and preservation of data/unknown files. Registration was skipped to preserve the maintainer desktop. Linux TAR/DEB checks passed all 552 payload files, executable permissions, package version/architecture, root ownership, launcher and desktop entry.
- The final clean-commit payload/installer checks, source export and SHA-256 set are recorded in the local release's `VERIFICATION.md` before its immutable directory is assembled.

## Limits

The vault tests use synthetic CLI responses; they do not sign in to a personal Bitwarden/Vaultwarden account or prove every server/CLI version. Live acceptance remains open on a separate test account. Supported key types are RSA 2048–8192 and Ed25519; additionally encrypted key payloads must first be imported in Bitwarden. No TOTP, attachments, SSH certificates, vault editing or automatic sync. New vault-key profiles cannot be used in older application versions; preserve a compatible database backup before downgrade.

Owned key/output buffers are zeroed, but immutable .NET strings, crypto objects and CLI memory can retain transient copies. Closing forgets ServerDesk's vault session without globally invalidating a shared CLI profile. Disabling the optional plugin preserves the key belonging to an active core SSH session until that session ends.

No VMware/hardware acceptance was repeated. Existing physical RDP mouse/keyboard/IME/DPI, reverse transfer and monitor-off/network acceptance limits remain. Linux validation uses WSL/headless and process probes, not a clean distribution/desktop acceptance. Installers remain unsigned. Preparing a local release does not publish it remotely.

See [setup](../bitwarden.md), [release notes](../releases/release-0.47.0.md) and [known issues](../known-issues.md).
