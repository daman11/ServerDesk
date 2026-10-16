# SSH trust management — 2026-10-16

Development changes after 0.31.1. Frozen release packages are unchanged.

## Behavior

Settings now includes a Security category with a trusted SSH keys manager. It lists existing host/port/SHA-256 fingerprints, including entries without saved profiles, and supports search, refresh, fingerprint copying and confirmed removal of one selected entry. IPv6 endpoints include brackets. Full fingerprints remain visible in a selectable detail field; list rows can truncate them.

Removal matches the host, port **and reviewed fingerprint** in a parameterized DELETE. If another window has trusted a different key in the meantime, the stale operation removes nothing and refreshes the list. No bulk reset or automatic replacement is provided. The existing database schema and host-key verification remain in use; there are no new dependencies.

Changed keys remain rejected before authentication, without reaching the first-use confirmation. The error points to Settings → Security → Trusted SSH keys. After explicitly removing trust, a new connection again requires fingerprint verification. Removing stored trust does not terminate established connections. The action takes effect immediately, independently of saving/cancelling settings; this is explained in the Security page.

Database work runs off the dispatcher, with three-second command timeouts. Controls disable during an operation and recover after a database error. Closing cancels and awaits the active manager operation before disposing its lifetime. Profile/credential/history data is not removed, and host fingerprints remain excluded from profile exports.

## Verification

- Release solution build and Windows harness: passed, zero warnings/errors. Linux x64 self-contained harness publish: passed.
- Unit tests: **503 passed, 0 failed, 0 skipped**. Seven new cases cover listing/scoping, compare-and-delete, stale replacement, cancellation, profile/history/settings isolation, export exclusion and endpoint formatting.
- Windows 11 and Ubuntu/WSL Linux x64 headless UI: settings entry/search, host/port/fingerprint filtering, exact clipboard text, cancelled confirmation, busy database error/recovery, confirmed single-port removal, stale fingerprint protection and immediate apply followed by settings cancellation. Empty/populated states, PL/EN, Graphite/Light, font16 and 520/800 px manager widths passed. Windows renders were visually inspected.
- Actual isolated OpenSSH on loopback port 22222, from both Windows and Linux: seeded wrong fingerprint blocks SSH and SFTP without a first-use prompt; declined first-use leaves no trusted entry; accepted actual fingerprint is stored; removal through the manager leaves an established SSH printf channel usable; subsequent SFTP requires fresh confirmation; SSH and SFTP then share the accepted fingerprint. No server key or system account was modified. The fixture was stopped after testing.
- Existing Windows settings-layout, restart-design and localization acceptance passed with the fifth category, including saved/cancelled preferences, restart semantics and compact layouts.
- The offline manager scenario is included in the Windows/Linux CI matrix. Network acceptance remains opt-in against an explicitly owned fixture.

These tests use disposable databases, headless clipboard/UI events and a local OpenSSH server. They do not validate every physical input/display, SSH implementation or remote trust rotation policy. The manager does not prove server identity; administrators must compare fingerprints through an independent trusted channel.

Local evidence is under `artifacts/reports/mvp-trusted-hosts/`; generated databases, binaries and logs remain ignored by Git.
