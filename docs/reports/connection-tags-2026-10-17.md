# Connection tags — 2026-10-17

Development change after 0.41.0; no version bump, release package or tag. Existing protocol sessions, credential references and SQLite schema remain unchanged.

## Behavior

- Case-insensitive, exact tag membership; commas, semicolons and line breaks support existing profile strings. Multiword names are preserved. The profile editor normalizes delimiters and removes duplicates only when saved.
- Profile editor chips support removal and suggestions from saved profiles. Cancel discards assignments. Preview is bounded to 32 chips; existing tags remain in the editable text.
- Connection rows show up to two bounded labels and an overflow count, with the full tag list in a tooltip. Theme resources provide readable text independently of tag color.
- A searchable, virtualized tag picker provides global connection counts, any/all/untagged matching and a shared color palette. Color and filter drafts apply together on confirmation; Cancel discards drafts. Automatic colors are deterministic across restarts. Active filters can be removed individually or cleared with all browser filters.
- Search, protocol, Favorites/Recent and tags intersect. Immediate keyboard activation uses the same live predicate as the tree. Temporary expansion during filtering does not overwrite saved folder collapse states.
- Color overrides use the existing settings table. Optional `TagColors` metadata extends configuration JSON version 2, retaining version 1/2 import compatibility. Imports validate color metadata before writes and merge it inside the profile import transaction. Explicit local colors win on a case-insensitive name collision.

## Verification

- 55 focused unit/repository/localization checks: tag parsing, exact any/all/untagged matching, deterministic palette, metadata validation and duplicate rejection, save/reopen/export/import, legacy exports, atomic invalid import/save, existing search/repository behavior and PL/EN catalog parity.
- Production UI fixture `--connection-tags`: PL/EN filter intersection and immediate Enter, clear, color cancel/apply, case-insensitive tag search, untagged controls, editor suggestions/removal/cancel/normalized save, temporary folder expansion and long-label bounds. Graphite and Light at 900 × 680 and interface font 16; tag dialogs at 500 × 530.
- Existing `--connection-browser` fixture covers folder/menu/sort/selection/reload behavior and search expansion persistence.
- Incremental Release build of the affected Windows application and UI fixture. Repository content and whitespace checks.

Selected evidence is retained in `artifacts/reports/connection-tags-2026-10-17/`. Disposable data is isolated from maintainer profiles and credentials. No real connections, VM tests, full release matrix or installer build were required for this bounded connection-management feature.
