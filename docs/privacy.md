# Local data and privacy / Dane lokalne i prywatność

The reviewed ServerDesk 0.43.0 source does not implement analytics, telemetry, a cloud account, automatic update checks or crash-report uploads. Network connections are made for the protocols and servers selected by the user. This statement covers ServerDesk; operating-system components and external clients have their own behavior. [Security tests and scope, 2026-10-19](reports/security-pentest-2026-10-19.md).

The local database stores server addresses, usernames, folders, preferences, trusted SSH host fingerprints scoped to host and port, workspace state and connection history. Trust entries can outlive a saved profile and are excluded from profile exports. Version 0.33.0 provides Settings → Security → Trusted SSH keys to inspect or remove an individual entry. Custom artwork and diagnostic logs are also stored locally. Logs do not intentionally record passwords or terminal contents, but can contain connection identifiers and errors; review and redact before sharing.

Development after 0.41.0: **Settings → Security → Logs and diagnostics → Open logs** provides a local, read-only viewer. Search is limited to the preview (up to the final 128 KiB); **Save log copy** saves the full selected file as it was when export began. It does not upload, redact or delete logs. The export destination must be outside the live log directory. Viewing or exporting does not save other settings drafts. [Verification](reports/application-logs-2026-10-18.md).

Development after 0.43.0: application error diagnostics include an operation name, random error ID, exception types/codes and outer/inner stack traces, excluding exception `Message` and `Data`. A diagnostic is limited to 64 KiB and 16 exceptions; oversized traces are marked as truncated. Startup logs add the application build, OS and runtime; each entry includes the local process ID. Files roll daily or after reaching 4 MiB, retaining 14 files (not necessarily 14 days; a single entry can cross the size threshold). Fatal/unobserved-task diagnostics remain local and are not crash uploads. Existing infrastructure identifiers, local stack paths and explicit local-file failure details can still be sensitive. [Verification and limits](reports/error-logging-2026-10-19.md).

Network Tools additionally stores bounded operation history, target addresses, custom resolvers, OID/WoL favorites and collection names. HTTP history omits path/query/body/headers/credentials and keeps only the origin and safe parameters. Full collections use the OS credential store, including any request bodies or headers, with no plaintext fallback. Explicit result exports can contain sensitive data supplied by remote services; inspect them before sharing. HTTP masks known secrets and common credential fields. [Network Tools privacy and limits](network-tools.md).

Passwords are stored only when requested: DPAPI-protected files on Windows, or the system Secret Service on Linux. Private-key files remain at the paths chosen by the user. Profile exports omit passwords and key contents, but still contain sensitive infrastructure metadata.

This encryption protects stored data; it does not isolate secrets from malware running as the same logged-in user, an unlocked keyring, or a debugger attached to the application. Active sessions retain credentials in process memory for authentication/reconnect. Managed strings and third-party clients do not provide guaranteed memory erasure. DPAPI uses the current Windows user scope; it is not an application-specific master-password vault. Secret Service access also depends on the keyring provider and session policy.

Development after 0.43.0 fixes HTTP result masking of reflected Basic Auth values, form credentials, short secrets and escaped JSON values. Frozen 0.43.0 packages do not include that fix. Masking is best effort: arbitrary encodings, unknown secrets, filenames, profile descriptions, remote results and explicitly exported command templates can still contain sensitive information. Inspect exports before sharing; the application does not classify every piece of personal or infrastructure data.

Terminal cursor/history preferences and the optional local SFTP start directory are saved in the local preferences database. Choosing a history limit does not save terminal contents to disk; the scrollback buffer remains in memory. SFTP defaults and visited-directory history are excluded from server-profile exports.

Commands additionally stores encrypted user templates/categories through the OS credential store, with no plaintext fallback. SQLite holds opaque vault-part references, favorite IDs, manual/automatic/pinned profile choices per host and optional recent command IDs/timestamps (history is disabled by default). It does not store rendered commands, entered parameter values or output. Secret parameters have no saved defaults and are copy-only, with a clipboard warning. Command JSON export is an explicitly approved plaintext exchange file containing user templates/categories, without entered values, history or host bindings. Automatic SSH discovery uses bounded read-only exec channels on an already authenticated connection, with an in-memory per-connection cache; RDP/VNC/Serial do not trigger shell discovery. [Commands privacy and limits](commands.md).

Default locations:

- Windows: `%APPDATA%\ServerDesk`.
- Linux data: `${XDG_DATA_HOME:-~/.local/share}/serverdesk`.
- Linux configuration: `${XDG_CONFIG_HOME:-~/.config}/serverdesk`.
- Linux remembered passwords: the system Secret Service, separately from these directories.

The installer/uninstaller preserves user data. To erase it, close ServerDesk, remove the data/config directories yourself and remove its remembered entries from the system credential store. Keep required backups first. `SERVERDESK_DATA_HOME` overrides the directories for isolated tests or a controlled portable setup; it does not make Windows DPAPI passwords transferable to another user/machine.

The full factory reset creates and preserves database snapshots. Protected vault entries and diagnostic logs are also retained for recovery; a reset is not a secure wipe. Disabling an optional plugin preserves its data. To remove all traces, account for backups, logs, DPAPI files/Secret Service entries and any files you explicitly exported or downloaded; disk/backup retention is outside the application reset.

PL: ServerDesk nie wysyła telemetrii ani raportów awarii. Baza, historia, grafiki i logi pozostają lokalne. Eksport JSON nie zawiera haseł ani treści kluczy, ale ujawnia adresy i nazwy użytkowników. Deinstalacja zachowuje dane. Przed udostępnieniem logów lub eksportu usuń informacje o swojej infrastrukturze.
