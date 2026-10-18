# Settings recovery — 2026-10-18

Focused development increment after 0.42.0; no version change, release package or tag rebuild. Existing frozen release files are preserved.

## Behavior

- Restore defaults stages the known preference controls in the current form. Save commits; Cancel rolls back the preview. Defaults share the repository's ApplicationSettings.Defaults definition.
- Security → Restore settings offers a separate factory reset of preferences, language, window placement, tree expansion, sorting, file-panel proportion and saved workspace. It preserves profiles/folders/icons, credentials, history, SSH trust, connection tag colors, unknown settings and plugin data.
- Confirmation creates a coherent verified database backup before writing a pending request. No successful copy means no request. The operation is independent of the form's Save/Cancel; reopening shows the pending state and an explicit cancellation action. Cancellation preserves the copy.
- Startup consumes the request in a single transaction against an explicit key allowlist. The request is removed in that same commit. Earlier close-time autosaves therefore cannot restore the old layout. A write error rolls back all removals and leaves the request retryable. Normal startup takes no reset writer lock when no recognized request exists.
- Reset is applied before language selection and main-window placement. The existing restart handoff and session/transfer close confirmations remain in use. Cancelling shutdown or a launch failure leaves the request pending; it can be cancelled in settings. Close other instances before restarting: concurrent independently running windows can subsequently save their own preferences.
- No schema migration, dependency change, recursive data deletion, credential-store access or protocol/session implementation change. No reset has been run on maintainer data.

## Verification

Passed: 45 focused Windows tests (factory reset, settings, localization, workspace persistence and plugin settings) and all six new reset regressions on Linux/WSL. Windows incremental app/harness builds have zero warnings/errors. Headless `--settings-reset`, `--extended-settings` and `--presentation-consistency` passed; the reset scenario is registered in CI.

Targeted unit and UI results are retained under artifacts/reports/settings-reset-2026-10-18. The new fixture covers confirmed/rejected reset requests, draft defaults Save/Cancel, reopening and cancellation, backup preservation, guarded replacement callback, late shutdown writes, fresh main-window defaults and retained user data. PL/EN Graphite/Light at 960×720 and 680×500, including scrolling restart actions above the fixed footer with larger text. The replacement is a test callback; no actual application or real sessions are terminated.

`--published-settings-reset <exe>` also passed against the real self-contained Windows development EXE: a separate owned data directory is seeded, backed up and marked for reset; actual App startup consumes the request before language/workspace loading, starts in Polish defaults, preserves the profile/plugin document/backup and closes normally. The harness owns that process and uses no maintainer profile database. This complements the simulated UI restart handoff rather than claiming a native multi-session acceptance run.

Native Linux desktop, OS credential providers, system file pickers and the full release matrix are outside this focused increment. The factory reset preserves their data rather than attempting to erase it. A database snapshot does not include OS credentials/private keys and contains sensitive infrastructure metadata; use the existing private backup handling and [manual restore guidance](../database-backups.md).
