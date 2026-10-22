# ServerDesk 0.46.0 verification

Date: 2026-10-22. Public Beta, MIT. Scope: Bitwarden/Vaultwarden plugin and pending SSH focus/Commands insertion fixes. No new dependencies or schema migration.

## Verification scope

- Locked restore and solution Release build completed successfully.
- Final Windows and Linux/WSL unit/loopback runs: 1056 passed on each OS, 0 failed, 0 skipped, including 21 Bitwarden cases. Explicit foreign-platform CLI paths survive a settings round trip; execution still requires a native absolute executable path. A missing cloud server URL is rejected before unlock rather than guessed.
- Dependency audit: all five solution projects, no reported advisories/diagnostics. All 23 audit-gate regression fixtures passed.
- Synthetic Bitwarden CLI/native child checks passed on Windows and Linux: environment-only secrets, stripped inherited debug/preload/session options, metadata-only selection, exact item retrieval, wrong-account rejection, bounded output/stderr, child cancellation, PL/EN dialogs/settings and disable/drain/re-enable without terminal/data changes. Final targeted checks also cover one-time credential filling, editor removal, Windows external-RDP guidance and safe unexpected-error stacks without password/session/master-password canaries in diagnostics.
- Full headless UI matrix: 45 scenarios passed on Windows and 45 on Linux/WSL, including existing protocols, recovery, plugins, credentials, settings, localization, help, presentation and terminal focus. Final targeted plugin/help/localization/presentation regressions cover the last scoped changes.
- Staged native Windows checks passed: published off-screen/maximized startup, real restart/language handoff and RDP HWND/OLE-host create/detach/repaint/disposal without a server or credentials. Published Linux process startup passed with isolated data.
- Staged installer checks passed: compiled Polish welcome text, upgrade from 0.45.1, busy refusal, repair of missing/corrupt payload/uninstaller, complete byte verification, desktop-shortcut choices and preservation of data/unknown files. Registration was explicitly skipped to preserve the maintainer desktop.
- Staged TAR/DEB checks passed: all 550 payload files, executable permissions, package version/architecture, root ownership, launcher and desktop entry. Final artifact hashes and post-commit packaging checks are recorded in the local release's `VERIFICATION.md` before its immutable directory is assembled.

## Limits

The Bitwarden tests use disposable data and a synthetic CLI executable; they do not sign in to a personal vault or prove current Bitwarden cloud/Vaultwarden server compatibility. Install the official native `bw` executable, configure/log in from CLI and complete live acceptance on a separate test account. Login passwords only: no TOTP, attachments, SSH Key retrieval, vault editing or automatic sync.

Decrypted CLI output exists transiently in memory. The plugin clears owned byte buffers, avoids secret-bearing process arguments and omits raw CLI stderr/stdout from diagnostics. Managed strings and external CLI memory are not guaranteed to be zeroed. Closing forgets ServerDesk's session; it does not globally invalidate a shared CLI profile with `bw lock/logout`.

No VMware/hardware acceptance was repeated for this release. Existing physical RDP mouse/keyboard/IME/DPI, reverse transfer and monitor-off/network acceptance limits remain. Linux validation uses WSL/headless and published process probes, not a new distribution/desktop acceptance run. Installers remain unsigned. Preparing a local release does not publish it to GitHub.

See [plugin setup](../bitwarden.md), [release notes](../releases/release-0.46.0.md) and [known issues](../known-issues.md).
