# Developer tools

These projects are development and acceptance harnesses. They are not application payloads and are kept outside the main solution so a standard build/test run does not open native diagnostic windows or contact a server.

| Project | Purpose |
|---|---|
| `ServerDesk.UiSmoke` | Headless UI, layout, localization and isolated protocol acceptance |
| `ServerDesk.AppProbe` | Native published Windows EXE/RDP-host verification |
| `ServerDesk.DataProbe` | Owned historical migration/restore, process-termination and bounded tmpfs disk-full acceptance |
| `ServerDesk.RdpProbe` | Explicit RDP endpoint/certificate and optional FreeRDP authentication diagnostics |
| `ServerDesk.NetworkProbe` | Opt-in real SSH/SFTP checks against a disposable server |
| `ServerDesk.SecretProbe` | Isolated Linux Secret Service checks |
| `ServerDesk.SerialProbe` | Explicit serial/pseudo-terminal stream checks |
| `Shared` | Disposable loopback fixtures and shared test configuration |

Start with [development.md](../docs/development.md). Network, native-desktop and hardware tests are opt-in. Fixture passphrases in source are public test constants; generated private keys and test databases belong in ignored directories.

`ServerDesk.UiSmoke <fresh-output-directory> --native-desktop` requires `SERVERDESK_DISPOSABLE_DESKTOP=1` and an explicitly disposable, logged-in graphical session. Publish the harness self-contained so its apphost can launch the separate credential readback process. It checks the real OS text clipboard and DPAPI/Secret Service save/read/update/delete with its own random identity, restores previous clipboard text, and records screen/scaling metadata in `native-desktop.json`. This does not verify physical shortcuts, non-text clipboard formats or mixed DPI.

The owned Linux RDP lab normally requires synthesized pointer input to reach its private server. `SERVERDESK_RDP_LAB_SKIP_SYNTHETIC_POINTER=1` permits investigation of the remaining checks when a compositor blocks global input injection; `native-rdp-result.json` then explicitly records `Passed=false` and `SyntheticPointerTested=false`. A zero process exit in that mode is not a complete RDP acceptance result. See the [VM acceptance record](../docs/reports/vm-mvp-2026-10-19.md).
