# ServerDesk 0.36.2 — getting started

**New: Commands — Ctrl+Shift+K.** Browse 165 command variants offline or follow the active SSH host. Add encrypted user snippets, parameters and favorites. Insert never sends Enter; Execute requires confirmation. [Guide, architecture and limits](commands.md), [0.36.2 notes](releases/release-0.36.2.md).

Choose **Layout → Three panels (1 + 2)** or **Four panels (2 × 2)**. Spare docked tabs fill empty panels. Drag a tab to another panel or use its **Dock in panel 1–4** menu. Click an empty panel's number before opening a connection. Drag separators to resize; **Equalize panel sizes** resets both axes. Reducing the split keeps tabs open in the remaining groups.

**Settings → Workspace** controls startup layout, where new tabs open (active/first/fewest tabs), tab-strip height, separator width and panel numbers. Sizes preview live; Cancel restores them. The startup layout applies on the next launch without connecting automatically. [Release notes](releases/release-0.35.0.md).

In SFTP, use Back/Forward or Alt+Left/Right, Alt+Up for the parent directory and Alt+Home for home. Ctrl+L selects the path; Enter opens it and Escape restores the open directory. Each panel has its own history, cleared when the whole SSH session reconnects.

**Development builds after 0.36.2:** if only the file connection breaks, choose **Refresh / F5** or reopen SFTP. A detected broken browser connection is restored while keeping directories, filters and history, without restarting the terminal. Host trust is checked again. This only reloads directory listings; it does not repeat transfers, deletions or renames. If the entire SSH session is lost, reconnect that session first.

Free, open-source software under MIT. **Public beta**; see [known-issues.md](known-issues.md) for tested platforms and limitations. Polski: [getting-started.md](getting-started.md).

## Windows x64

Run `ServerDesk-0.36.2-win-x64-Setup.exe`. Setup installs for the current user, creates a Start menu shortcut and registers an uninstaller. No separate .NET installation is needed. Alternatively, extract the **entire** portable ZIP into its own directory and run `ServerDesk.App.exe`.

This beta is not signed with a trusted publisher certificate. Use a trusted release source and do not disable Windows protection. Compare `Get-FileHash .\ServerDesk-0.36.2-win-x64-Setup.exe -Algorithm SHA256` with `SHA256SUMS.txt`. A checksum alongside a download detects corruption; it does not independently authenticate the publisher.

## Linux x64

Ubuntu/Debian: `sudo apt install ./serverdesk_0.36.2_amd64.deb`, then launch ServerDesk from the menu or run `serverdesk`. Remembered passwords need `libsecret-tools` and an unlocked Secret Service such as GNOME Keyring. Install your distribution's FreeRDP client for RDP connections.

For TAR.GZ: `mkdir serverdesk-0.36.2`, then `tar -xzf ServerDesk-0.36.2-linux-x64.tar.gz -C serverdesk-0.36.2` and `./serverdesk-0.36.2/ServerDesk.App`. Native library requirements are listed in the DEB and [development.md](development.md). Linux testing used WSLg; other desktops need further verification.

## Connect

1. Click `+`, choose SSH, RDP, VNC or Serial and enter your connection details.
2. Connect. Verify the SSH host-key fingerprint or RDP certificate with your administrator. Enter passwords in the application; remembering them is optional.
3. For SSH, select Terminal, SFTP or Monitoring. Drag tabs to another pane or detach them into a separate window.
4. Change themes, interface size, terminal preferences and language in `Menu → Settings`. **Restart the application after changing its language.** Reminders appear in the selected language. Choose **Save and restart**, or save and use **Restart now** in the main window later. Open tabs require confirmation before sessions and transfers disconnect. Cancelling keeps the app open; the saved language loads at the next launch. Tab restoration follows the workspace recovery setting and never connects automatically.

In SSH terminals, **Ctrl+Shift+C** copies a selection (sends nothing without one), **Ctrl+Shift+V** pastes and **Ctrl+C** interrupts the remote process. Escape reaches the remote terminal, including vim's edit-mode exit. The remote PTY follows the panel size when resizing the window. See [terminal changes](releases/release-0.28.0.md).

VNC traffic is not encrypted. Use a VPN or an existing secure tunnel; do not expose VNC directly to the Internet.

The welcome dashboard shows up to three recently used connections. Clicking one follows normal sign-in; opening the app never connects automatically. In Settings, **Ctrl+F** finds an option across categories; **Escape** in the search field clears the filter. Searching preserves your draft and Cancel rolls back the appearance preview.

**Local computer** and **Remote · server** headings identify each SFTP side. Color marks the active side for operations/shortcuts; click a heading to focus its list. In a narrow pane, use **Local/Remote** to switch sides. See the [design and interaction changes](releases/release-0.27.0.md).

## SFTP operations and deletion

**Settings → Terminal → Cursor and history**: choose a bar, block or underline and cursor blinking. Cursor preview is immediate; Cancel restores the previous choice. The 0–100,000-line history limit applies to the next SSH/Serial connection and preserves the current buffer (0 disables history). Remote applications can change the cursor.

**Settings → SFTP**: choose an absolute local start directory (empty means home), hidden-file visibility, active panel and independent sorting/direction for each side. These defaults apply the first time SFTP is opened in a tab. Saving preferences and reconnecting that tab retain its browser choices; a new tab uses the new defaults. An unavailable start directory falls back to home with a notification. [Release notes](releases/release-0.35.0.md).

Use **Copy link contents…** for one symbolic link. Confirm the resolved source and destination; the contents become regular files/directories and nested links are skipped. **Details** shows the resolved target; Enter/double-click opens a linked directory. **Create remote symbolic link…** links to the selected existing target. [Detailed behavior and limits](releases/release-0.27.0.md).

Select files/directories and choose **Delete selected…** or press Delete while the list has focus. The read-only preview identifies the computer/server, full paths and all entries, including hidden contents. **Delete permanently** starts work. Nonempty directories are supported; link targets are not traversed. Cancellation does not undo completed deletions. After an error/disconnection, inspect the result and refresh the list; counts include acknowledged requests. The preview is limited to 20,000 entries and 64 levels.

## Network Tools

Open **Network Tools** from the left activity bar or Menu. Choose a tool, enter a target, expand **Parameters** and select **Start**. No saved connection is required. A server's context menu can prefill an editable address. Each tool is an independent workspace tab; Escape stops, Ctrl+Enter starts and F5 restarts. **History and collections** keeps safe history and stores full collections in the OS credential store. On Linux install `iputils-ping` for ICMP without root. See [network-tools.md](network-tools.md) for capabilities, exports and limits.

## Upgrade, backup and uninstall

Close sessions and quit ServerDesk. Back up the whole data directory while the app is closed: `%APPDATA%\ServerDesk` on Windows; `~/.local/share/serverdesk` and `~/.config/serverdesk` on Linux (or your XDG paths). Linux passwords are stored separately in the system Secret Service. Windows DPAPI passwords are bound to the user/machine. JSON export transfers profiles **without passwords or private-key contents**.

Run Windows Setup over the existing installation in the same directory. Extract ZIP/TAR updates into a **new directory**, without mixing versions. Update a DEB using `apt install ./new-package.deb`. Application files are separate from user data. Do not open a database changed by a newer version with an older version; restore a backup into a separate data directory instead.

Uninstall Windows through Settings → Apps; remove a DEB with `sudo apt remove serverdesk`. Delete the extracted directory for ZIP/TAR. User data and remembered passwords are preserved; see [privacy.md](privacy.md) for deletion.

## Report problems

Include version, OS, display scale, protocol and reproduction steps. Logs are in the data directory; redact infrastructure identifiers before sharing. Never send passwords, keys, credential files or the database. Follow `SECURITY.md` for private vulnerability reports.
