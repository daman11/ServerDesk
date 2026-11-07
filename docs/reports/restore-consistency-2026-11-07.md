# Database restoration and protected content — 2026-11-07

Development change after 0.51.0. This is focused recovery validation and error
guidance, without changing the database schema, credential lifetime, storage
format or release version. No VM, remote server or user credential store is used.

## Change

Commands and Network Tools now explain an unavailable protected payload with
unlock/retry guidance and the limitation of SQLite-only backups. Commands keeps
the built-in library available and custom-library writes disabled while its
protected content cannot be loaded. The Network Tools error formatter preserves
the actionable collection message instead of reporting a general network error.
The existing warning logging in the Commands panel retains the exception type
and stack; no protected content is included in the new messages.

A null credential result is not proof of deletion: the existing Secret Service
adapter also returns null for a missing executable, some failures and an internal
timeout. The new guidance therefore does not assert that content was deleted.
Explicit credential-read exceptions retain their existing cause and handling.
The adapter's contract is unchanged.

## Focused evidence

- 33 selected Windows unit tests pass, including 12 new restoration cases and
  existing Commands, collection atomicity and factory-reset checks.
- Restored profiles, folders, notes, connection history, command host preferences
  and favorites, network history and collections recover from the snapshot with
  the matching disposable protected store.
- SSH/RDP/VNC profiles restored after a target edit retain the old credential ID.
  A missing old entry does not select the new target's credential, a legacy ID
  or a VNC alias. This tests reference resolution without a live login.
- A password replaced under the same credential ID remains the newer password
  after restoring SQLite. The database does not contain password versions.
- Saving a newer command library retires its old protected chunks. Restoring
  the old SQLite pointer reports unavailable content and leaves both that
  pointer and the current library untouched. Restoring the matching fixture
  chunks allows the same reader to retry successfully. A missing later chunk
  never becomes a partial/empty library.
- A deleted collection reappears as metadata in the old snapshot but cannot
  load its deleted payload. Other collections remain readable; saving a new
  collection retains the unavailable entry instead of truncating the index.
- An explicitly failing vault read preserves the causal exception and all
  entries/references; unlocking the fixture permits retry. A nonexistent
  Secret Service executable exercises the adapter's null result without
  contacting an OS vault, verifying conditional unlock/recovery guidance.
- Focused headless UI checks pass in Polish and English: the restored Commands
  panel shows the recovery explanation, keeps the built-in list visible,
  disables duplication into the unavailable library and retains its pointer.

Evidence is local in `artifacts/reports/restore-consistency-2026-11-07`:
`restore-consistency-final.trx`, PL/EN UI screenshots and diagnostics under `ui-final`.
Builds complete without warnings or errors. Repository/link and diff checks pass.

## Limits and remaining work

The protected store is a controlled in-memory fixture; the unavailable-tool test
starts no executable. This does not certify actual DPAPI recovery, Linux
Secret Service/KWallet unlock dialogs, migration between accounts/computers,
hardware failures or sudden power loss. SQLite snapshots still exclude secrets
and protected plugin contents. Full portable secret backup remains separate work.

The application is not certified ready for stable 1.0 by these checks. Remaining
native desktop, physical input, DPI, transport and platform acceptance is listed
in the [stabilization plan](../stable-release-plan.md).
