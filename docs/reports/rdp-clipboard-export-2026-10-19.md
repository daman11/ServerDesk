# RDP clipboard export — local development increment, 2026-10-19

Status: implementation and local verification after 0.43.0. The maintainer explicitly deferred VM and physical gesture attempts in this turn. This does not close the full RDP acceptance point or produce a release.

## Implemented behavior

Embedded Windows RDP has **Files from RDP**. Copy files in remote Explorer with Ctrl+C, open the dialog, inspect the current clipboard list and drag its handle to a local folder. This is a **copy-then-drag clipboard bridge**, not a continuous mouse gesture starting inside remote Explorer. The clipboard is shared across sessions/applications; the dialog explains that names must be checked. Ordinary local Explorer Ctrl+V remains an alternative. Linux FreeRDP export dragging is not implemented.

The source exposes only validated `FileGroupDescriptorW`, indexed read-only `FileContents` and Copy preference. It never forwards move/delete feedback to the remote data object, shares drives or stages private files in a hidden cache. The target application owns saving, conflicts and progress; a submitted drop is not a completion acknowledgement.

The platform-independent parser rejects absolute/traversing/ADS/reserved paths, duplicate and file-parent collisions, missing sizes, invalid Unicode and reparse points. Limits: 256 roots, 10,000 descriptors, 64 levels and total below 2 GiB. Stream reads/seeks/copies are bounded by the descriptor, reject short/growing data, forbid writes and check session/clipboard validity. A COM worker guard reads thread-safe session state rather than Avalonia properties. Already-blocked native stream calls are not forcibly interrupted; real loss-of-network behavior remains an acceptance item.

The native enumerator uses an explicit pointer to one ULONG for `pceltFetched`, independent of the requested batch size. This avoids the framework interface's array-size marshalling for that counter. A native-vtable call requests 64 formats and checks guard bytes around the single returned counter. Export interfaces are explicitly COM-visible. No changes were made to credentials, certificate trust or automatic clipboard mode.

## Local evidence

- **117/117** focused xUnit cases: existing RDP configuration/probe/file-selection coverage plus the new descriptor validation cases. This count does not imply execution on Linux or a real RDP server.
- Native source checks: Unicode nested text and 1 MiB binary, byte/SHA-256 equality, virtual-only formats, Copy preference, suppressed remote feedback, disconnected/cancelled source, bounded/read-only/short/growing streams, seek and COM copy-all.
- Native format-enumerator counter guard; retained HWND on detach/reattach and repeated disposal; existing OLE incoming-drop regressions.
- Follow-up native `IDataObject` vtable checks verify descriptor HGLOBAL, indexed binary IStream bytes and rejection of CF_HDROP through the COM callable wrapper, rather than only managed method calls. The local native runner passes; this is not an Explorer drag or a live remote-source test.
- PL/EN export-dialog layout at 360/640 px, existing incoming-drop layout at 240/520/1100 px, plus focused pane/GUI/controller/help/localization checks.

Artifacts: `artifacts/rdp-reverse/tests/rdp-reverse.trx`, `final-v2-native-rdp/received/`, `final-v2-rdp-export/` and other `final-v2-*` scenario folders. Selected results are retained in `artifacts/reports/rdp-clipboard-export-2026-10-19/`.

## Attempts that do not count as PASS

Computer Use did not receive approval for the synthetic export window. The maintainer also could not perform the requested local gesture. Consequently a physical drag into actual Explorer is **unconfirmed**.

An exploratory direct Windows Shell folder-handler probe accepted the initial Copy effect but timed out in Drop. It did not read the file streams and produced no copied files. Traces are retained under `artifacts/rdp-reverse/shell-*/`. Direct handler invocation did not reproduce the complete OLE gesture context; it is not a passing Explorer integration test and does not establish that Explorer export works. The exploratory probe was removed from the maintained runner; the bounded source/wire/stream tests above remain. Full Explorer compatibility must be checked through the real drag loop before this feature is considered accepted.

The initial native-counter test exposed COM interface visibility requirements; these were corrected before the final native checks. An attempted harness rebuild met its own active demonstration process's file lock; a separate final harness folder was used. Neither attempt is counted as a successful check.

## Remaining acceptance

The VMs were not started or modified. Real remote-to-local Explorer copying, conflicts/cancellation, several sessions, source changes and network interruption remain open. Physical AltGr and Japanese Microsoft IME must be tested inside ServerDesk, not the VMware console. Remaining image/rich clipboard formats, Linux mouse/clipboard, account variants and mixed DPI retain their previous unverified status. See the [RDP matrix](../rdp-test-matrix.md) and [integration guide](../rdp-integration.md).

A continuous gesture beginning in remote Explorer also needs a cooperating remote component: the documented ActiveX clipboard controller synchronizes clipboards and does not expose a remote OLE drag source. The maintainer has been asked whether a manually launched, non-administrator helper in the remote Windows session is acceptable. No helper has been deployed, and no heuristic edge-triggered Ctrl+C is implemented.

Primary references: [Shell file formats](https://learn.microsoft.com/en-us/windows/win32/shell/clipboard), [OLE data transfer](https://learn.microsoft.com/en-us/windows/win32/shell/dataobject), [DoDragDrop](https://learn.microsoft.com/en-us/windows/win32/api/ole2/nf-ole2-dodragdrop). This local implementation is not a substitute for the remaining live acceptance.

Remote integration references: [IMsRdpClipboard](https://learn.microsoft.com/en-us/windows/win32/termserv/imsrdpclipboard), [MS-RDPECLIP copy/paste sequence](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-rdpeclip/9a784d09-bf53-4362-995d-a20d0eb5f074), [CreateVirtualChannels](https://learn.microsoft.com/en-us/windows/win32/termserv/imstscax-createvirtualchannels), [OnChannelReceivedData](https://learn.microsoft.com/en-us/windows/win32/termserv/imstscaxevents-onchannelreceiveddata).
