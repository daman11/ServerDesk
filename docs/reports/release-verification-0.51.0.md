# ServerDesk 0.51.0 — local release verification

Date: 2026-10-25. Public Beta. Scope: four browser/SFTP work items and the
previous vault/presence performance fixes. No database schema, dependency lock
or upstream component change. Previous release packages/tags remain unchanged.

## Four completed work items

1. Saved-tab reveal clears search/protocol/tag filters, opens a hidden sidebar,
   expands ancestors and selects the saved UUID. Quick/deleted profiles stay
   disabled and are not recreated. PL/EN checks use a credential provider that
   throws on unexpected access.
2. Expand/collapse all folders handles 32 levels, moves hidden child selection
   to the visible root, persists normal state across restart and leaves search
   expansion temporary. Context menu, icons and action-palette entry points are
   included, with in-app Help and current guides in both languages.
3. Downloads use short sibling temporary filenames. Actual OpenSSH transfer
   checks on Windows/Linux cover 248-character and Unicode destination names,
   complete bytes and cleanup without appending to the long destination name.
4. Upload size/write-time and download size/write-time/type checks run before
   publishing. Disposable OpenSSH tests change a local timestamp during upload
   and mutate a same-size remote source during download. Both retain sentinel
   destinations, clean partials and complete explicit retries with matching
   SHA-256. This is metadata validation, not a filesystem snapshot or a
   cryptographic guarantee against every concurrent change.

## Regression and release gates

- Locked restore and Release build; **1135 unit/bounded loopback tests passed
  on each OS**, with no failures/skips. The five-project dependency audit found
  no reported advisories/diagnostics; 23 audit-gate decision fixtures passed.
- The applicable Windows/Linux headless matrix covers core settings, plugins,
  terminal/SFTP UX, layout, data protection and the newer presence/navigation
  checks. Native synthetic Bitwarden tests retain pinned-binary, TLS challenge,
  cancellation and profile-isolation coverage; the fixture deliberately refuses
  final login, so it is not a personal-vault acceptance result.
- Additional Windows OpenSSH resilience/interruption checks cover silent loss,
  bounded cancellation, preservation of overwritten targets, retry SHA-256,
  exact partial warnings, scoped cleanup and a 5000-entry listing.
- Published Windows checks cover off-screen correction, maximized restart,
  missing-monitor fallback, reset-before-workspace, language restart and the
  native RDP host. The host check does not log into a remote Windows computer.
- Setup is exercised in private directories with registration skipped: older
  install/start, update, byte/data preservation, busy refusal, repair of missing
  and corrupt files/uninstaller, shortcuts and scoped uninstall. Actual compiled
  welcome text is checked for separators and Polish diacritics.
- Final packages require version/commit, notices, binary/source pins, complete
  file inventories, source export, checksums and clean annotated-tag checks.
  The frozen **VERIFICATION.md** supplies final per-platform counts, exact
  commit and payload/installer results. This source description alone does not
  certify an untested future payload or replace those final gates.

## Boundaries

Same-size/same-timestamp source mutations, coarse SFTP clocks and filesystem
races can escape metadata checks. No transfer resume, automatic retry, remote
agent, new native Wayland framebuffer or direct reverse RDP drag is added.

The tests use explicit synthetic/disposable data. No saved production profile,
personal vault, VMware guest or real remote RDP account is used. WSL/headless and
WSLg startup do not certify native KDE/KWallet, physical input/mixed DPI, Serial
hardware, clean-VM installer registration, all WAN/server variants or multi-hour
use. Installer registration remains **SKIP** on the maintainer desktop.

ServerDesk source remains MIT; the separate pinned Bitwarden OSS component
retains its GPL notices/source. Its upstream source archive contains previously
reviewed public test/development vectors; it is byte-pinned and unchanged.
Packages are unsigned. Preparing this local release does not publish it on GitHub.

[Release notes](../releases/release-0.51.0.md), [remaining 1.0 work](../stable-release-plan.md),
[vault performance](vault-read-performance-2026-10-23.md),
[browser presence](browser-presence-2026-10-25.md).
