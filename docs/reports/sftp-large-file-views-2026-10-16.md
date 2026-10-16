# Large SFTP file views — 2026-10-16

Development changes after the frozen 0.31.1 release. No release archives were modified.

## Result

Local and remote directory views with at least 2000 source entries filter and sort on a worker, using snapshots captured on the dispatcher. An 80 ms delay coalesces input. Obsolete work is cancelled during enumeration and sorting; a semaphore allows one active computation per pane. Requests cancelled while waiting do not copy or sort their lists. Results publish only for the current request and directory paths. Small lists keep immediate updates.

Publication retains currently selected visible files and independent panel preferences. Loading another directory clears that panel's previous rows before its background result is ready. The session operation indicator explains the work in Polish or English. Reconnect and close cancel and await outstanding view tasks before releasing their lifetimes.

## Verification

- Release solution build: passed, no warnings/errors. Linux x64 self-contained harness publish: passed.
- Unit suite: **496 passed, 0 failed, 0 skipped**, including seven new cancellation/ordering cases.
- Windows 11 and Linux x64 in Ubuntu/WSL: real headless controls with **50,000 generated file entries per panel**, PL/EN and Graphite/Light. Checked dispatcher work, natural ordering, latest of 30 requests, independent size/direction sorting, selected identity, no-match parent row, cancellation before a small replacement and close/drain. Rendered screenshots inspected on Windows.
- Existing sorting acceptance: passed for server names/IP/folders, direction persistence, keyboard controls and local/remote SFTP selection, PL/EN, compact/large Graphite/Light layouts.
- Real OpenSSH fixture on loopback port 22222: complete 5000-file remote directory, natural sorting and retained filtered selection; refresh 295 ms. Paused listing cancellation, silent upload/download cancellation, dropped overwrite, retry and original-file preservation passed. The fixture was stopped after testing.
- A warning about failed remote temporary-upload cleanup occurred during intentional transport loss. This is the documented remaining `.part` limitation; no partial upload was published over the final destination.

| Platform | Returning the request to UI, PL / EN | Initial views ready, PL / EN | Close with pending view, PL / EN |
|---|---|---|---|
| Windows | 6.64 / 0.72 ms | 659 / 714 ms | 61 / 30 ms |
| Linux/WSL | 6.54 / 0.86 ms | 822 / 813 ms | 168 / 34 ms |

Measurements describe this bounded local fixture, not a general latency guarantee. The 50,000-entry test exercises the client views using generated metadata; it does not read 50,000 files from a remote server. Whole-directory listings still occupy memory, and publishing rows/selections still occurs on the UI thread. WAN conditions, other SSH servers, very large recursive trees and long-session memory behavior remain further work.

Local evidence: `artifacts/reports/mvp-large-file-views/` contains build/unit logs, rendered views and JSON timing records. A single current harness per platform is reusable under `artifacts/dev-harness*`; none of these generated files belong in Git.
