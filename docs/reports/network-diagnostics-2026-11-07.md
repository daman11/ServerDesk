# Network Tools error diagnostics — 2026-11-07

Focused development change after 0.51.0. No protocol/session lifecycle,
database schema, dependency or release-version changes.

## Problem and change

Network Tools showed run and UI-action failures only in its status text. These
paths did not record a safe stack or correlation ID. The shared network error
formatter was also used for local actions, so a SQLite write failure or failed
export could appear as a generic network failure.

- Run and guarded-action failures now use the existing safe application logger
  at Warning level, with a distinct ErrorId shown in the tool status. Context
  consists only of the fixed tool/operation name, such as
  `NetworkTools.Ping.History.Delete` or `NetworkTools.Http.Results.Export`.
- Local guarded actions use local database/filesystem/application guidance;
  service execution keeps its protocol-specific network guidance.
- Optional history/preference failures retain successful results and show the
  first associated ErrorId. Each failure still has its own log event.
- Input-validation errors and expected cancellation do not produce error
  events. Existing run timeout/cancellation presentation is unchanged, without
  a new log event or ID on that path. A late failure is recorded after its tab closes without changing
  detached controls or opening a dialog.
- The full status can be selected/copied, including its ID. A scrollable area
  bounded to 96 layout units replaces the two-line clipping limit.
- Added the tool-status accessibility label in PL/EN and completed two missing
  Polish catalog entries from the preceding protected-content recovery change.

No request body, header, password, token, collection name or target is added to
diagnostic context. The logger continues using ExceptionDiagnostics, excluding
exception Message/Data and the raw exception object while retaining bounded
types/codes/stacks. This is not a complete audit or anonymization of all logs.

## Focused evidence

- 15 selected unit tests pass: ExceptionDiagnosticsTests, OperationErrorTests
  and LocalizationTests. The first localization run detected the missing
  Polish entries; the final run passes with matching catalogs/placeholders.
- `ServerDesk.UiSmoke --network-diagnostics` passes on Windows/headless in
  Polish and English, using disposable SQLite databases and an injected ICMP
  probe. No real ICMP, external service or OS credential store is accessed.
- Ten induced operational failures produce distinct safe Warning events with
  matching visible IDs, except the deliberately late closed-tab event. Tests
  check outer/inner stack evidence, codes, absence of a private canary from
  Message/Data/export content/path, and no raw exception attached to log events.
- A controlled SQLite trigger refuses history deletion/append. The original
  history survives, the local-database message is correct, diagnostics retain
  their result despite optional persistence failure, and deletion retries after
  the trigger is removed.
- An occupied parent path refuses export, preserving its existing bytes and
  showing export guidance. Invalid input and cancellation create no error log;
  retry clears the old failure. A delayed failure after removing the tab does
  not update status, reopen a window or access credentials.
- The full status copies to the headless clipboard with the ID. At 920 × 620
  the message area stays bounded and Start remains visible in PL/EN.
- Final harness and Windows development builds have no warnings or errors.
  Repository/link checks and `git diff --check` pass.

Local evidence: `artifacts/reports/network-diagnostics-2026-11-07/diagnostics-final.trx`
and the `ui-02` result/screenshots. The first harness attempt had a duplicate
teardown call, corrected before the passing final run; it is not counted as PASS.

## Limits

These checks certify the listed failure-presentation paths and controlled
retries. They do not exercise actual WAN/IPv6/VPN, SNMP devices, physical WoL,
native Linux desktop/clipboard, hardware failures or a complete security audit.
The remaining [stabilization plan](../stable-release-plan.md) stays open.
