# Changelog

## Unreleased

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
