# Connection browser workflow — 2026-10-17

Development after 0.40.0. Five related improvements to the original profile/folder MVP; the application version and frozen release packages are unchanged.

## Changes

1. **Creation context.** New connections inherit the selected folder or selected server's folder. No selection, or the Transfers/Network Tools section, uses the root. The organization page remains editable; cancellation writes nothing.
2. **Rename continuity.** The prompt captures its original folder before awaiting input. After a successful repository transaction, collapsed descendant paths move to the new prefix and the current selection survives. Icons/profile assignments use the existing repository transaction. A rejected rename leaves view state unchanged; similarly named siblings remain independent.
3. **Deletion continuity.** Confirmation refers to the original folder even if selection/rebuild changes while it is open. Successful deletion removes obsolete collapsed paths, preserving other selections and branches. Cancel/nonempty-folder rejection preserves data. Recreating a deleted path starts expanded.
4. **Profile drag and drop.** Folder targets share validation with a new localized root target below the tree. Unchanged destinations, unknown IDs, hidden profiles, unrelated data and inactive sections are rejected. Successful folder moves expand ancestors and select the moved profile. An explicit reveal after layout scrolls the selection into view without moving keyboard focus; delayed callbacks cannot reveal an obsolete selection.
5. **Keyboard workflow.** Tree-local F2 edits/renames, Delete confirms deletion, Ctrl+D opens the duplicate editor, and Enter connects a server or toggles a folder. Menus display shortcut hints. Duplicate names use the current PL/EN language. Hidden selections cannot trigger these actions before a queued filter rebuild. Text fields and terminal controls retain their keys.

## Focused verification

- Windows: Release build of the UI harness, zero warnings/errors; `--browser-workflow` in PL/EN, followed by existing `--connection-browser` and `--sorting` regressions.
- Ubuntu/WSL: Release harness build and the same new PL/EN scenario. The final managed scrolling change is also rerun with the rebuilt platform-independent App/harness assemblies and existing Linux native dependencies.
- The scenario uses a fresh SQLite database and an in-memory credential store. It never connects to a server, reads saved personal profiles or accesses the system vault.
- Checks include actual editor save/cancel, changing selection during open prompts, exact prefix boundaries, icon/profile retention, a depth-32 folder chain, rejected rename, nonempty deletion, cancellation, recreating a deleted path, production routed drag-over/drop events, metadata isolation and selection position inside the viewport after moving to root.
- Keyboard cases cover F2, localized duplicate/cancel, Delete cancel/confirm with a changed selection, modifiers, folder Enter, text-field isolation and rapid actions immediately after filtering. Deleted-profile credentials are represented only by counted in-memory calls.
- Graphite/Light captures cover PL/EN at 900×600, sidebar 220 px, interface font 16. The explicit root target stays within the sidebar; renamed collapse paths are checked after shutdown.
- The new opt-in scenario is included in the future Windows/Linux CI list. Local evidence: `artifacts/reports/browser-workflow-2026-10-17/`.

## Limits

Routed input and headless rendering do not certify physical drag gestures, keyboard layouts, screen readers, mixed DPI, native KDE/Wayland or large-tree resource endurance. The depth-32 case verifies operations/state; it is not a claim that every deeply indented label fits a narrow sidebar. No full solution/test/platform matrix, version bump, installer or release tag was produced for this focused UI batch. The remaining stable-release criteria stay open in the [1.0 plan](../stable-release-plan.md).
