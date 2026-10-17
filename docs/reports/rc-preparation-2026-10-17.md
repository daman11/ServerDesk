# RC preparation — 2026-10-17

Six bounded tasks from stable-release plan stage 6. Application code, version 0.40.0, frozen packages and tags remain unchanged; this step changes test/release tooling, CI configuration and guides.

1. Extend the installer harness with an explicit older Setup and matching extracted payload. Exercise real 0.39.1 installation/start, 0.40.0 upgrade/start and uninstall, with isolated database files and an unknown file preserved.
2. Delete the installed app DLL and runtime configuration in the owned fixture. Rerun the same Setup and compare every installed file with the frozen ZIP payload; user-data file set and hashes remain identical.
3. Replace Core/Infrastructure DLL bytes with controlled corrupt content in that fixture. Rerun Setup and verify complete byte restoration without data/unknown-file changes.
4. Remove the owned uninstaller, rerun Setup and verify that the generated uninstaller has identical bytes. Start/close the repaired app and uninstall; check that all expected payload files disappear while user data/unknown files remain.
5. Add `audit-dependencies.ps1`: run the SDK's vulnerable/transitive query without restoring, pin JSON schema 1, and reject command failure, stderr, diagnostics at any level, advisory results, missing/duplicate/unexpected projects, invalid sources, malformed/incomplete reports or unsupported schema. Add bounded decision fixtures and future Windows/Linux CI steps with summary-only upload.
6. Correct the stale GitHub guide (0.36.2 and mandatory releases after small changes), document the new gates/repair workflow in PL/EN, and add a concrete RC checklist retaining open publication/hardware/clean-VM requirements.

## Local checks

- Windows, SDK 10.0.401: 23 synthetic audit decisions pass. Include zero-exit source errors/warnings, project/framework diagnostics, direct/transitive advisories, malformed input and coverage/flag failures; the clean SDK shape omitting frameworks is accepted.
- Actual NuGet query on all four solution projects: command exit 0, zero reported advisories, no diagnostics/stderr. Raw output and summary are under `artifacts/reports/rc-preparation-2026-10-17/audit/`. This is known-advisory coverage, not a complete security review. Locked dependency files remain unchanged.
- Real frozen Setup 0.39.1 and 0.40.0 with verified SHA-256 input files; matching 0.40.0 ZIP extracted into a fresh fixture. The native harness checks all 461 payload files through older-version upgrade, fresh install/start, busy install/uninstall refusal, reinstall, all three repairs, repaired startup and full owned-payload removal. Three input guards reject a mismatched installer/payload or non-older previous version before creating an install fixture. No registration test: explicitly skipped on the maintainer desktop and reported separately.
- The harness starts only owned processes with disposable `SERVERDESK_DATA_HOME`, never production connections or a normal credential store. It waits for clean shutdown before comparing database/settings/log hashes. Fixtures are inside workspace artifacts; mutations check absolute paths and reject reparse points. Installer timeout/startup failure cleanup affects only processes started by the harness.
- PowerShell syntax, repository/link checks and staged whitespace checks.

## Limits and remaining work

These tests run on the existing Windows host, not a clean VM or every supported OS. The old/new installers both use schema 3 and the databases are newly created by the fixture; this does not certify historical user datasets or schema migration. Newer development UI commits are not silently included in frozen 0.40.0 packages.

Full gates, broader version/system acceptance, registration on a clean account, public GitHub CI, private security contact, pilot users and critical-bug triage remain open in the [RC checklist](../rc-checklist.md) and [stable plan](../stable-release-plan.md). No full application test matrix, application build, new installer, version bump, tag or remote publication was needed for these tooling/documentation changes.
