# Developer tools

These projects are development and acceptance harnesses. They are not application payloads and are kept outside the main solution so a standard build/test run does not open native diagnostic windows or contact a server.

| Project | Purpose |
|---|---|
| `ServerDesk.UiSmoke` | Headless UI, layout, localization and isolated protocol acceptance |
| `ServerDesk.AppProbe` | Native published Windows EXE/RDP-host verification |
| `ServerDesk.RdpMatrixFixture` | Opt-in Windows Forms clipboard/input receiver using only fixed synthetic data |
| `ServerDesk.DataProbe` | Owned historical migration/restore, process-termination and bounded tmpfs disk-full acceptance |
| `ServerDesk.RdpProbe` | Explicit RDP endpoint/certificate and optional FreeRDP authentication diagnostics |
| `ServerDesk.NetworkProbe` | Opt-in real SSH/SFTP checks against a disposable server |
| `ServerDesk.SecretProbe` | Isolated Linux Secret Service checks |
| `ServerDesk.SerialProbe` | Explicit serial/pseudo-terminal stream checks |
| `Shared` | Disposable loopback fixtures and shared test configuration |

Start with [development.md](../docs/development.md). Network, native-desktop and hardware tests are opt-in. Fixture passphrases in source are public test constants; generated private keys and test databases belong in ignored directories.

`ServerDesk.UiSmoke <fresh-output-directory> --native-desktop` requires `SERVERDESK_DISPOSABLE_DESKTOP=1` and an explicitly disposable, logged-in graphical session. Publish the harness self-contained so its apphost can launch the separate credential readback process. It checks the real OS text clipboard and DPAPI/Secret Service save/read/update/delete with its own random identity, restores previous clipboard text, and records screen/scaling metadata in `native-desktop.json`. This does not verify physical shortcuts, non-text clipboard formats or mixed DPI.

The owned Linux RDP lab normally requires synthesized pointer input to reach its private server. `SERVERDESK_RDP_LAB_SKIP_SYNTHETIC_POINTER=1` permits investigation of the remaining checks when a compositor blocks global input injection; `native-rdp-result.json` then explicitly records `Passed=false` and `SyntheticPointerTested=false`. A zero process exit in that mode is not a complete RDP acceptance result. See the [VM acceptance record](../docs/reports/vm-mvp-2026-10-19.md).

## Windows RDP input and clipboard workbench

`ServerDesk.AppProbe EXE REPORT HOST USER --interaction` opens an isolated ServerDesk window. Enter the password through the hidden prompt/stdin; it travels to the child through a current-user-only pipe, never an argument. `SERVERDESK_RDP_MATRIX_MINUTES` bounds the window to 1–120 minutes (20 by default). Certificate verification remains enabled. Use the ServerDesk window, not the VMware console, for physical input results.

Publish `ServerDesk.RdpMatrixFixture` self-contained for `win-x64`, then run `ServerDesk.RdpMatrixFixture.exe FRESH_ROOT local|remote --disposable-desktop` only in explicitly disposable desktops. It refuses reused roots and closes after 90 minutes. The remote role temporarily prevents sleep; the local role restores previous clipboard text on exit. Other clipboard formats are not saved/restored. Reports contain counters, comparisons and hashes, not entered text, raw key codes or clipboard payloads. Only fixed synthetic fixture files may be received, under a fresh destination inside its own root. The fixture is never shipped with the application.

Each fixture accepts a GUID/`Operation` request in its own `request.json`; responses are written atomically as `response-GUID.json`. Fixed operations cover synthetic text/large text, sentinel/empty, RTF/HTML, DIB/DIBV5/PNG, test files, read-only input checks and graceful close. No arbitrary path, shell command or input injection is accepted. Self-check each clipboard format before testing transport, replace the destination with a different sentinel first and record incomplete/missing formats as failures.

The workbench report names a fresh `matrix-control-GUID` directory. Its bounded request/response interface supports only snapshot, focus, fixed test-file preparation/drop at the known receiver, clipboard reconnect, split/float/redock and close. It does not provide arbitrary coordinates or commands. Use physical gestures for gesture acceptance; calling `files-drop` verifies the product's transfer path, not a human drag. `--rdp-matrix-control`, `--rdp-drop` and `--native-rdp` in UiSmoke cover the controller, routed drop surface and native OLE lifecycle without a remote server. [Evidence and remaining limits](../docs/reports/rdp-desktop-drop-2026-10-19.md).

`UiSmoke --native-rdp` also checks the reverse clipboard source with fixed virtual files, read-only/size-bounded streams and a native COM enumeration counter guard. `--rdp-export` checks its PL/EN dialog without a server. `--native-rdp-export-demo` opens that real dialog with a synthetic source for at most three minutes; drag to an owned fresh local folder to test the gesture. The demo does not read the actual clipboard contents, authenticate or contact RDP. API/stream checks do not count as a physical gesture or Explorer acceptance. [Current scope and failed exploratory attempts](../docs/reports/rdp-clipboard-export-2026-10-19.md).
