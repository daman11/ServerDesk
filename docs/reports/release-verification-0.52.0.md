# ServerDesk 0.52.0 — release verification

2026-11-14 · Public Beta · Windows x64 primary, Linux x64 with documented limits.

This batch adds reviewed atomic batch tag editing, copy-address actions,
SFTP/Help keyboard fixes, log wrapping/search and corrected DEB ICU alternatives.
No schema, dependency lock, pinned upstream component or protocol/session
lifecycle changes are intended. Older releases remain frozen.

## Completed pre-freeze checks

- Locked restores and Release builds: Windows and Ubuntu 24.04 WSL, no build
  warnings/errors. Unit tests: **1208/1208 on each OS**, no skipped tests.
- Headless UI: **60 distinct scenarios per OS**, with the corrected scenarios
  rechecked as described below. The pinned native Bitwarden OSS bridge also
  passed on both OS using a disposable fake TLS server, without a personal vault.
- Dependency audit: five projects, zero known advisories or audit diagnostics.
  Audit-gate fixtures: **23/23**. Redacted Git-history secret scan: no findings.
- Data recovery: eight historical-schema/interrupted-operation cases per OS;
  real ENOSPC preservation/retry passed in a disposable 8 MiB Linux tmpfs.
- Real loopback SSH/Bash paste checks passed on Windows and Linux: bracketed
  and legacy modes, 1200 UTF-8 continuation lines, selection replacement,
  exact content hash and no execution before Enter. The owned sshd was stopped.
- Native Windows ActiveX viewport/host geometry, OLE and lifecycle checks passed
  without logging into a remote computer. These are not live RDP acceptance.
- Preliminary self-contained startup, settings reset, language restart, native
  RDP host, installer text, upgrade from 0.51.1, repair and data/unknown-file
  preservation passed. Installer registration was explicitly skipped.
- Linux TAR/DEB: **589 matching payload files**, launcher, permissions, ownership
  and upstream notices checked; **12 isolated APT dependency simulations** passed.

Evidence is retained locally in `artifacts/reports/release-0.52.0/`. The immutable
release's `VERIFICATION.md` records the final source commit, repeated final-binary
gates, package/source/version/hash checks and publication limits. Preliminary
package checks alone do not freeze or approve the final binaries.

## Findings and rechecks

The first Windows unit run timed out in one malformed-frame VNC case. Its exact
cause was not established. A separate CI timing problem in the incomplete-frame
test was corrected with dedicated workers and an explicit writer-held assertion.
The complete 1208-test Windows suite then passed, as did 33 focused VNC cases and
15 additional repeated cases. Initial failure evidence is retained, not counted
as a passing run; production VNC protocol code was not changed.

The initial UI matrix found missing shared icon metadata for batch tags and
Copy address on both OS. Both registrations were added; presentation consistency,
batch tags and browser workflow passed their focused rechecks on both OS.

Windows also exposed timing assumptions in two headless fixtures: rendering
could consume the subnet debounce before synthetic Escape, and a fixed frame
count could finish before asynchronous window shutdown. The fixtures now route
Escape from the focused control without an intervening render and wait boundedly
for close completion. Both affected scenarios were rechecked on both OS; no
production cancellation or shutdown behavior was changed. The initial failures
and successful rechecks are retained separately.

Linux CI APT setup now retries transient fetch failures three times. This does
not suppress errors or replace dependency resolution checks.

The first pushed preparation commit's Windows CI also timed out in the native
PowerShell quoting fixture (1207 passed, one failed). The 10-second process budget
was increased to 30 seconds, with bounded termination/drain; literal-output,
stderr and exit-code assertions are unchanged. This tests quoting safety rather
than cold shell startup latency. The exact runner delay was not established.
Both complete local unit suites were repeated after this test-only change.

## Focused evidence

- [Batch tags](batch-tags-2026-11-08.md): 31 selected cases plus PL/EN, rollback,
  stale targets, latest-field Save, retained selections and no credential/session access.
- [ICU packaging](linux-icu-packaging-2026-11-14.md): original failure reproduced,
  isolated APT matrix and unchanged old payload in the local 0.51.1-1 repair.
- Logs/SFTP/Help/browser fixes have targeted PL/EN checks in the current harness;
  the final matrix reruns them for this release.
- [Earlier four-hour soak](session-soak-2026-11-07.md) remains evidence for that
  recorded commit only. It is not repeated or promoted to native desktop evidence.

## Boundaries

Disposable fixtures only; no personal vault, production profile or VMware guest.
Installer registration is skipped on the maintainer desktop. Native Linux GUI,
KWallet, physical mixed DPI/input/IME/drag, live RDP/remote bitmap, Serial hardware,
broader WAN/server compatibility and pilot acceptance remain open. DEB resolver
simulation does not run ICU-78 binaries. Reverse RDP export remains a helper flow;
portable protected-secret backup is not introduced. Packages remain unsigned beta.

The final source commit is rebuilt/repackaged and its published-payload/installer/
content gates repeated before freezing. Routine metadata commits need no repeated
multi-hour soak. See [release procedure](../releasing.md) and [1.0 plan](../stable-release-plan.md).
