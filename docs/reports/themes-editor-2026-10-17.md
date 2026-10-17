# Theme presets and SFTP editor — 2026-10-17

Small development increment after 0.41.0; existing version, tags and frozen release packages are unchanged.

- Add six application palettes: Ocean, Forest, Aubergine, Dracula, Solar Paper and Sandstone. Eleven presets plus System use the existing live preview/save/cancel flow. Light variants carry explicit metadata; the System entry is resolved by the catalog length rather than a fixed index. Terminal palettes remain independent.
- Settings → SFTP → File editing persists an automatic/preset/custom editor. Older preferences default to Automatic. A custom program uses an absolute executable path without argument templates; unavailable programs fail with guidance. The installed program receives the absolute file path as one argument through `ArgumentList`, with shell execution disabled. Automatic selects only named text editors, never file associations.
- Local Edit file and remote Download and edit are available in file menus, Operations and F4. Only one regular file is supported. A remote copy uses the existing transfer queue, atomic download, destination checks, overwrite confirmation and cancellation. The editor opens only after a successful download; failures/skips/cancellation do not open an older local copy. A queue Retry repeats the download; use local Edit file afterwards if the initial attempt failed. Changes are uploaded explicitly through the existing Upload operation, with its normal overwrite confirmation.
- Editor launch reads saved preferences, independently of an unsaved settings preview. No editor is installed by ServerDesk, and editor processes are not terminated when a pane closes. Downloaded files remain in the selected local directory.

Focused Windows verification:

- Incremental Release build, zero warnings/errors.
- `--themes-editor`: primary/secondary text contrast at least 4.5:1 on standard surfaces for all 11 themes; PL/EN live preview, dark/light variants, System selection, cancellation, persistence/reopen and default restoration. Searchable SFTP preferences, invalid custom-path field recovery and 680×500 layouts at interface font 16.
- The file-menu fixture launches only the owned UiSmoke child process and checks the exact Unicode/space/metacharacter path, saved editor choice despite a different live preview, menu icon and blocked directory/link/multiple selections. It does not start an installed editor or use personal connections.
- 35 filtered `TextEditorTests`, `ApplicationSettingsTests` and `LocalizationTests`: preference compatibility/atomic rejection, theme persistence, literal process arguments, missing-program failure and PL/EN catalog parity.
- `--design-refresh`: existing settings search/help/empty recovery/draft rollback and welcome/SFTP layout regression in PL/EN.
- Actual development EXE startup in three isolated Windows processes: off-screen correction, maximized restore, disconnected-monitor fallback and clean shutdown. A fresh `artifacts/dev-themes-editor/` folder avoids stale app-local runtime libraries found in the older shared harness output.
- Repository/link and whitespace checks. Selected renders/TRX/summary live in `artifacts/reports/themes-editor-2026-10-17/`; disposable databases are not retained there.

No full matrix, installer, native Linux desktop/editor acceptance, VMware or real SSH/SFTP endpoint test is claimed for this increment. The remote action reuses the existing transfer engine; real editor behavior and a complete remote editing/upload round trip remain manual acceptance items.
