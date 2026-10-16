# Artifact cleanup — 2026-10-16

Removed obsolete generated builds, acceptance harness copies, extracted source builds, temporary application data and intermediate screenshots/logs from `artifacts/`.

| Result | Value |
|---|---|
| Size before cleanup | 30.304 GiB |
| Size after cleanup | Approximately 2.449 GiB |
| Generated file bytes removed | 29,908,846,022 bytes (27.855 GiB) |
| Removed top-level entries | 1,290 |
| Frozen releases preserved | 13 versions, 130 files |
| Cleanup failures | 0 |

Remaining top-level entries are `releases/`, `reports/` and `README.md`. Selected final 0.31.1/repository-preparation logs, unit results and representative screenshots are retained in `artifacts/reports/`. Sources, local SDK/tools, Git history and the installed application were outside the cleanup scope. The installed executable referenced by the user's shortcuts still exists.

Before deletion, all 117 entries in the releases' SHA-256 manifests matched their files. An independent snapshot of all 130 release files, including the manifests, matched names, sizes and SHA-256 hashes after deletion. The entire generated directory remains ignored by Git.

The disposable cleanup test passed for preview behavior, unsafe-root rejection, release/report/explicit retention, read-only files and a junction target outside the selected cleanup root. A separate disposable WSL test confirmed that a broken Linux symbolic link can be removed without following its target. Both PowerShell scripts passed syntax checks. These are maintenance checks; application binaries were unchanged and were not rebuilt for this cleanup.

See [artifact retention and cleanup](../artifacts.md) for the reusable command and retention rules. Detailed local cleanup evidence is in `artifacts/reports/maintenance/`.
