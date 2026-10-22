# ServerDesk 0.47.0 — Public Beta

Date: 2026-10-22. License: MIT.

## SSH keys from Bitwarden / Vaultwarden

SSH connections can now use a native **SSH Key** vault item. Select **SSH key from vault** in the connection editor, enter the server username, bind an item and save. Unlock the vault when connecting. Search shows key names and fingerprints separately from Login passwords.

RSA 2048–8192 and Ed25519 keys work in supported OpenSSH/PEM/PKCS#8 formats without an additional key passphrase. The terminal, SFTP, transfers and monitoring authenticate from memory; ServerDesk does not export a private key to a temporary file, database or credential store. Reconnect fetches the item again. Public-key/fingerprint metadata is checked when present, and account changes or mismatched item types stop retrieval.

This release also includes the simpler two-step vault picker, keyboard navigation, labeled results and first-time setup guidance developed since 0.46.0. PL/EN Help explains key setup and lifecycle.

## Installation and compatibility

Use the Windows x64 Setup or a fresh portable folder. Linux x64 DEB/TAR packages are provided with the existing desktop validation limits. Keep a database backup before upgrading. The database schema remains 4 and there are no new dependencies, but older versions cannot use profiles with the new vault-key authentication method. Configuration export/import omits vault references; choose the item again when connecting. Profile duplication opens an unbound editor draft; bind a key before saving that copy.

Install the official native Bitwarden CLI and configure/sign in to its account first. The plugin does not edit or automatically synchronize the vault, retrieve TOTP/attachments/SSH certificates or handle separately encrypted private-key payloads. Import those keys into Bitwarden first. Managed/CLI memory cannot be guaranteed to have every transient copy zeroed. Live acceptance against a personal Bitwarden/Vaultwarden account remains open.

See [setup and privacy](../bitwarden.md), [verification](../reports/release-verification-0.47.0.md) and [known issues](../known-issues.md). Packages remain unsigned.
