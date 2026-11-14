# Linux ICU packaging repair — 2026-11-14

The published 0.51.1 DEB required ICU 76, 74, 72 or 70. A newer available ICU
did not satisfy any alternative. This was a packaging metadata failure, not
evidence of an SSH/RDP or application startup failure.

The canonical packager now permits ICU 78/77/76/75/74/73/72/70. ICU remains
mandatory and globalization remains enabled. Recommends for Secret Service,
ping and FreeRDP are unchanged. [Background and usage](../linux-package-dependencies.md).

## Local repair artifact

`artifacts/fixes/linux-icu-0.51.1-1/serverdesk_0.51.1-1_amd64.deb`
was rebuilt from the exact published 0.51.1 payload, not current development
binaries. Debian version: 0.51.1-1; application version: still 0.51.1.

- Repaired DEB SHA-256: `dce5758167ad20728cafb3028440b860bf95324aebce46b5ebf7177564a68049`.
- Original frozen DEB SHA-256: `03f934d6c542d135e90149fe32a968494642986dc78d398ae52692e16adc426f`.
- All **586 installed files** have identical bytes, modes and owner/group IDs.
  Only DEBIAN/control version/dependency metadata changes.
- The original frozen package was rehashed after the repair and is unchanged.
  No release tag or GitHub asset was moved, replaced or published.

## Verification

- Bash syntax check and normal/revised/DEB-only packaging fixtures passed.
  Invalid revision/argument cases and direct writes into frozen releases were
  rejected. Default upstream package naming/TAR output remains compatible.
- **12 APT resolver scenarios passed** for both the tiny packaging fixture and
  actual repair: ICU 70/72/73/74/75/76/77/78, multiple ICU providers, OpenSSL t64,
  missing ICU and unsupported ICU. The two latter cases correctly fail resolution.
- The original published DEB failed the ICU-78 scenario; the repaired one passed.
- dpkg version comparisons confirm 0.51.1 < 0.51.1-1 < 0.51.2.
- APT ran in simulation with temporary synthetic status/cache/config and empty
  source lists, without host package state, installation or network refresh.
- Repository/link/diff checks passed. No application code, dependencies or
  lockfiles changed; no full application build/test/release matrix was run.

Local verification used WSL Ubuntu 24.04.4. APT metadata simulation is not a
native Kali GUI or actual ICU-78 loading test. Desktop/input/RDP/Secret Service
acceptance remains separate. The dependency regression is registered for future
Linux CI and the full Linux package check; no remote CI result is claimed here.
