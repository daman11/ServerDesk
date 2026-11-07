# Controlled session soak preparation — 2026-11-07

Scope: tools and verification, without an application release or production
session changes. [Procedure and declared budgets](../session-soak.md).

Fourteen focused unit tests pass for scheduling, normalized CPU, retention
budgets, missing work, invalid observations and UI/observation gaps.

The new opt-in workload maintains eight real SSH sessions, distributes complete
reconnect/close/open cycles across the requested interval, verifies SFTP bytes,
exercises monitoring and docking, and records process resources. Periodic samples
do not force GC; retained resources are compared after equivalent cleanup.
The coordinator pins the owned loopback fixture host key, isolates all data,
prevents automatic Windows system sleep only for its own lifetime and stops its
owned service when the child ends.

## Calibration

The initial calibration found two harness issues: monitoring was opened on an
inactive pane, and PowerShell cleanup used an invalid null-conditional expression.
Both were corrected; no application behavior was changed for those failures.

The second calibration passed with eight sessions, three complete cycles,
three steady-state SFTP round trips plus warmup, 15 samples and 60 seconds of
steady workload. Maximum observation gap was 8.44 seconds. No retired pane or
connection lifetime remained; both transfer slots were available after cleanup.

Warmed empty-workspace baseline → final cleanup:

| Metric | Baseline | Final |
|---|---:|---:|
| Managed bytes | 14,201,144 | 6,345,024 |
| Private bytes | 107,487,232 | 92,987,392 |
| Handles | 536 | 526 |
| Threads | 32 | 27 |

These are calibration results, not four-hour acceptance. Raw evidence is retained
under artifacts/reports/session-soak-2026-11-07-calibration-02.

The final 30-second/two-cycle calibration also passed after adding a one-second
UI heartbeat and checking both pre-reconnect and pre-close lifetime sources.
Maximum UI heartbeat gap was 1.42 seconds; retired controls/lifetimes were zero.
Its coordinator exited successfully, restored the Windows execution-state
request and stopped the fixture.

Cancellation was explicitly requested with eight active sessions. Both child
and coordinator recorded CANCELLED, drained the owned sessions, returned both
transfer slots, restored execution state and stopped the fixture. This is not
counted as a successful duration run.

## Full run

The four-hour/100-cycle run is pending. It must be launched from the clean
committed source and a matching stopped harness, and its eventual result must
come from result.json plus the coordinator exit result. RUNNING is not PASS.
Do not mark the complete stability stage closed on the basis of calibration.

The planned retained report is
artifacts/reports/session-soak-2026-11-07-windows-4h-01. Its eventual status must be
read from the files, not inferred from this preparation report.

The Windows process uses Avalonia headless/Skia with real OpenSSH in WSL. Native
desktop, physical input/DPI and native Linux long-run acceptance remain separate.
The experiment measures the harness process too, so threshold crossings require
analysis rather than an unsupported claim of a specific product leak.
