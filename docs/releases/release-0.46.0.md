# ServerDesk 0.46.0 Public Beta

MIT. Local release prepared on 2026-10-22; this preparation does not publish a GitHub release.

- Add a built-in **Bitwarden / Vaultwarden** plugin. Configure an installed official CLI executable in Settings → Plugins; sign in to the CLI beforehand. The picker supports masked master-password unlock, bounded Login-item search and a one-time selected password for SSH/RDP/VNC.
- Bind a vault item to a saved profile. Store item/account/server metadata, fetch the current password after unlock, reject account/server changes and confirm different usernames. Database backups retain references; configuration export/import and profile duplication omit them. Changing the binding rotates the local credential identity to prevent old stored passwords reappearing.
- Keep master passwords and session keys out of process arguments, logs, clipboard and the profile database. Bound profiles use a session-only manual fallback. Closing a picker or disabling the plugin cancels/drains its owned processes without interrupting terminal/SFTP work. CLI setup, encrypted cache and explicit synchronization remain under the user's control.
- Fix SSH tab focus after mouse selection or Ctrl+Tab, including rapid switching and split panels. Commands Insert now shows and focuses the terminal after SFTP/monitoring, inserts without Enter and rejects a destination changed while waiting.
- Add PL/EN plugin settings, dialogs and an offline **Help → Bitwarden / Vaultwarden** article.

No schema migration or new package dependency. This remains a public beta: the Bitwarden tests use synthetic data/CLI fixtures; live cloud/Vaultwarden account compatibility requires acceptance on a separate test account. TOTP, attachments, vault editing and SSH Key retrieval are outside this version. Existing RDP physical-input/network/DPI and reverse-transfer limitations remain unchanged.

See [setup and data handling](../bitwarden.md), [verification](../reports/release-verification-0.46.0.md) and [known issues](../known-issues.md).
