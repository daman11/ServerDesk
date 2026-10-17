# Settings search and empty-section usability — 2026-10-17

Small UI development step after 0.41.0. Application version, frozen release packages and existing tags remain unchanged.

- Settings search keeps a matching section's help, including next-connection limits and language-restart guidance. It no longer overwrites a note's independent visibility. Accent preset controls follow their own field, so a font search does not expose unrelated color actions.
- Empty search results have a themed icon, heading and Show all settings action. The action clears the query, restores focus and preserves unsaved preferences. A compact layout hides the decorative icon when the language reminder reduces available height; scrolling remains available at smaller viewport sizes, without drawing over the footer.
- Empty Favorites/Recent sections use star/history icons. With saved profiles, Connection list returns to the browser without opening a session or editor; with no profiles, New connection remains available.
- Filter reset has a visible Clear label, complete count tooltip and search focus after clearing. Clearing the query's inline button also restores focus; protocol/filter semantics are unchanged.

Focused Windows validation:

- Incremental Release application/UI harness build, no warnings/errors.
- `--design-refresh`: settings help while filtering, unrelated accent controls hidden, draft retention and cancel rollback, Show all settings/keyboard focus, PL/EN Graphite/Light, 680×500 with font 16 and a pending language change. Recovery is checked against the actual content viewport as well as the window; existing welcome/SFTP layout checks remain part of this scenario.
- `--connection-browser`: contextual empty sections and return navigation, icons in PL/EN Graphite/Light, 220 px sidebar at font 16, unclipped labelled reset and recovered search focus; existing selection/filter/sort/folder regression.
- Eight filtered `LocalizationTests`, repository/link checks and whitespace checks.

Selected final JSON/TRX/logs/renders: `artifacts/reports/ui-ux-refinements-2026-10-17/`. Disposable UI databases stay outside retained evidence. A development Windows executable is prepared from the final local commit, preferring `artifacts/dev-harness/ServerDesk.App.exe` when stopped.

These are automated/headless Windows checks, not physical keyboard, monitor/DPI or native Linux acceptance. No server connection, personal data store, full application test matrix, version bump, new installer or release tag is needed for this step.
