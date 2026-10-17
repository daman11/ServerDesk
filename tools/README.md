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
