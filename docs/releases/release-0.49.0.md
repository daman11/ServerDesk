# ServerDesk 0.49.0 Public Beta

Date: 2026-10-23. Local release; no remote publication in this task.

## Vault setup in the application

Enable Bitwarden / Vaultwarden in Settings → Plugins. Choose US, EU or a custom HTTPS server, enter an optional account email and select Save and connect. Masked email/master-password sign-in and separate verification-code dialogs open after saving. Use Sync vault or Sign out of vault account in the vault dialog. Existing passwords, SSH keys and saved bindings remain supported.

Complete Windows/Linux x64 packages include official Bitwarden CLI OSS 2026.10.0. No manual installation or executable selection is required. Each server/email has a separate owned profile. External clients remain an advanced compatibility option; their accounts are never reconfigured or signed out.

The component is a separate GPL v3 process; ServerDesk code remains MIT. Packages include exact upstream notices and corresponding OSS source with commercial modules excluded. Archive/source/binary hashes are pinned and checked during preparation; runtime checks the executable before every invocation.

## Data and limits

Master passwords/sessions use child environment variables; verification codes use stdin. ServerDesk does not put them in settings, logs, clipboard or arguments. The official client keeps account tokens/encrypted cache in its own profile. Closing the dialog forgets the ServerDesk unlock; explicit Sign out clears owned login. Factory reset preserves cache for recovery and is not a secure wipe. Changing email/server selects another profile.

HTTPS with trusted TLS is required. Email/password with authenticator/email/YubiKey OTP is supported; SSO/FIDO2 and primary personal API-key sign-in are outside this GUI. Public Bitwarden configuration may be fetched on first initialization. Legacy external clients retain their previous workflow.

No database schema or NuGet changes. Linux additionally declares libstdc++6/libgcc-s1. The component increases package size. Source-only development outputs need the preparation script.

The report records synthetic account/GUI and real bundled-client loopback TLS tests. No real account was used; successful personal vault acceptance remains open. Existing physical RDP/IME/mouse/DPI, reverse transfer and clean-VM limits remain. Packages are unsigned Public Beta.

[Setup and privacy](../bitwarden.md), [verification](../reports/release-verification-0.49.0.md), [known issues](../known-issues.md).
