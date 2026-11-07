# Monitoring data validation — 2026-11-07

Focused development change after 0.51.0, without a schema/dependency/version
change or new release. No SSH server, VM or OS credential store was used.

## Reproduced problems

The disk parser accepted `NaN` and infinities as percentages; clamping did not
reject these inputs. NaN could therefore reach progress presentation. The JSON
address parser also accepted invalid IP strings, mismatched address families and
out-of-range prefixes. A malformed explicit prefix could silently become a bare
address. An invalid JSON gateway was counted as a route, suppressing a usable
text fallback. A gateway without a device received an empty separator.

Before the fix, 20 of 22 new regression cases failed; the two already valid
cases passed. This is captured in `before-fix.trx`, not counted as passing
acceptance. The inputs are deliberately malformed fixtures, not evidence of a
particular production distribution emitting these values.

## Changes

- Reject nonfinite disk percentages before creating a DiskInfo record. Other
  valid disk rows, mount-point spaces and unit conversions are preserved.
- Validate JSON IP addresses against their declared IPv4/IPv6 family and
  explicit integer prefixes against 0–32/0–128. A genuinely absent prefix
  preserves a valid bare address; an invalid explicit prefix rejects the entry.
- Ignore invalid JSON gateways so the existing independent text fallback can
  supply valid routes. Valid gateways without a device have no empty separator.
- If no disk rows can be parsed, Monitoring shows the localized unavailable
  message instead of presenting raw malformed `df` output as disk statistics.
  The raw snapshot remains unchanged in the model. CPU/RAM and other sections
  continue to render; a later valid sample restores the disk card and footer.

The collector command, authentication, polling, optional-plugin lifecycle,
transport and credential handling are unchanged.

## Verification

- **61/61 selected unit tests pass**, none skipped: the 22 new malformed-input
  cases plus LinuxDetailsTests, MonitoringSampleTests,
  MonitoringCompatibilityTests and NetworkTrafficTests. They cover valid
  neighboring data, malformed/missing fields, partial samples, recovery,
  historical text fallback and existing CPU/traffic baselines.
- **Focused headless UI PASS** in Polish/English and dark/light at 900 × 760.
  Injected parser snapshots exercise the real Monitoring controls and bottom
  metrics bar: missing disk has no progress row or fake zero, CPU/RAM remain
  visible, invalid IP is absent, valid gateway fallback is shown, all progress
  values are finite, and a corrected sample restores `/: 45%` and clears the
  partial-data warning.
- The UI fixture never marks SessionPane connected or invokes the SSH/polling
  path. A denied credential-store fixture records zero calls. The bottom bar's
  presentation input is supplied directly; this is not a logged-in server test.
- Harness and incremental Windows development builds pass with no warnings or
  errors. Repository/link and diff checks pass.

Evidence: `artifacts/reports/monitoring-validation-2026-11-07/monitoring-final.trx`
and `ui/result.txt` with eight partial/recovered PL/EN dark/light screenshots.
Opt-in UI command: `ServerDesk.UiSmoke ROOT --monitoring-validation`.

## Remaining limits

This closes the listed validation regressions, not Linux monitoring acceptance
across distributions, privilege models, missing utilities or real IPv6 routing.
It does not replace live SSH testing, native Linux desktop/hardware tests or
the other items in the [stabilization plan](../stable-release-plan.md).
