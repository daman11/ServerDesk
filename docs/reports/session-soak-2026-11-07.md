# Controlled four-hour session soak — 2026-11-07

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

## Full run — PASS

The four-hour/100-cycle run started on 2026-11-07 at 11:42 UTC (12:42 Warsaw)
from clean commit e3c74a2a2981d37a924b05dd2278e8a4d8ac0895 and its frozen
matching harness. Eight sessions reached steady state at 11:42:49 UTC.
The steady-state endpoint was 15:42:49 UTC (16:42:49 Warsaw), with
**14,400.039 seconds** of observed workload. Cleanup and the coordinator completed
at 15:42:52 UTC. Both the child and coordinator confirmed PASS and exit code 0.
The result's 14,402.725 seconds include cleanup; the CSV steady-end row establishes
the actual four-hour workload separately.

The retained report is
artifacts/reports/session-soak-2026-11-07-windows-4h-01. Plan/source metadata,
result.json, coordinator.json, samples.jsonl and samples.csv were cross-checked.
The frozen application assembly also reports the expected source commit.

Completed work and checks:

- **100 reconnect/close/open cycles**, eight sessions in all 950 steady-state CSV
  samples, and 952 measurements including empty-workspace checkpoints. Controlled
  replacement transitions temporarily have seven sessions as declared.
- **20 SFTP round trips with matching SHA-256**, in addition to warmup. Each round
  trip uploads and downloads a 64 KiB synthetic file. Monitoring and four dock
  groups were exercised, with periodic floating/redocking.
- Maximum interval between UI heartbeat ticks: **1.438 seconds**, below the
  declared five-second limit. The heartbeat interval itself is one second;
  this is not a physical input-latency measurement.
- Maximum measurement gap: **17.663 seconds**, below the 60-second limit. CSV
  observations are monotonic; periodic observations never forced GC.
- **Zero retained retired panes and zero retained lifetime sources**, covering
  the sources replaced by reconnect and close. Both transfer slots were returned.
  The final sample has zero connected sessions.
- No completion or retention threshold failed. stderr and launcher stderr are
  empty; the isolated application's diagnostic log contains no ERR/FTL entries.
- The coordinator restored its process-scoped Windows execution-state request.
  The fixture-stop record confirms the owned SSH listener was stopped.

Warmed empty workspace → final post-cleanup checkpoint:

| Metric | Baseline | Final | Change / declared budget |
|---|---:|---:|---|
| Managed memory | 13.54 MiB | 13.33 MiB | decreased; +32 MiB budget |
| Private memory | 102.18 MiB | 109.73 MiB | +7.54 MiB; +128 MiB budget |
| Working set | 188.19 MiB | 203.73 MiB | +15.54 MiB; informational |
| Handles | 532 | 519 | -13; +64 budget |
| Threads | 30 | 23 | -7; +16 budget |

Steady-state peaks were 72.95 MiB managed, 135.16 MiB private and 229.18 MiB
resident memory, 707 handles and 39 threads. These include the eight active
sessions; the retention verdict compares equivalent empty-workspace checkpoints.
The modest private-memory high-water retention stays within the predefined
budget and is not treated as evidence of unbounded growth.

Time-weighted process CPU was approximately **0.26%**, normalized across the
reported 22 logical processors. This measures the client/harness and headless
renderer; it excludes the WSL SSH server, coordinator and other applications.
Short-interval CPU peaks do not establish a sustained idle-CPU requirement.

The controlled Windows protocol/UI lifecycle soak therefore passes its declared
scope. This does not establish absence of every possible leak or complete
stability on other workloads. No application change, build, VM run or release
was performed while analysing these results; only this report is updated.

The Windows process uses Avalonia headless/Skia with real OpenSSH in WSL. Native
desktop, physical input/DPI and native Linux long-run acceptance remain separate.
The experiment measures the harness process too, so threshold crossings require
analysis rather than an unsupported claim of a specific product leak.
