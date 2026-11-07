# ServerDesk 0.51.1 — release verification

2026-11-07 · Public Beta · Windows x64 primary, Linux x64 with documented limits.

This batch collects Windows RDP viewport fitting, vault startup controls, local
SSH key/passphrase and clipboard fixes, protected-content recovery guidance,
Network Tools diagnostics and Linux monitoring validation. No schema, dependency
lock or pinned upstream component change. Older releases remain frozen.

## Completed source checks

- Locked restore and Release build: Windows and WSL Ubuntu 24.04.4, .NET
  10.0.12 / SDK 10.0.401. No changed locks or build warnings.
- Unit/bounded loopback: **1197/1197 Windows and 1197/1197 Linux**, no skips.
- UI: **59/59 Windows and 59/59 Linux scenarios**, including the three newly registered CI
  cases for recovery, Network Tools diagnostics and monitoring validation.
- Dependency audit: all five projects covered, no reported advisories or
  diagnostic/query failures; **23/23 audit-gate fixtures**.
- Historical recovery/terminated-write checks: **8/8 on each OS**; Linux also
  passed real ENOSPC preservation/retry in an isolated **8 MiB tmpfs**.
- Actual disposable OpenSSH/Bash clipboard checks on both clients: bracketed
  and legacy mode, 1200 Unicode continuation lines each, selection replacement,
  exact remote SHA-256 and no execution before Enter. Synthetic keyboard input.
- Native Windows RDP geometry/refresh checks: retained control, real OLE object
  rectangles and offline failure recovery; no remote login.
- Preliminary published EXE: startup/window placement, reset, language restart
  and native RDP host lifecycle passed. Compiled installer text and upgrade
  from 0.51.0, busy refusal, repair and data/unknown-file preservation passed.

The first Windows hardening case failed because its assertion predated the
new error ID. The updated assertion requires the exact localized message plus
a valid GUID; its PL/EN rerun passed. The application behavior was unchanged.
The original failure remains in local evidence rather than being hidden.
The Linux sequential coordinator was stopped after its active child finished;
the resumed parallel matrix retained only recorded exit-zero cases and reran
unrecorded work. All 59 final cases completed; a stopped coordinator itself is
not counted as a passing run. Bundled Bitwarden native checks also passed on
both platforms using only synthetic loopback authentication.

## Final binary gates

Locked restore, Release build, complete applicable unit/headless checks on
Windows and Linux, dependency audit and audit-gate fixtures, repository and secret
checks, historical data recovery, published native startup/RDP-host/restart checks,
installer text/upgrade/repair/preservation checks, Linux archive checks and final
payload/source/version/hash validation are required before publication.

The immutable release's VERIFICATION.md records the final gate counts,
source commit, repeated checks of binaries built from that clean commit, results
and installer registration scope. This source document is not a claim that a
pending gate passed or that an earlier payload certifies the final binaries.

## Focused development evidence

- [Vault/SSH usability](vault-ssh-usability-2026-11-06.md): local-key prompts,
  selection-preserving multiline paste, startup opt-out and toolbar unlock.
- [Protected-content recovery](restore-consistency-2026-11-07.md): 33 targeted
  tests and PL/EN UI; SQLite references remain separate from protected content.
- [Network Tools diagnostics](network-diagnostics-2026-11-07.md): 15 targeted
  tests plus PL/EN failure/retry/privacy checks.
- [Monitoring validation](monitoring-validation-2026-11-07.md): 61 selected tests
  and PL/EN dark/light presentation using injected malformed/recovered samples.
- [RDP viewport](rdp-viewport-2026-11-07.md): 12 model tests, real ActiveX geometry,
  actual same-DPI monitor move, synthetic DPI, retained HWND and offline rejection.
- [Four-hour soak](session-soak-2026-11-07.md): PASS for its recorded earlier
  commit, eight SSH sessions/100 cycles and post-cleanup resource gates.

## Boundaries

A four-hour run is not repeated for this patch. The recorded soak does not
certify the final commit or a native desktop. RDP viewport tests do not log in
or validate a remote bitmap, physical input or live resize negotiation.

Fixtures use disposable data only. No private connection, personal vault or
VMware guest is used. Native KDE/KWallet, physical mixed DPI/input, Serial,
broader WAN/server cases and clean-VM installer registration remain open.
Registration is skipped on the maintainer desktop. No direct reverse RDP gesture
or portable protected-secret backup is introduced.

Source is MIT; the separate Bitwarden OSS component retains its unchanged GPL
notices/source. Packages remain unsigned. Full [1.0 readiness](../stable-release-plan.md)
is not claimed by this public beta.
