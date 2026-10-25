# ServerDesk 0.51.0 Public Beta

Local release, 2026-10-25. MIT for ServerDesk; separate bundled Bitwarden OSS
component retains its GPL notices and corresponding source. Remote publication
is a separate maintainer decision.

## Four MVP work items

1. **Show server in connection list** from a saved session's tab menu or the
   action palette. Clears filters, shows the sidebar, expands ancestors and
   selects that saved profile. Quick/deleted profiles cannot create new entries.
2. **Expand all folders / Collapse all folders** in the connection-area context
   menu and action palette. Normal state survives restart; search state remains
   temporary. Collapse moves a hidden child selection to its visible root.
3. **Long SFTP download names:** short sibling `.serverdesk-UUID.part` files
   avoid extending an otherwise valid filename beyond the component limit.
4. **Changed-source protection:** upload size/write-time and download
   size/write-time/type checks run before replacing the destination. Detected
   changes preserve the old file and allow retry after the source stabilizes.

Includes the prior development improvements: one-process vault reads when an
unchanged owned profile proves the earlier account check, plus connection
indicators updated without rebuilding large trees or disturbing browser context.

## Install and upgrade

Use `ServerDesk-0.51.0-win-x64-Setup.exe` on Windows; close sessions first.
Setup supports per-user upgrades and preserves user data. Portable Windows and
Linux users extract/install a complete package. Keep a backup; database copies
do not include the OS vault or every plugin's external secret store. Old payloads
and tags are not overwritten. The installer has no Authenticode signature.

## Limits

Source checks compare metadata, not a snapshot or a content signature. Same-size
changes with unchanged timestamps, coarse server clocks and concurrent races
can escape detection. They do not implement transfer resume or automatic retry.
Existing path, symlink and overwrite protections remain active.

This is Public Beta, not completed 1.0 acceptance. Physical RDP/clipboard/input,
KDE/KWallet/Wayland, mixed DPI, Serial hardware, wider WAN/server compatibility,
multi-hour use and clean-VM installer registration retain their documented gaps.
[Verification](../reports/release-verification-0.51.0.md),
[known issues](../known-issues.md), [1.0 plan](../stable-release-plan.md).
