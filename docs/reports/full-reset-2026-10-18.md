# Full application reset — 2026-10-18

Focused follow-up after 0.42.0 and the maintainer's explicit selection of a full application reset with backup. The schema and credential/session lifecycles are unchanged. This extends the existing reset path; it does not create release packages or replace the frozen 0.42.0 artifacts.

## Scope and recovery

- Restore defaults remains a preferences draft: Save persists it, Cancel restores the previous preferences. Connections and plugin data are preserved.
- A confirmed factory reset queues a distinct `application-v1` request. Cancel remains available until startup; the normal guarded restart closes sessions before the reset.
- Startup creates a fresh verified SQLite `before-reset` backup from the same read snapshot used for deletion, including final shutdown writes. A competing writer prevents the snapshot's write upgrade; it cannot erase unbacked newer data.
- One transaction clears all six application tables: profiles (including embedded notes/artwork), folders, folder icons, host keys, settings (including tags and plugin references/documents) and history. An error/cancellation rolls back every deletion and keeps the request retryable. No migration, recursive directory cleanup or external file deletion is involved.
- Protected DPAPI/Secret Service entries remain in place for local recovery. New application state contains no references to old entries, so command libraries/collections and saved login references disappear from normal use. The reset neither decrypts/exports secrets nor deletes the OS keyring. Logs, existing backups and external keys/downloads/unrelated files are preserved. A database snapshot alone is not a portable credential backup; recovery requires the original protected store on the original account/machine.
- All new schema-3 snapshots omit the pending reset marker, including manual snapshots taken while a reset is scheduled. Restoring them cannot replay the reset.
- Old `1` requests keep their original preferences-only scope, with an explicit explanation in settings. An existing/unknown request cannot be silently replaced by a full reset. Unknown request versions are ignored at startup.

Close every other ServerDesk instance before restarting for reset or restoring a backup. Another already running app instance could otherwise write its cached state after the reset. No maintainer data was reset during validation.

## Validation

- Focused Windows reset/backup/settings/localization/SSH/operation tests: **98 passed**.
- Focused Linux reset/backup/SSH/operation tests under WSL Ubuntu: **72 passed**. This does not certify Kali KDE/Wayland behavior.
- Disposable fixtures cover failure to create the scheduling/startup backup, cancellation, SQL-trigger rollback across all tables, late writes, plugin/vault references, idempotence, restoration without replay, legacy/unknown requests and retention of unrelated data.
- Windows application/UI smoke builds: **0 warnings, 0 errors**.
- Four affected UI scenarios passed: `--settings-reset`, `--ssh-errors`, `--database-backups` and `--presentation-consistency`. Reset checks cover Polish/English, full-scope confirmation, legacy scope presentation, draft Save/Cancel, scheduled-reset cancellation/reopening, restart handoff and compact Graphite/Light layouts at 960×720 and 680×500.
- `--published-settings-reset` passed against the actual self-contained Windows development executable: reset occurs before startup language/workspace loading, removes the disposable profile/history/plugin state, retains both backups and closes with exit code 0.
- Repository content/link checks and `git diff --check` passed. Evidence is in `artifacts/reports/full-reset-2026-10-18/`; the development executable is `artifacts/dev-reset-ssh/ServerDesk.App.exe`.

The focused local cycle intentionally leaves the application version at 0.42.0, with development commits ahead of the release tag.
