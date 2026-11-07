# Controlled SSH/SFTP session soak

This opt-in verification uses eight ServerDesk session panes and real disposable
OpenSSH connections. It is separate from physical keyboard/display acceptance.
No personal database, saved credential or production endpoint is used.

## Workload and evidence

- Default: 14,400 steady-state seconds, eight sessions and 100 evenly distributed
  cycles. Each cycle reconnects a session, closes/removes it and opens a new pane.
  Controlled transitions temporarily have seven sessions.
- Exercise all four dock groups; periodically float/redock, upload/download a
  64 KiB fixture and verify SHA-256, and open Linux monitoring.
- Prime bounded 1000-line terminal buffers before measurement. A separate warmup
  opens/closes eight sessions and exercises the same features before capturing
  an empty-workspace baseline.
- Sample raw managed/private/resident memory, CPU normalized by logical processor
  count, handles and threads. Regular samples do not force GC. GC is explicit
  only for comparable baseline/steady-start/post-cleanup checkpoints.
- After closing sessions, compare with the warmed empty workspace. Investigation
  budgets are +32 MiB managed, +128 MiB private, +64 handles and +16 threads.
  Retired connection lifetime sources must be collected; at most one retired pane
  may remain, matching the existing last-focused-control acceptance bound.
- A one-second UI heartbeat detects dispatch gaps over five seconds. Observation
  gaps over 60 seconds, incomplete duration/cycles, unexpected
  disconnects or failed operations cannot produce PASS. A smoke run is always
  distinguished from a four-hour plan.

The budgets are declared before execution in plan.json. Crossing one requires
investigation; it does not by itself prove a particular product memory leak.
Metrics include the harness and headless renderer, not just protocol libraries.

## Windows coordinator

Build a stopped, isolated harness using the pinned SDK and restored dependencies:

    .tools/dotnet/dotnet.exe build tools/ServerDesk.UiSmoke -c Release --no-restore -o artifacts/soak-harness -m:1
    pwsh -File scripts/test-session-soak.ps1 -ReportDirectory artifacts/reports/soak-calibration-UNIQUE -Seconds 60 -Cycles 3
    pwsh -File scripts/test-session-soak.ps1 -ReportDirectory artifacts/reports/soak-four-hour-UNIQUE

Use the repository's documented DOTNET_CLI_HOME/NUGET_PACKAGES when needed.
Choose a new report path for every attempt. A matching stopped harness directory
may be selected with -Harness; do not overwrite an active harness.

The coordinator starts only scripts/start-test-sshd.sh with the disposable NSS
alias and loopback port 22222. It refuses to reuse an already running service.
The harness accepts only that fixture's explicit host-key fingerprint.
There is no automatic trust of an arbitrary loopback SSH endpoint.

The coordinator requests system-required execution state on its own Windows
thread for the duration of the test, then restores it. It changes no power plan
and does not keep the display on. A manual suspend can still interrupt the run.
Run the coordinator in a persistent process when disconnecting the shell.

To request cancellation, create cancel.request in the report directory.
The harness finishes its bounded current operation, closes owned sessions and
reports cancellation. A duration-plus-20-minute watchdog terminates only its
owned child if it cannot finish; an interrupted run is not PASS.

## Output and boundaries

- coordinator.json: durable parent state, child identity and exit result.
- run/plan.json: source/version, platform, workload, fixture identity and budgets.
- run/status.json: atomic progress; RUNNING is not a successful result.
- run/samples.jsonl and samples.csv: append-only measurements.
- run/result.json: completion, cleanup, retention and functional verdicts.
- stdout/stderr and failure records remain separate.

Disposable databases/files are under artifacts/soak-data/GUID; remote files are
under an exact /tmp/serverdesk-soak-GUID directory. Only that run's entries are
removed. Keep selected reports, then clean stopped scratch using the artifact
cleanup procedure. Never use a user installation as the test directory.

The first execution targets Windows with Avalonia headless/Skia and a WSL-hosted
OpenSSH fixture. It does not certify native desktop handle/window behavior,
KDE/KWallet, WAN, physical input, DPI, RDP or a native Linux four-hour run.
