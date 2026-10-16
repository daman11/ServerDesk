# Local data and privacy / Dane lokalne i prywatność

ServerDesk 0.31.1 does not implement analytics, telemetry, a cloud account, automatic update checks or crash-report uploads. Network connections are made for the protocols and servers selected by the user. This statement covers ServerDesk; operating-system components and external clients have their own behavior.

The local database stores server addresses, usernames, folders, preferences, trusted SSH host fingerprints scoped to host and port, workspace state and connection history. Trust entries can outlive a saved profile and are excluded from profile exports. Development builds after 0.31.1 provide Settings → Security → Trusted SSH keys to inspect or remove an individual entry. Custom artwork and diagnostic logs are also stored locally. Logs do not intentionally record passwords or terminal contents, but can contain connection identifiers and errors; review and redact before sharing.

Network Tools additionally stores bounded operation history, target addresses, custom resolvers, OID/WoL favorites and collection names. HTTP history omits path/query/body/headers/credentials and keeps only the origin and safe parameters. Full collections use the OS credential store, including any request bodies or headers, with no plaintext fallback. Explicit result exports can contain sensitive data supplied by remote services; inspect them before sharing. HTTP masks known secrets and common credential fields. [Network Tools privacy and limits](network-tools.md).

Passwords are stored only when requested: DPAPI-protected files on Windows, or the system Secret Service on Linux. Private-key files remain at the paths chosen by the user. Profile exports omit passwords and key contents, but still contain sensitive infrastructure metadata.

Default locations:

- Windows: `%APPDATA%\ServerDesk`.
- Linux data: `${XDG_DATA_HOME:-~/.local/share}/serverdesk`.
- Linux configuration: `${XDG_CONFIG_HOME:-~/.config}/serverdesk`.
- Linux remembered passwords: the system Secret Service, separately from these directories.

The installer/uninstaller preserves user data. To erase it, close ServerDesk, remove the data/config directories yourself and remove its remembered entries from the system credential store. Keep required backups first. `SERVERDESK_DATA_HOME` overrides the directories for isolated tests or a controlled portable setup; it does not make Windows DPAPI passwords transferable to another user/machine.

PL: ServerDesk nie wysyła telemetrii ani raportów awarii. Baza, historia, grafiki i logi pozostają lokalne. Eksport JSON nie zawiera haseł ani treści kluczy, ale ujawnia adresy i nazwy użytkowników. Deinstalacja zachowuje dane. Przed udostępnieniem logów lub eksportu usuń informacje o swojej infrastrukturze.
