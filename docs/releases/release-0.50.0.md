# ServerDesk 0.50.0 Public Beta

Date: 2026-10-23. Local release; no GitHub publication in this task. Windows/Linux x64. ServerDesk code: MIT.

## Unlock the vault once

Configured Bitwarden / Vaultwarden accounts can prompt for a master password when ServerDesk starts. Successful unlock closes the startup dialog automatically; Skip lets you unlock later. The saved startup option is in Settings → Plugins. Fresh unconfigured installations do not prompt.

Closing a picker keeps the unlock in workspace memory. Subsequent password/SSH-key selections reuse it. Lock vault, sign-out, account/profile changes, disabling the plugin or closing ServerDesk clears it. The master password is not retained for future operations and no unlock/session key is written to ServerDesk settings, logs or the database. The official client's account cache remains governed by its existing behavior.

## Less waiting

Reopening a bound, unlocked picker launches no CLI commands. Secret retrieval still checks account identity before and after fetching that exact item. Recent search metadata can be reused for up to eight queries/30 seconds; no password or private key is cached there. Explicit searches and synchronization refresh results. The startup status is prepared in one pass; successful GUI login validates and reuses its returned session instead of repeating unlock. CLI executable verification and TLS checks remain enabled. Official-client startup and network latency can still cause delays.

## Connection list

The green check is now at the left edge of each server row, before its color stripe and icon. A reserved slot keeps labels and protocols fixed when connection state changes. Favorites remain before the right-aligned protocol.

No schema or dependency changes. Existing core SSH/SFTP sessions survive optional-plugin changes. Packages remain unsigned Public Beta; real vault-account and physical RDP/input/DPI acceptance limits remain.

[Vault setup and privacy](../bitwarden.md), [verification](../reports/release-verification-0.50.0.md), [known issues](../known-issues.md).
