# ServerDesk — 0.36.0 Public Beta

Native Windows/Linux workspace for SSH, SFTP, RDP, VNC and serial connections. Built with C#, .NET 10 and Avalonia 12. Free and open source under the [MIT license](LICENSE).

**Publiczna beta — rozwój MVP.** [Instrukcja PL](docs/getting-started.md) · [English guide](docs/getting-started.en.md) · [Documentation index](docs/README.md) · [Known issues / ograniczenia](docs/known-issues.md) · [Changelog](CHANGELOG.md).

## Downloads and installation

| System | Package | Start |
|---|---|---|
| Windows x64 | `ServerDesk-0.36.0-win-x64-Setup.exe` | Run Setup; current-user installation and Start menu shortcut |
| Windows x64 portable | `ServerDesk-0.36.0-win-x64.zip` | Extract everything; run `ServerDesk.App.exe` |
| Linux x64 DEB | `serverdesk_0.36.0_amd64.deb` | `sudo apt install ./serverdesk_0.36.0_amd64.deb` |
| Linux x64 archive | `ServerDesk-0.36.0-linux-x64.tar.gz` | Extract into a new directory; run `./ServerDesk.App` |
| Source | `ServerDesk-0.36.0-source.zip` | .NET 10 SDK required |

The binary packages include .NET. Linux additionally needs its system X11/fontconfig libraries, Secret Service for remembered passwords and an installed FreeRDP client for RDP. Linux verification used Ubuntu/WSLg; other desktop/distribution combinations are experimental. ARM/macOS packages are not provided.

This beta is unsigned. Obtain it from a trusted release source and compare its SHA-256 with `SHA256SUMS.txt`. A checksum beside the download does not independently authenticate the publisher. No public download repository is configured in this source snapshot.

## Features

- Shared offline Smart Cheat Sheet / user snippets panel (Ctrl+Shift+K): 165 command variants, SSH OS/tool discovery, manual profiles, parameters, favorites, encrypted snippets and confirmed terminal actions. [Guide and limits](docs/commands.md).
- SSH terminal, SFTP transfer queue, batch/recursive deletion with preview and Linux monitoring.
- Local SSH/Serial scrollback search (Ctrl+Shift+F), reviewed SSH trust management in Settings, and cancellable background filtering/sorting for large file lists.
- Independent SFTP directory histories, Back/Forward/Parent/Home controls and confirmed paths for file-operation destinations.
- Six searchable settings categories; configurable terminal history/cursor and first-open SFTP directory, hidden files, sorting and active panel.
- Larger connection lists with batched updates; bounded transfer progress and tested cancellation/retry under network loss.
- SSH VT keyboard fixes, background PTY setup and coalesced resizing; real password/OpenSSH terminal acceptance checks.
- SFTP symbolic-link navigation/details, remote link creation and explicit copying of resolved contents, with bounded directory traversal and protected link destinations.
- Nine [Network Tools](docs/network-tools.md): Ping, Traceroute/MTR, DNS, TCP scanner, Wake-on-LAN, HTTP, TLS, subnet calculator and SNMP v2c/v3; standalone targets and editable server context.
- Embedded Windows RDP; external Linux FreeRDP; embedded VNC; serial terminal.
- One, two, three or four workspace panels, detachable tabs and recovery without automatic connections; configurable startup layout, new-tab placement, separators and tab-strip height.
- Server/folder icons, colors, custom artwork, five themes and configurable interface/terminal.
- Polish and English UI, destination-language reminders and Save and restart in Settings.
- Responsive welcome dashboard with recent connections, searchable settings (Ctrl+F) and clear active local/remote SFTP panel headings.
- Verified SSH host keys, optional local password storage, JSON profile import/export without secrets.

**VNC is unencrypted.** Protect it with a VPN or an existing secure tunnel. Read [known issues](docs/known-issues.md) before using this beta for your own workflows.

Original MVP acceptance and remaining verification: [mvp-status.md](docs/mvp-status.md).

Close the app and back up its data before upgrading. Windows Setup and Linux package removal preserve user data. ZIP/TAR updates should be extracted into a new directory. See the [upgrade/backup guide](docs/getting-started.en.md) and [local-data/privacy notes](docs/privacy.md).

## Development

Install the SDK specified in `global.json` and Git. The repository restores from nuget.org with tracked lockfiles; `.tools` is optional local tooling and is not needed for a clean checkout.

```sh
dotnet restore ServerDesk.sln --locked-mode
dotnet build ServerDesk.sln -c Release --no-restore -m:1
dotnet test tests/ServerDesk.Tests -c Release --no-build
dotnet run --project src/ServerDesk.App
```

Source archive: [CONTRIBUTING.md](CONTRIBUTING.md), [development notes](docs/development.md), [release procedure](docs/releasing.md), [full user manual](docs/user-manual.md). Binary packages include the quick-start documentation; development and historical reports are in the source archive.

## Repository layout and version control

```text
src/             Core models, infrastructure and Avalonia application
tests/           Unit and bounded loopback tests
tools/           Optional UI, protocol and native acceptance harnesses
scripts/         Build, validation, packaging and disposable lab entry points
packaging/       Windows/Linux packaging assets
third-party/     Pinned upstream notices and asset provenance
docs/            Current guides; releases/ and reports/ contain history
.github/         Windows/Linux CI, issue/PR templates and Dependabot
```

Git history starts at the imported `v0.31.1` baseline. Keep logical changes in separate commits and release packages outside Git. [Git workflow and first GitHub upload](docs/github.md). No remote is configured until the maintainer selects a repository.

Report bugs to the maintainer/repository from which you obtained the release. Include the version, OS and reproduction steps; remove infrastructure identifiers from logs. Follow [SECURITY.md](SECURITY.md) for private vulnerability reporting. ServerDesk does not implement telemetry or automatic update checks.

Bundled components have separate licenses. Their full texts and a dependency inventory are included in each binary package. Source notices are under `third-party/`; restored package/runtime notices are gathered during publishing.
