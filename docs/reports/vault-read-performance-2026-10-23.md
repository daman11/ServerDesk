# Vault read performance — 2026-10-23

This is a focused development change after 0.50.0. It does not change unlock,
password or SSH-key lifetimes, add a listener, cache vault secrets or change dependencies.

## Cause and change

Each password/key retrieval previously started `status`, `get item <UUID>` and
`status` as separate CLI processes, each with pinned executable verification.
The bundled client now reuses an account verification only while a fresh local
store digest proves that the previously verified profile is unchanged. Reads
still fetch the exact item through the CLI. External/shared profiles retain both
status commands. Missing, oversized, unreadable, symlinked or unsupported stores
fall back to the existing path. A portable `bw-data` override also disables reuse.

The pinned CLI stores its durable account, server and encrypted vault state in
`data.json`. Auto-unlock rewraps the auto key even during reads. The digest covers
all file bytes, replacing only that exact account's wrapped key value with its
authenticated 64-byte value. HMAC is checked before AES-CBC decryption; temporary
buffers are cleared. Removing/changing the key, changing any other byte, or
invalid authentication cannot reuse the earlier proof. Proofs are established
only across a stable, successful unlocked status and cleared on lock/disposal.

Format references: pinned clients commit
[`b449ac1`](https://github.com/bitwarden/clients/tree/b449ac1d255784380d75ef026343198891286be7),
`LowdbStorageService`, `NodeEnvSecureStorageService`, `DefaultStateService`,
`DefaultUnlockService`; [SDK binary layout](https://sdk-api-docs.bitwarden.com/src/bitwarden_crypto/enc_string/symmetric.rs.html),
[authenticated encryption](https://bitwarden.com/help/what-encryption-is-used/).
Unknown/new key formats deliberately use normal CLI checks.

## Evidence and limits

- Focused Bitwarden unit tests (81 passed, no failures/skips) include repeated independent reads, same-size and
  same-timestamp changes, account/server switching before/during a read, logout,
  status races, rewrapped/changed/tampered keys, missing/oversized stores,
  unsupported formats, cancellation, manual lock and lock contention.
- PL/EN workspace UI exercises one-command bound reads, startup/unlock reuse,
  manual lock, cancellation, plugin disable and account-setting changes.
- On this Windows host, six warm starts of the pinned OSS CLI 2026.10.0 averaged
  **3144.4 ms** each (3025.9–3261.2 ms). The two avoided process starts account for
  about **6288.7 ms**. This measures startup overhead with `status` in a fresh,
  unauthenticated synthetic profile, not a real vault's end-to-end password time.
- The authenticated read/account-transition tests use synthetic fixtures. No
  maintainer vault, saved production connection or personal password was used.
  Real Vaultwarden latency remains a maintainer acceptance check. The remaining
  CLI startup, vault size, disk and network can still affect response time.

Local evidence is in `artifacts/reports/vault-performance/`. No full release or
unrelated application test matrix is required for this bounded optimization.
