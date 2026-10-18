# Saved profile deletion — 2026-10-18

## Confirmed cause and path

The frozen `v0.40.0` source and the development baseline both call `CredentialStore.DeleteAsync` before `ProfileRepository.DeleteAsync`. On Windows that calls `File.Delete(Path.Combine(paths.Data, "credentials", id + ".bin"))`. The directory is created only when a password is saved. A profile with no remembered password can therefore have no `credentials` directory. `File.Delete` tolerates a missing file but throws `DirectoryNotFoundException` for a missing parent on Windows. The UI caught only `InvalidOperationException` from an unavailable Secret Service, so this filesystem exception escaped before SQLite's DELETE was reached. The old generic error then incorrectly suggested connection/network troubleshooting.

Default affected location on this machine: `C:\Users\Damian\AppData\Roaming\ServerDesk\credentials\<profile-GUID>.bin`. `SERVERDESK_DATA_HOME` can replace the data root. The VNC-specific identity uses the same directory and its own derived GUID. The old logger recorded only exception types, so the historical profile GUID cannot be recovered from that message alone; the failure was reproduced with isolated fixtures, not by deleting a maintainer connection.

Before the fix, two of six new credential-deletion tests failed with the exact stack `System.IO.FileSystem.DeleteFile → CredentialStore.DeleteAsync`, for both a never-created and a previously removed credential directory. The captured `before-fix.trx` contains the concrete disposable paths and stacks.

## Audit of the deletion path

| Data | Behavior |
|---|---|
| Saved profile/configuration | Parameterized `DELETE FROM profiles WHERE id=$id` in `serverdesk.db`; SQLite autocommit completes before UI reload. There is no separate profile configuration directory to recursively remove. |
| Notes/description, tags, custom artwork | Stored in the selected profile JSON. Other profile rows remain byte-for-byte unchanged. No external icon, private-key or document path is followed for cleanup. |
| History | Retained in the history table as audit snapshots. The LEFT JOIN produces no reconnectable profile after deletion. Recording a late live-session event only UPDATEs an existing profile; it cannot INSERT a deleted one. |
| Workspace/session state | Restoration and queued snapshot saves filter identities against current database profiles. Stale cards do not recreate saved profiles. Live sessions are not torn down by deleting their saved definition. |
| Folders, icons, settings, plugins | Remain unchanged by profile deletion. Plugin library/host preferences do not need to be decrypted or available for profile removal. |
| Exports, logs, keys, remote files, local session folders | No cleanup/delete operation is performed on these files or directories. Missing optional paths cannot block profile deletion. |
| Credentials on Windows | Delete only the selected profile's two existing credential identities, using GUID filenames and `Path.Combine`. Missing file/parent is success; other IO/access failures remain errors. No directory is created just to forget a password. |
| Credentials on Linux | Continue using `secret-tool clear application ServerDesk profile <GUID>`; no Windows credential path or shell-built command is introduced. An absent entry is already success; an unavailable backend still requires explicit consent to delete only the profile. Inner backend exceptions now survive wrapping. |

## Fix and logging

The missing-path handling stays inside the Windows credential deletion adapter and under its existing semaphore. It does not use an `Exists` check vulnerable to check/delete races and does not suppress database, permission or locked-file errors.

The UI separates credential cleanup, durable database deletion and list refresh. A refresh failure after DELETE is reported as an already committed deletion, rather than as an unsaved operation. Filesystem/database guidance no longer suggests SSH/RDP connection fixes. Existing application validation/missing-tool guidance is preserved; SSH and network exceptions have separate messages.

Local credential-delete failures log the exception with the exact file path and full stack. Database-delete failures add the database path and profile GUID. General and background failures record exception types, HResults, SQLite codes and complete outer/inner stacks without copying arbitrary remote replies, exception Data or secret-bearing messages. No credential value is logged. Log location remains the normal data-root `logs/serverdesk-*.log`, with the existing retention policy.

No database migration, credential format/encryption change, protocol change or release/package rebuild is introduced. This is a focused correction to missing-file deletion and failure reporting.

## Validation and limits

- Red reproduction: 2 expected failures / 6 tests before the fix, specifically the missing-directory cases.
- Green: 59 filtered tests passed: `CredentialDeletionTests`, `ProfileDeletionTests`, `OperationErrorTests`, `AtomicCredentialTests`, `RepositoryTests`, `ConnectionHistoryTests`, `WorkspacePersistenceTests`, `LocalizationTests`.
- `--profile-deletion` UI checks: SSH/PL and RDP/EN, confirmation/cancel, missing credential directory, preservation of another profile/notes/history/file, fresh-process **read-only** database verification, reopening the workspace, real Windows file-lock failure, a SQLite rejection trigger, logged paths/stacks, retry, a deliberately failed post-commit list refresh, and unavailable-vault consent/cancel. Fixtures use documentation IPs and never connect to a server.
- Incremental Release application/harness build; repository/link, pinned-asset and whitespace checks.

Evidence is under `artifacts/reports/profile-deletion-2026-10-18/`. Tests use new temporary/workspace data only. The maintainer's database and credential files were not opened or changed by these fixtures. Native Linux/Secret Service desktop acceptance and a full application matrix were not run; cross-platform repository tests and the unavailable-backend process check run here on Windows. Windows-only credential tests exercised actual DPAPI files in the fixture directories.

## Changed files

- [CredentialStore.cs](../../src/ServerDesk.Infrastructure/CredentialStore.cs): idempotent missing-path handling.
- [SecretServiceCredentialStore.cs](../../src/ServerDesk.Infrastructure/SecretServiceCredentialStore.cs): preserve deletion failure causes.
- [ProfileRepository.cs](../../src/ServerDesk.Infrastructure/ProfileRepository.cs): expose the database path for diagnostics; SQL/schema behavior unchanged.
- [OperationErrorMessages.cs](../../src/ServerDesk.Infrastructure/OperationErrorMessages.cs), [ExceptionDiagnostics.cs](../../src/ServerDesk.Infrastructure/ExceptionDiagnostics.cs): error categories and safe detailed traces.
- [MainWindow.cs](../../src/ServerDesk.App/MainWindow.cs), [ProfileDeletion.cs](../../src/ServerDesk.App/ProfileDeletion.cs): staged deletion, persistence/refresh distinction, contextual logging.
- [pl.json](../../src/ServerDesk.Core/Localization/pl.json), [en.json](../../src/ServerDesk.Core/Localization/en.json): localized guidance.
- [CredentialDeletionTests.cs](../../tests/ServerDesk.Tests/CredentialDeletionTests.cs), [ProfileDeletionTests.cs](../../tests/ServerDesk.Tests/ProfileDeletionTests.cs), [OperationErrorTests.cs](../../tests/ServerDesk.Tests/OperationErrorTests.cs): regression tests.
- [ProfileDeletionChecks.cs](../../tools/ServerDesk.UiSmoke/ProfileDeletionChecks.cs), [Program.cs](../../tools/ServerDesk.UiSmoke/Program.cs): isolated UI/readback scenarios.
- [CHANGELOG.md](../../CHANGELOG.md), [user-manual.md](../user-manual.md), [reports index](README.md) and this report.
