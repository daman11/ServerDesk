# ServerDesk 0.50.0 verification

Date: 2026-10-23. Public Beta. Scope: leading connection status, a workspace-owned vault unlock, startup preference and fewer redundant CLI commands. No database schema, package-lock or upstream component changes.

## Regression scope

- Unit regressions cover metadata-only search reuse, explicit refresh, sync/lock invalidation, account changes before cache access, logout invalidation, one-pass prepared status, verified GUI login tokens and response-buffer clearing. Existing account/server/key/credential/persistence guards remain applicable.
- Synthetic PL/EN workspace checks cover startup unlock/automatic close, Skip and opt-out, reuse across pickers, zero-command bound-picker reopening, one search across recent reopenings, exact-ID fetching with status/get/status, manual lock, configuration change, queued dialog serialization, cancellation, disable/re-enable and no core credential access. Fixtures never use the maintainer's saved accounts.
- Existing native synthetic CLI tests retain argument/environment redaction, bounded output, cancellation, password and SSH-key UI, manual fallback and username confirmation. Managed-account tests cover settings save-before-connect, masked login/2FA, explicit sync/sign-out and plugin drain. The new workspace checks are included in that existing CI scenario.
- Connection-row geometry checks cover 64 SSH/RDP rows, connected/disconnected and favorite states, narrow/wide widths, font sizes 13/16, address details and Dark/Light. Status occupies the leading slot; labels/protocol origins remain fixed.
- Applicable unit, headless UI, package, installer and source/secret checks are recorded with final counts and the exact source commit in the frozen VERIFICATION.md. Previous 0.49.1 packages/tags remain untouched. This release is local only.

## Security and performance boundaries

The in-memory unlock deliberately lasts longer than one dialog, as requested. There is no master-password cache or persistent session key. Changing only the startup-prompt checkbox leaves the current unlock intact; changing server/email/executable/profile drains and disposes it. Closing a picker cancels its pending work, and plugin/application teardown waits for owned work before disposing the shared client. Manual Lock vault clears the ServerDesk key without globally locking or signing out an external shared CLI.

Reopening an unlocked bound picker performs zero CLI operations, and a recent automatic search can avoid a list call. These are deterministic command-count improvements on fixtures, not a real-server latency benchmark. Secret reads preserve both live account checks and exact-ID/type validation. Search caches hold at most eight metadata result lists for 30 seconds, remain account-checked, are bypassed by explicit search and cleared on lock/sync. Passwords/private keys are always fetched again. Pinned binary SHA-256 validation still runs before every managed CLI invocation.

Login reuses the CLI's raw session only after validating its format and independently verifying account UUID/server and unlocked status. A malformed token, wrong account/server or cancellation cannot retain it. No raw output, password, token or verification code is logged or persisted by ServerDesk. Same-user hostile processes and immutable managed-string copies remain outside guaranteed erasure/isolation.

No production account, VMware machine or saved connection is used for these checks. Successful personal Bitwarden/Vaultwarden account acceptance, remote RDP/clipboard/input/DPI tests and clean physical desktops retain their previous limits. Packages are unsigned. No remote publication is authorized by this implementation request.

[Vault setup/privacy](../bitwarden.md), [release notes](../releases/release-0.50.0.md), [prior release](release-verification-0.49.1.md).
