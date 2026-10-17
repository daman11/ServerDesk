# Everyday navigation and connection ergonomics — 2026-10-17

Focused development iteration after `13d5914`. Existing protocol clients, credential handling, profile storage and release version remain unchanged.

## Changes

- Ctrl+Shift+O, the main menu and action palette reveal/focus connection search without toggling an already visible sidebar. The current browser query and filters are retained; invoking it from Transfers/Network returns to Connections.
- Down/Enter in connection search focuses current tree results after the queued rebuild. It does not connect. Escape from the tree returns to search; Up from the first visible node also returns. The destination is checked against live filters, and a subsequent focus change prevents background focus stealing.
- Quick connect uses a dedicated window with an empty initial address, protocol examples, inline validation, endpoint/login preview and explicit connect/edit-as-profile actions. It uses the existing strict `QuickConnect.Parse` rules and revalidates current text on activation. Password URLs remain rejected. Editing a parsed destination opens the normal profile editor; cancellation has no persistence or protocol side effects. The body scrolls independently and actions adapt to compact widths.
- Session search uses a resizable layout with a scrollable list and fixed actions. Single-line names/endpoints, separate state/location metadata, an active marker, clear-search control, Ctrl+F and empty guidance improve navigation. Pick reparses the current query against current choices, preventing fast Enter from activating a result excluded by a pending search update. Arrow selection scrolls into view.

## Verification

- 29 focused checks: existing quick-connect validation, additional protocol URLs, session search and PL/EN localization.
- `--ergonomics` production UI fixture in PL/EN: reveal/preserve filters/repeated shortcut, rapid search-to-tree Enter, Escape and hidden sidebar; invalid/password URLs, SSH/RDP/VNC/Serial preview, fast stale activation, edit intent and Enter with fresh text; session fast-search activation, empty results and no sessions. Graphite/Light at font 16, 500 × 500 switcher and 460 × 460 quick-connect window with action bounds checks. No real connection or credential read.
- Existing `--session-workflow`: restored/floating/duplicate session references, no automatic credential read or connection, current profile presentation, search/Enter/empty results, selection styling, opt-out and corrupt snapshot behavior.
- Existing `--connection-browser`: folder/menu/sort/selection and temporary search expansion regression coverage.
- Incremental Release build and repository/whitespace checks. Captures inspected for both window layouts and English text.

Selected evidence is in `artifacts/reports/ergonomics-2026-10-17/`. Disposable test data is isolated from maintainer data. This change does not require a full matrix, installer, version bump or release tag.
