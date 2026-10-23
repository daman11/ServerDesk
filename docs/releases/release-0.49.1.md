# ServerDesk 0.49.1 Public Beta

Date: 2026-10-23. Windows/Linux x64. ServerDesk code: MIT.

## Changes

- Connection rows keep SSH/RDP and other protocol labels at the right edge. The green connected check now sits beside the server icon in a reserved slot. Connection state and favorites no longer shift protocol labels; addresses and tags retain their alignment.
- Includes the previously local 0.47–0.49 improvements: SSH keys from Bitwarden / Vaultwarden, 14 offline command guides and 22 tool references with 168 option/syntax descriptions, on-demand official documentation refresh, and the bundled official Bitwarden CLI OSS with GUI server/account setup, sign-in, verification codes, sync and sign-out.
- Full packages include the separate Bitwarden OSS client, GPL notices and corresponding OSS source. No executable selection is needed. Existing vault bindings and advanced external clients remain supported.

## Installation and updates

Use the Windows Setup EXE for per-user installation and in-app updates. The installer offers an optional desktop shortcut. Portable users should extract the complete ZIP into a new directory. Linux packages are DEB and TAR.GZ; updates remain manual. Preserve your data/backup before upgrading. Database schema is unchanged from 0.49.0.

## Verification and limits

The connection-row change passed 64 local layout cases before this release. At the maintainer's request, this publication repeats secret scanning and package/source/version/checksum checks, without rerunning the full functional matrix, installer workflows, VM or hardware tests. Earlier 0.47–0.49 reports describe their own tested scope. This remains an unsigned Public Beta; existing RDP, physical-input, desktop and real vault-account acceptance limits remain.

Local Codex attachments and publication authentication are excluded from Git and release exports. No saved user connections, vault data or local logs are included.

[Verification](../reports/release-verification-0.49.1.md), [vault setup/privacy](../bitwarden.md), [Commands](../commands.md), [known issues](../known-issues.md).
