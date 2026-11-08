# Batch connection tags — 2026-11-08

Development change after 0.51.1; no schema, dependency, protocol/session lifecycle
or credential-store changes. [Usage in PL/EN](../connection-tags.md).

The connection-area and saved-profile menus open an explicit selection dialog.
Candidates are a snapshot of the browser's current search/protocol/tag/section
filters, including collapsed folders. Search within the dialog retains selection;
selected-only mode and full selection/change/result counters make the target set
reviewable. Existing tag colors are displayed and remain unchanged.

Add/remove operations use exact, case-insensitive names. They preserve existing
spelling and skip semantic no-ops, including legacy tag formatting. The save path
rebuilds the plan from current field values, rather than trusting a potentially
queued TextChanged notification. A busy writer blocks duplicate save/close and
drains before cancellation closes the dialog.

The repository snapshots requests and uses one SQLite transaction. Conditional
updates compare the preview's original tag text; a deleted/retagged target rejects
the entire batch. Only the JSON Tags field changes, preserving concurrent edits
to other fields, unknown JSON properties and credential bindings. No missing
profile is inserted. Errors use a static diagnostic operation/correlation ID;
the UI distinguishes a stale preview from other local save failures.

## Verification

- **31/31 selected unit/repository cases**, no skips: ProfileTagsTests and
  ProfileTagChangesTests. These cover parsing/no-ops, overlap/duplicate targets,
  durable reopen, concurrent non-tag edits, unknown JSON, shared colors,
  unselected profiles, stale/deleted targets, mid-batch SQLite refusal with
  rollback/retry, pre-cancellation and a 5000-profile planning fixture.
- New **batch-tags** headless scenario passed in PL/EN, Graphite/Light, font 16:
  real browser filter scope, captured-profile preselection, preview/cancel/save,
  hidden selections, overlap validation, 620×500 controls, failed save/retry,
  stale-preview guidance and delayed writer cancellation/double-save protection.
  Immediate Save uses the latest field value. No credential calls or protocol
  sessions occurred. Rendered wide/compact views were inspected.
- Existing **connection-tags**, **browser-context-menu** and **help** scenarios
  passed in PL/EN. The menu regression now includes the new action. Embedded
  Help and the current guide describe scope, selection and rollback behavior.
- Release-configuration builds completed without warnings/errors. Repository,
  Markdown/link, localization/resource and diff checks passed. The new UI
  scenario is registered in CI; no remote workflow is claimed for this local work.

Local evidence: `artifacts/reports/batch-tags-2026-11-08/`. Earlier failed harness
iterations remain diagnostic evidence; final scenarios completed successfully.

All data was disposable. The UI checks use Windows/headless Avalonia, not a
native Linux desktop or physical input acceptance. Mid-transaction power loss
and native cancellation while SQLite is writing were not simulated; actual
rollback/retry used a SQLite trigger, while in-flight UI cancellation used a
controlled delayed writer. No full release matrix or installer was produced,
and these checks do not establish 1.0 readiness.
