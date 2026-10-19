# Changelog

## Unreleased

## 0.44.0 — Public Beta — 2026-10-19

- Add a Windows RDP clipboard export dialog: copy remote files, inspect their names and drag a Copy-only virtual-file source to a local folder. Validate descriptor names/counts/sizes and expose read-only bounded streams without hidden file staging or remote delete feedback; invalidate stale/disconnected selections. Keep automatic clipboard redirection and existing credentials/session behavior. Live RDP/input/network acceptance is deferred at the maintainer's request; local tests and remaining Shell/gesture limits are recorded separately.

- Open new RDP profiles in ServerDesk by default, retaining explicitly saved external-client choices. Accept local file/folder drops on the embedded Windows desktop through an owned native OLE target, prepare the bounded clipboard selection and send a scoped remote paste at the drop point. Preserve automatic text/image clipboard redirection; reject stale, obscured or disconnected targets. Fix hit testing across the clipboard drop zone's padding and keep its width stable during preparation. Add native/headless regression checks and disposable Windows RDP receiver evidence, including a physical file gesture and eight consecutive verified drops.

- Fix verified database backups and factory reset in deep Windows data directories: use a shorter unique staging filename to reserve SQLite journal path space, opt into long paths in the application manifest, and validate the published PE. Add a backup/reset regression and record native Windows 11/Ubuntu VMware acceptance, including real clipboard/vault checks and explicit remaining RDP/hardware limits.

- Add SFTP text clipboard actions for selected names, full paths and the current directory, including cached offline views and Ctrl+Shift+C. Add Ctrl+F filtering and Escape back to the list; preserve native text editing. Disable stale selection operations while filtering/loading and unavailable file actions after disconnect, with matching toolbar/context-menu states.

- Harden application error handling: correlate operation failures with a copyable error ID, prevent simultaneous/late error dialogs, contain dialog-presentation failures, and retain safe outer/inner stacks for settings, history, backups and optional persistence. Record unobserved task/fatal failures, flush on exit, add startup build/runtime context and process IDs, and rotate logs daily or at 4 MiB with 14 retained files. Bound diagnostic formatting and tolerate broken stack formatters without recording exception messages/data.

- Add a right-click menu on the connection sidebar header and blank list area for creating profiles/folders, quick connection, import/export and clearing filters. Keep row-specific and text-editing menus; avoid replacing folder rows during selection. Correct saving profiles created from a folder or quick-connection draft.

- Give Help sections distinct numbered cards, larger headings, tinted header bands and clear gaps between sections while keeping related paragraphs together. Preserve multiline shortcuts, searching and whole-topic copying.
- Complete the shared icon metadata for Help's clear-search action, caught by the release presentation audit on Windows/Linux.

- Improve Polish/English Help readability: put each keyboard shortcut on its own line, group shortcuts by context, and split dense file-transfer, RDP and troubleshooting instructions into shorter sections.

- Add an offline Help workspace tab with 14 Polish/English documentation topics, full-content search, selectable/copyable articles and responsive topic navigation. Open it from the menu, application rail, action palette or F1 outside terminal/desktop input; reuse the existing tab and support docking without treating help as a saved connection.

- Align welcome action icons with their headings and keep equal column indents across wrapped descriptions, shortcuts, languages and narrow layouts.
- Synchronize application and Fluent accent palettes, including custom selection surfaces, focused inputs and native controls. Preserve primary button colors during hover/press and use contrasting text/check marks for light and dark accents; restore preset colors on preview cancellation or clearing the override.

- Improve SFTP transfer ergonomics: open full read-only details and copy them from either queue, keep sidebar cards compact, and choose between clearing successful jobs or all finished history while preserving active work.
- Restore SFTP keyboard focus after directory navigation without taking focus back from a control selected while loading, so Enter followed by Alt+Up remains usable.

- Security review: mask reflected HTTP Basic Auth and form credentials, including short secrets and escaped JSON values; preserve sensitive JSON field detection and use one-pass literal masking with a fail-closed time limit. Add local privacy/pentest regression cases and SSH host-key rejection probes; document data collection and OS-vault limits.

## 0.43.0 — Public Beta — 2026-10-19

- Save profile identity changes with a new local credential binding in the same database transaction, then retire old secrets. Failed writes preserve the original profile/password; unavailable cleanup cannot expose an old password to a new destination. Clear bindings on duplicate/import/export and reject stale editor saves.
- Commit profile deletion before credential cleanup. A failed database delete retains the saved password; a locked/unavailable vault after a committed deletion produces a separate cleanup warning.
- Protect Commands from stale/concurrent writers with an optimistic snapshot check and atomic pointer exchange. Serialize Network Tools index/history mutations in SQLite across windows/processes; partial or cancelled vault writes preserve the previous complete library.
- Migrate to schema 4 with a verified pre-migration backup, retaining legacy credential bindings and profile bytes. Older applications must reject this schema rather than ignore new credential bindings. Backup verification supports schemas 0–4.
- Bound silent SFTP uploads/downloads by a progress-based idle timeout; close only their owned connection and retain destination/partial-file protections. Check quiet SSH connections with a bounded read-only exec channel, allowing explicit recovery after a silent outage or long idle/suspend.
- Preserve SFTP panel/directories, navigation, filters and selection across SSH reconnect. Distinguish transfers interrupted by transport loss from user cancellation; require explicit retry and fresh overwrite consent.
- Explain SSH authentication, local private-key, DNS, connection-refused, timeout and server disconnect failures in Polish and English. Keep the specific reason in the session header/tooltip and record safe protocol/network codes without exposing credentials or claiming an opaque authentication rejection proves a wrong password.
- Separate draft-only preference defaults from a confirmed, cancellable full application reset. Before resetting at startup, create a fresh verified database snapshot including final shutdown writes; atomically clear connections, folders/notes, history, SSH trust, tag colors and plugin state. Preserve protected vault entries for local backup recovery, logs, previous backups and external files. Strip pending reset requests from snapshots so restoring a backup never replays the reset; legacy preferences-only requests retain their original scope.
## 0.42.0 — Public Beta — 2026-10-18

- Reject newline-suffixed log filenames and prevent exports through directory junctions/symlinks from overwriting live logs. Add cross-platform edge-case tests for nested aliases, rotation, parallel exports, sparse files and unavailable destination directories.
- Complete icon metadata for tag-filter actions; align localization and protocol smoke checks with the current sidebar and cover every settings category.
- Add colored connection tags, editable tag chips and Any/All/Untagged filtering with draft color previews. Improve workspace navigation, quick connection, session switching and calm selected-row presentation.
- Add Settings → Security → Logs and diagnostics with a read-only daily-log viewer, preview search, refresh, folder opening and full-file copy export. Bound large previews to 128 KiB and preserve existing exports on failures/cancellation.
- Fix saved SSH/RDP profile deletion when the Windows credential directory is absent. Treat missing credential files/directories as already forgotten, preserve real access/IO failures, distinguish filesystem/database/network errors, and log deletion failures with paths and stack traces. Add scoped persistence, restart and failure regression checks.
- Replace the brief About message with localized application, license and component tabs. Include the original MIT license offline and copyable build/runtime information for bug reports.
- Improve the Commands plugin with fixed search/filter controls above its scrolling system profile, a risk filter, contextual empty-state actions and Enter/Down/Escape/Ctrl+F navigation. Give cards category/risk information and organize details into parameters, preview and guidance, with visible explanations of unavailable dispatch actions.
- Separate navigation into connection, transfer, network-tool and plugin groups. Give sidebar modules a framed icon/title/description header, and make command/network headers and session plugin boundaries explicit across themes.
- Add Ocean, Forest, Aubergine, Dracula, Solar Paper and Sandstone application themes with live preview; keep System selection independent of the number of presets.
- Add a saved SFTP text-editor preference (automatic, VS Code, Notepad++, Kate, GNOME Text Editor or a custom executable). Edit local files or download and edit a remote file from its menu/F4; upload saved changes explicitly through the existing transfer workflow.
- Keep contextual help and restart notices visible while searching settings; show accent presets only with their matching field.
- Add an actionable settings search empty state with a compact layout above the language-restart reminder; clearing results preserves draft values and returns focus to search.
- Give empty Favorites/Recent views contextual icons and a route back to saved connections, or creation when no profiles exist. Label filter clearing and restore search focus after clearing.

## 0.41.0 — Public Beta — 2026-10-17

- Preserve saved Network Tools collection secrets when an index write fails. Commit index deletion before retiring its encrypted blob; keep the original storage error if cleanup also fails and allow safe retry.
- Add disposable historical-schema migration/restore, forced-process-termination and bounded 8 MiB Linux tmpfs disk-full acceptance, including original data/export preservation and successful retries. Register recovery checks in CI without using personal stores or connections.
- Write the tracked Windows installer ownership ledger with repository LF/UTF-8 encoding so repackaging preserves its source bytes.
- Prepare the next candidate toward 1.0. Native VM/hardware acceptance is deferred at the maintainer's request; retain Public Beta status and explicit platform limits.

- Extend the isolated Windows installer harness with older-version upgrades, missing/corrupt payload repair, uninstaller recovery and full owned-payload removal checks; preserve user data and unknown files.
- Add a dependency-audit gate for complete solution coverage, command/diagnostic failures and direct/transitive advisories, with bounded regression fixtures and future CI checks. Align RC/GitHub/repair guides with the lightweight development cycle and remaining acceptance gates.
- Give connection folders distinct headers, persistent folder symbols alongside custom icons, consistent expander spacing and localized Empty badges for empty leaves.
- Add 36 selectable system/service symbols for profiles and folders, including Linux distributions, Windows Server, Proxmox, Docker, Kubernetes, web servers, databases and monitoring tools. Bundle pinned Simple Icons silhouettes with source hashes, attribution and individual licenses; render filled marks in theme foreground colors.
- Start new connections in the selected folder (or selected server's folder), retaining an empty root default when nothing is selected.
- Keep folder rename/delete prompts tied to their original target; preserve other selections, remap collapsed descendant paths after a successful rename and clear them after deletion.
- Add a root-folder drop target, reject unchanged/unknown/filtered profile drops, and reveal/select a profile after moving it into a folder.
- Support tree-local F2 editing/renaming, Delete confirmation, Ctrl+D duplication and Enter folder expansion, with menu shortcut hints and localized copy names. Preserve text-field/terminal key handling.
- Reuse each visible profile's unchanged artwork during metadata edits instead of validating/decoding it again. Decode Base64 once when artwork changes, release owned bitmaps on detach, retain valid artwork despite a malformed legacy color, and render a safe protocol icon for null/corrupt/oversized legacy artwork.

## 0.40.0 — Public Beta — 2026-10-17

- Add live interface font selection, control corner radius, menu/connection row spacing, server addresses and independent tab icon/close-button visibility. Preserve defaults for older preferences and support search, cancel, save and reset in PL/EN.
- Add an explicit read-only check for a selected database snapshot in Security settings: SQLite integrity/schema feedback in PL/EN, rejection of changed/missing/corrupt/newer/journal-dependent files, and draining checks when settings close. Keep restoration manual and disclose that this does not validate application data or credential-store contents.

- Let Stop/Escape cancel queued subnet calculations, supersede automatic work on manual Start, coalesce Restart requests and let Stop prevent a pending restart.
- Drain diagnostic actions and delayed work when a tab closes; prevent late collection/secret/UI updates and cancel owned save prompts. Publish saved-control refreshes only while the pane is alive.

## 0.39.1 — Public Beta — 2026-10-17

- Write Commands and Network Tools exports through flushed temporary files and replace the destination only after success; preserve previous files on failure/cancellation, reject provider-only destinations, and drain diagnostic exports on close.
- Restrict new Unix staging/export files to the owner's read/write permissions.
- Reject duplicate configuration JSON fields before importing and perform stream reading, validation and SQLite work outside the UI thread.
- Preserve corrupt Network Tools history, collection indexes and remembered values instead of silently resetting them; validate collection payloads, null request fields and WoL favorites before applying them.
- Retain successful diagnostic results when optional history/preferences persistence fails, with localized warning guidance.
- Clarify that RDP fullscreen applies to the separate client window; embedded desktops follow their panel size and retain stored preferences.
- Reject stale profile moves before creating destination folders; preserve profile metadata and unknown JSON fields during folder renames, and accept unchanged names without writes.
- Inspect folder contents under the same write transaction as deletion/rename; preserve competing edits and legacy implicit-parent deletion behavior.
- Export profiles, folders and icons from one SQLite read snapshot; verify storage-error rollback/retry for configuration imports without replacing local folder icons.
- Prepare configuration JSON before staging it next to the destination, then flush/close and replace the selected file; preserve previous exports on preparation/write/publication failure or cancellation. Add PL/EN failure guidance and require a filesystem path instead of truncating provider-only files.
- Save profiles and nested folders in one SQLite transaction so a failed write preserves the previous profile and leaves no partial ancestor chain.
- Move profiles together with any new destination folders in one transaction; retain the original destination and metadata on failure, and allow retry.
- Include destination ancestors in recursive folder-rename transactions alongside folder paths, icons and profile assignments; roll back the entire rename when any write fails.
- Add a read-only catalog of the 20 newest database snapshots in Security settings, with timestamps, sizes, schema/reason, copied paths and an explicit folder launcher; exclude incomplete/unrelated files.
- Show descriptions and command templates on browsable cards; open full details and parameter forms on click/Enter, with Back/Escape navigation and retained filters.
- Unify settings section headings and help text, give Plugins a distinct icon and compact built-in badges, and keep command headers readable in narrow panels.
- Preserve icons and destination-language tooltips in restart confirmations, distinguish primary dialog actions, and add responsive HTTP result-tab icons. Keep empty result guidance within narrow split panels while retaining table scrolling.

## 0.39.0 — Public Beta — 2026-10-17

- Add a stable built-in plugin catalogue and lifecycle contract for Linux monitoring and Commands, with independent settings, localized descriptions and live apply after Save.
- Preserve older settings, immutable JSON value semantics, unknown opt-outs, user snippets and favorites. Hide and guard disabled entry points; keep the core status bar and transfer counter.
- Cancel optional SSH work independently, drain monitor/discovery tasks, prevent late panel/sample updates, and retain the existing SSH terminal and SFTP during disable/enable.
- Extend PL/EN UI and real disposable OpenSSH acceptance; no external DLL loader, NuGet dependency or database migration.


## 0.38.0 — Public Beta — 2026-10-17

- Host Linux X11 FreeRDP inside ServerDesk tabs, retaining the same authenticated desktop across tab changes, split views and docking. Wayland sessions use XWayland.
- Add masked RDP credentials and a graphical certificate review with selectable SHA-256, identity warnings and explicit verification before trusting an exceptional certificate for the current tab.
- Pass Linux RDP passwords through redirected stdin, isolate the client trust cache, retain NLA, and drain/stop owned processes on reconnect and shutdown. Keep native Wayland, Linux RDP file drag-and-drop and the wider physical keyboard/clipboard matrix outside this release.
- Add bounded password/argument/lifecycle tests, PL/EN RDP GUI coverage and an opt-in disposable NLA/Xvfb acceptance lab.


- Keep FreeRDP certificate confirmation interactive by removing `/from-stdin:force` from PTY launches; retain NLA and normal certificate verification.
- Retain bounded console diagnostics for the legacy external-client adapter; production Linux RDP now uses GUI authentication and localized exit feedback.
- Add client-side resize edges and a visible corner grip for the Linux main window; hide the grips when maximized or resizing is disabled.

- Replace the generic connection-time credential-save error with an explicit choice to connect without another save attempt or cancel. Explain Linux Secret Service requirements in PL/EN while preserving the existing secure storage backend.
- Add focused missing-helper/read-only-store UI coverage and a real disposable SSH recovery check; document troubleshooting for Linux password storage.

- Refit tracked dialogs when display scale or monitor layout changes, restore original resize limits on a larger screen, and keep confirmation content scrollable above its actions.
- Add opt-in PL/EN desktop UX coverage for five themes, larger text, simulated DPI transitions, keyboard navigation, accessible category names and collection of closed dialog windows.
- Add a read-only Linux desktop preflight and a native acceptance checklist for Kali/KDE/Wayland; extend session endurance measurements with CPU, working set and post-close counters.

- Defer connection-row visuals and folder-menu actions until presented/opened; retain profile icons, status, sorting details and drag handlers while reducing allocations for collapsed trees.
- Keep empty folders accessible without overlaying the first-server prompt; preserve localized folder actions and icons across repeated menu opens.
- Expand the opt-in large-browser check to 5,000 profiles and 200 folders, including protocol/search filters, numeric IP sorting, matching selection and the hidden-profile Enter guard. Add a measurement-only mode for bounded before/after comparisons.

- Keep Linux monitoring network details on older iproute2 without JSON support, using validated one-line addresses/link details and separate IPv4/IPv6 route fallbacks.
- Show IPv6 default gateways alongside IPv4; ignore duplicate gateways and malformed fallback rows without discarding other metrics.
- Treat inconsistent CPU counter intervals as unavailable rather than showing a misleading 0%; resume after a valid pair of samples. Add focused parser and real OpenSSH/UI regression coverage.

## 0.37.0 — Public Beta — 2026-10-16

- Preserve selected folders across connection-tree sorting and refresh. Clear filtered server selections and check live filters before Enter/double-click can start a connection.
- Keep search-expanded folder states temporary, retaining the normal collapse state and sort preferences after restart. Add PL/EN browser acceptance to Windows/Linux CI alongside sorting and database backups.
- Surface unconfirmed SFTP temporary-file cleanup with exact local/remote paths, warning status and queue counts; retain earlier cleanup warnings after a successful retry without deleting older partial files automatically.
- Include successful transfers with cleanup warnings in the attention filter and make transfer details searchable. Explain cancellation, completed files and restart-from-zero retry in PL/EN.
- Unlink the exact remote temporary entry during cleanup, preserving the target if that leaf was replaced by a symlink.
- Explain SFTP transfer failures in Polish/English with recovery guidance for permissions, missing paths, authentication, timeouts, connection errors and known local disk-full codes. Preserve host-key and integrity safeguards; wrap queue details with full tooltips.
- Correct the chmod prompt to pass octal digits to SSH.NET without converting them twice. Report a disappeared transfer source separately from deletion-preview changes, retaining link/ancestor validation.
- Let explicit SFTP Refresh/F5 and reopening the file panel replace a detected broken browser connection while retaining the live SSH terminal, paths, filters, selections and navigation history.
- Serialize browser recovery, revalidate SSH host trust and cancel pending recovery when the tab closes. Post-transfer refresh still does not reconnect or repeat file operations automatically.
- Release SFTP connections and global transfer slots before refreshing directory listings, so stalled browsing cannot block queued copies. Preserve successful transfer results when refresh fails.
- Keep transfer retry buttons in sync with SSH loss/reconnect and share their enabled state with the Transfers overview. Add a real loopback regression for stalled refresh, queued cancellation and retry to the original destination.
- Add verified SQLite snapshots before migrating existing databases and a manual backup action under Settings → Security, with Polish/English feedback.
- Include committed WAL data, preserve completed backups, and stop migrations when snapshot creation fails. Keep schema version 3 unchanged.
- Explain database startup failures and close without saving workspace/preferences; preserve the original database for recovery. Document snapshot limits and restoration.

## 0.36.2 — Public Beta — 2026-10-16

- Complete action icons across layout, app, tab, folder, terminal, SFTP and command menus, protocol toolbars, and Network Tools actions.
- Add ten application-owned vector symbols for distinct panel layouts, equal sizing, font size and run/stop; preserve all pinned upstream assets.
- Align Polish/English module and action names across menus, welcome, sidebar, action palette and network tabs. Separate menu shortcut hints from action titles.
- Give the action palette row icons and distinguish it from Commands. Align Monitoring and RDP desktop captions with accessibility names.
- Translate network choice captions while preserving the raw authentication, profile and export values used by backend requests.
- Add a production action/icon audit and menu/protocol/network screenshots to UI acceptance; include this audit and icon integrity checks in CI.

## 0.36.1 — Public Beta — 2026-10-16

- Refine Commands with a calmer selection surface, a separate command preview card and three fixed dispatch actions. Move favorite/edit/duplicate to accessible header icons.
- Collapse system profile options initially and bound the scrollable filter area, preserving list, preview and action space in small windows and with larger interface text.
- Use a compact responsive Settings header, clearer section dividers, quieter field captions and consistent rounded cards across the application.
- Match command, SFTP and Network Tools selection colors to all five application themes. Preserve keyboard focus and existing command confirmation safeguards.
- Extend visual acceptance for PL/EN, all themes, compact windows, expanded profiles and interface font 16.

## 0.36.0 — Public Beta — 2026-10-16

- Add one shared Commands panel for the offline Smart Cheat Sheet, user snippets, favorites, optional ID-only recent history and Polish/English search.
- Ship 165 versioned command variants / 134 operations across ten categories, with distro/tool inheritance, risk, privilege and shell metadata.
- Add encrypted user-template CRUD, nested categories/moves, host/folder scope, typed parameters, safe shell quoting and strict JSON import/export.
- Discover Linux and Windows through bounded separate exec channels of existing SSH sessions; cache per connection, support manual/pinned profiles and never probe RDP/VNC/Serial as shells.
- Insert without Enter; preview and confirm explicit execution, confirm Dangerous operations twice, refuse stale session/connection targets, and keep multiline scripts/secrets copy-only.
- Preserve existing four-panel docking and protocols; add offline/editor/security and real loopback SSH acceptance coverage. No new runtime dependencies or SQLite table migration.

## 0.35.0 — Public Beta — 2026-10-16

- Add three panels (one tall plus two stacked) and a four-panel 2×2 grid. Fill empty groups from spare docked tabs when changing layout, preserving existing sessions and buffers; merge hidden groups safely when reducing the split.
- Extend docking, tab order, Ctrl+Tab, the session switcher and floating-window actions to four independent groups. Add panel numbers, empty-panel targeting and drop hit testing for each layout.
- Persist selection in all four groups and independent horizontal/vertical split proportions. Read older snapshots with the existing floating-dock value and default the new axis without reading credentials or automatically connecting.
- Add Settings → Workspace preferences for startup layout, new-tab placement, tab-strip height, separator width and panel numbers. Preview sizes live with cancel rollback; apply the startup choice on next launch.
- Resize content rows directly so the horizontal separator cannot stretch the adjacent tab header. Support pointer dragging and Up/Down/Home on that separator; add an Equalize panel sizes action.
- Initialize a loaded but unmeasured terminal template before applying cursor preferences to restored/inactive cards, fixing a possible null reference.
- Add model/persistence/compatibility tests, Windows/Linux layout and real four-session SSH acceptance, retained two-panel regression and published native RDP-host coverage for three/four panes.

## 0.34.0 — Public Beta — 2026-10-16

- Expand Settings to six searchable categories, including a dedicated SFTP page, with responsive PL/EN fields and notes on when each preference applies.
- Keep category navigation in one scrollable column, including small windows with a language restart reminder; reveal selected categories for pointer/keyboard navigation.
- Add terminal cursor shape/blinking with live preview/cancel. Preserve remote cursor overrides when unrelated preferences change; reapply defaults on a new SSH/Serial connection.
- Configure terminal scrollback from 0 to 100,000 lines on the next connection, preserving the current buffer during preview/save/cancel.
- Add an absolute local SFTP start directory with a folder picker. Resolve it off the UI thread on first SFTP open; fall back to home with a dismissible warning if unavailable.
- Configure initial hidden-file visibility, separate local/remote sorting and direction, and the active panel. Preserve browser choices across settings saves and reconnecting the same tab.
- Restore mixed true/false defaults and reveal the correct invalid field after searching. Keep legacy defaults and atomic persistence without new dependencies or a schema migration.

## 0.33.0 — Public Beta — 2026-10-16

- Add independent local/remote SFTP Back, Forward, Parent and Home controls, Alt+Left/Right/Up/Home shortcuts and Escape to restore an unsubmitted path.
- Keep at most 100 successfully loaded directories per panel in memory. Commit history only after both directory reads succeed, preserve forward navigation on refresh/failure and clear history on reconnect/close.
- Restore confirmed paths after failed navigation; disable stale file rows immediately while paths are edited or reads are pending. Transfers, new directories and link destinations require confirmed directory paths.
- Capture refresh inputs before waiting for the gate and reject superseded queued reads; retain existing large-list filtering and transfer cancellation behavior.
- Fit full-width paths, navigation buttons and horizontally scrolling breadcrumbs in narrow panels; add two icons from the existing pinned Lucide revision and PL/EN text.
- Add nine unit cases, Windows/Linux layout and real OpenSSH/SFTP acceptance, plus CI coverage. Bound the UI harness wait helper so a missing completion cannot block indefinitely.

## 0.32.0 — Public Beta — 2026-10-16

- Restore the local release cadence: finish user-visible changes with versioned packages, verification and an immutable Git tag; add a read-only check that detects code ahead of its release or mismatched packaged source.
- Include the full documentation tree in binary packages so the offline index and linked feature/release reports are available.
- Add local scrollback search to SSH/Serial terminals: Ctrl+Shift+F, literal/case/whole-word matching, previous/next navigation, Unicode and wrapped-line matches, bounded result counts, refresh after output and a return-to-current-output action.
- Keep search/status rows separate from terminal content, clear search on reconnect/close and consume local key releases so Kitty input cannot leak search shortcuts to the remote host.
- Add Settings → Security → Trusted SSH keys with host/port/fingerprint search, fingerprint copying and explicitly confirmed removal of one reviewed key. Compare the stored fingerprint when deleting so a stale dialog cannot remove a replacement key.
- Keep changed SSH keys blocked and point the error to trust management. Verify renewed SSH/SFTP trust, preservation of live sessions and busy-database recovery on isolated fixtures.
- Filter and sort large local/SFTP directory views off the UI thread, coalesce rapid edits, cancel obsolete sorts and drain view work on reconnect/close. Preserve selection and prevent stale results from replacing a newer directory.
- Add 50,000-entry-per-panel acceptance checks in Polish/English on Windows/Linux and include them in CI.
- Add safe generated-artifact cleanup with preview, release/report retention, process/shortcut protection and no-follow link deletion.
- Import the exact 0.31.1 release source into Git and preserve it with an immutable baseline tag.
- Organize release notes and historical verification reports; add a documentation index and Git/GitHub contributor workflow.
- Add pinned Windows/Linux CI, issue/PR templates, Dependabot, repository checks and consistent editor/line-ending settings.
- Pin the tested SDK and public NuGet source; track dependency lockfiles for every developer tool.
- Remove machine-specific RDP probe paths and SSH lab usernames. Use a disposable NSS alias and loopback listeners by default.
- Include repository/GitHub metadata in future source exports and follow the reorganized documentation paths when packaging.

## 0.31.1 — Public Beta — 2026-10-16

- Decode NSIS source and includes explicitly as UTF-8, fixing Polish welcome text and protocol separators in Setup.
- Display pending/saved language reminders, actions and restart confirmation in the destination language without switching the running UI.
- Add Save and restart in Settings and Restart now in the saved reminder. Validate and persist settings before requesting shutdown.
- Use the existing session/transfer shutdown and workspace persistence. The replacement waits for the exact original process to exit before opening application data; cancel or launch failure leaves sessions running.

## 0.31.0 — Public Beta — 2026-10-16

- Prepare SSH/SFTP private keys, derive passphrases, connect and clean up failed clients off the calling UI thread. Already-cancelled requests do not read the key file; cancellation after preparation prevents a new connection.
- Execute monitoring commands, dispose their channels and parse samples in the background.
- Preserve valid monitoring sections when CPU/memory files or optional df/ip/lscpu commands are unavailable. Mark missing sections explicitly; do not display unavailable RAM as zero usage.
- Reset CPU/traffic baselines after missing samples, detect CPU counter overflow, validate memory units/ranges and mark the MemFree fallback as an estimate.
- Clear monitoring cards/graphs when a session closes or collection fails; recover the normal presentation when data returns. Added PL/EN notices and status-bar tooltips.
- Recreate a lost monitoring-only SSH client after backoff while keeping the terminal connected; retain one monitoring loop across retries.
- Added 14 unit cases plus real OpenSSH preparation, restricted-PATH/permission and partial/recovered UI acceptance checks.

## 0.30.0 — Public Beta — 2026-10-15

- Unregister linked cancellation sources when a session closes; share one idempotent close task and wait for connection, refresh, appearance, duration, monitoring and history work before releasing lifetimes.
- Track active/queued transfer tasks and drain them before reconnect/close. Application shutdown also waits for sessions already removed from the workspace.
- Dispose SSH, SFTP and monitoring transports off the UI thread with bounded channel-close waits. SSH completion is signalled even if cleanup fails.
- Stop dependent browser/monitoring connections and transfers when the SSH terminal ends.
- Drain the previous window's pending layout while a pane is detached, preventing an invalid layout-manager exception when floating a live terminal.
- Added repeatable lifecycle, real OpenSSH reconnect/network-loss and bounded multi-session endurance checks. See the verification report for measurements and remaining limits.
- Include the version's verification report in both application payloads so local documentation links work offline.

## 0.29.0 — Public Beta — 2026-10-15

- Replace connection filter results in one collection reset, compile search terms once and create extended server context menus only when first opened.
- Coalesce transfer progress into one pending UI callback; discard stale updates after completion.
- Bound pending upload bytes by acknowledgements, propagate transport errors to blocked readers and abort a stuck dedicated transfer client after cancellation with a short grace period.
- Close transfer clients off the UI thread. Download via the public SFTP stream with bounded close on cancellation, preserving local originals and cleaning local partial files.
- Use cancellable asynchronous directory/existence/attribute reads; remove redundant pre-sorting and check cancellation between local entries.
- Added real OpenSSH loss/cancel/retry tests, a 5000-file directory scenario and 1000-profile UI measurements. Remote partial files may remain after network loss; retry restarts the transfer.

## 0.28.0 — Public Beta — 2026-10-15

- Fixed Windows Escape encoding for SSH and Serial VT streams; negotiated Kitty/Win32 input modes remain emulator-controlled.
- Ctrl+Shift+C without a selection no longer sends remote Ctrl+C/SIGINT. Selected text still copies; Ctrl+C retains its terminal behavior.
- Open SSH channels and negotiate PTYs off the UI thread. Coalesce resize requests into one pending grid and perform network writes in the background.
- Wait for SSH transport shutdown on reconnect/close. Added bounded-queue regression tests and real password-only OpenSSH keyboard, clipboard, resize and fullscreen-program acceptance checks.
- No new production dependencies, database migrations or credential formats. See the release verification report for exact tested scope.

## 0.27.0 — Public Beta — 2026-10-15

- Original MVP SFTP: inspect resolved symbolic-link targets, open linked directories, create remote links to selected existing targets and explicitly copy a selected link's contents after a source/destination preview.
- Copy/retry captures the approved resolved source; changing the alias after the preview does not redirect the transfer. Nested links remain skipped and counted; link objects are not recreated locally.
- Recursive transfers stop at 20,000 entries/64 directory levels per job, detect active-ancestor cycles and enumerate local sources off the UI thread. Remote tree listing is bounded.
- Transfer destinations that are links or incompatible types are rejected; source types are rechecked. Refresh displays canonical remote paths, including relative input. Broken local links no longer require reading their target length to list them.
- Parallel database test cleanup now clears only its own SQLite pool, fixing cross-fixture native-handle disposal without disabling concurrency.
- Updated original-MVP acceptance status; no new dependencies, schema changes or protocol implementation. Dedicated real OpenSSH link tests cover Windows and Linux, with local link upload on Linux.

## 0.26.0 — Public Beta — 2026-10-15

- Responsive welcome dashboard with equal action cards, real saved/favorite/protocol counts and up to three recent connections. Renames and deletions refresh the list; rendering never reads credentials or starts sessions.
- Settings search across option, section and category names, ignoring case/diacritics. Includes match counts, empty state, Ctrl+F, Escape/clear, draft retention, preview rollback and validation that exposes filtered invalid fields.
- Local/remote SFTP panel headings identify the computer/server and active side; clicking moves focus without losing selection. Remote captions track profile renames.
- File rows and column headers adapt to interface font size and density. Narrow welcome/settings/SFTP layouts and Polish/English Graphite/Light views covered by a dedicated UI regression scenario.
- No new dependencies, credential storage formats or protocol implementations. Existing sorting, terminal and transfer behavior covered by regression tests.

## 0.25.2 — Public Beta — 2026-10-15

- Fixed text-based IP sorting: IPv4 and IPv6 compare numeric address bytes; equivalent spellings are normalized and ports sort numerically. Serial address sorting uses the device path/COM number. Protocols sort by their visible names.
- Natural server/folder/file names put server2 before server10, without integer overflow on long numbers. Folder groups remain first with stable natural ordering.
- Connection sorting has an explicit ascending/descending toggle. The chosen order also applies in Recent and Favorites; Recent no longer silently overrides the selector. Both settings are saved atomically, rapid updates keep the latest choice, and closing waits for persistence.
- Address/date sorting displays the sort value below the server name. Missing connection/file dates stay last in either direction.
- Local and remote SFTP column headers sort by name, size, extension/type and modified date; repeated clicks reverse direction. Headers, selectors and arrows stay synchronized without losing selection. Directory size metadata does not reorder folders by a value displayed as unknown.
- PL/EN and narrow-layout fixes; real SSH/SFTP regression includes natural file names, header direction, refresh, selection and a 1 MiB round trip. No new dependencies or protocol rewrites.

## 0.25.1 — Public Beta — 2026-10-15

- Responsive settings navigation: compact accessible icons on narrow windows, full labels at larger widths; edits and closing are guarded while settings are being saved/applied.
- Shared dialogs keep actions outside scrollable content. Diagnostic messages support selection/copy, Escape closes informational dialogs, and dialogs fit the working area of the owner's monitor. Profile and deletion dialogs use the same screen bounds.
- Command palette handles multi-word searches, empty results, Up/Down without losing search focus, and direct commands for all nine Network Tools. Added Network Tools to the welcome screen.
- Saved-host selection preserves the WoL MAC and fills the IPv4 destination; subnet targets include /32 or /128 and HTTP formats IPv6 correctly.
- Network results have complete selectable details and useful empty states. Running tools lock request inputs until Stop/completion, tooltips and tab widths track the target/preferences, HTTP tab captions omit private URL data, and the status bar describes the active tool.
- Closing ServerDesk also asks before stopping active diagnostics. Polish/English messages updated; no new dependencies or protocol implementations.

## 0.25.0 — Public Beta — 2026-10-15

- Nine real Network Tools: Ping, Traceroute/MTR, DNS Lookup, TCP Port Scanner, Wake-on-LAN, HTTP Client, TLS Inspector, IPv4/IPv6 Subnet Calculator and SNMP v2c/v3 Browser.
- Standalone targets with no saved hosts, editable context-menu targets, independent instances in the existing tab/split/floating workspace and cancellation on close.
- Local history without credentials, custom DNS/OID/WoL favorites, secure collections in the OS credential store and TXT/CSV/JSON/PEM exports.
- Polish/English themed forms, live ICMP chart, JSON formatting/coloring and bounded output. Linux ICMP uses iputils-ping when .NET raw ICMP is unavailable to an ordinary user.
- Added DnsClient 1.8.0 (Apache-2.0) and SharpSnmpLib 12.5.7 (MIT), with full pinned upstream notices. No new project; Serial protocol implementation unchanged.
- See `docs/network-tools.md` for capabilities, security behavior, usage and limits.

## 0.24.1 — Public Beta — 2026-10-15

- Restored session cards have a single, scrollable placeholder with wrapped text. RDP, VNC and serial descriptions no longer overlap it.
- Transfers has its own file/server search, task-status filter and actual SFTP job list. Connection protocol filters, sorting, counters and add-folder actions stay in the connection browser.
- Shared progress, cancellation, original-task retry, source-session navigation and clearing of finished jobs. Updating progress preserves sidebar controls and keyboard focus.
- Polish/English messages and narrow-window layout improvements. Transfer history covers open sessions and remains bounded to 50 finished jobs per session.

## 0.24.0 — Public Beta — 2026-10-15

- Batch and recursive deletion in local/SFTP panes, with a read-only preview, server identity, full paths, counts and explicit permanent-delete confirmation.
- Progress, cancellation and partial results; no automatic retries. New files after preview are not added to the approved plan.
- Bounded scope and traversal, symlink target preservation and checks for changed entries/ancestors. Remote rename preserves symlinks; chmod on a symlink is refused.
- Polish/English dialogs with a scrollable preview and fixed actions in smaller windows.
- No new runtime dependencies. See `docs/release-0.24.0.md` for usage and concurrency limits.

## 0.23.0 — Public Beta — 2026-10-15

First public distribution of ServerDesk, under the MIT license.

- Windows x64 installer for the current user, portable ZIP, Linux x64 DEB and TAR.GZ.
- Source archive, SHA-256 checksums, dependency inventory and full third-party license notices.
- Polish and English installation, upgrade, backup and troubleshooting guides.
- SSH terminal, SFTP transfers, monitoring, Windows embedded RDP, external Linux RDP, embedded VNC and serial sessions.
- Split workspace, detachable tabs, server/folder icons, custom artwork, five themes and PL/EN interface.
- Includes the security and stability fixes from 0.22.1 and 0.22.2.

This is a public beta. See `docs/known-issues.md` for the tested platforms and limitations.

## Earlier development versions

Detailed development notes are retained in `docs/release-*.md` in the source archive.
