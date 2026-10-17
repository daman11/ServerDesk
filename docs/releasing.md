# Building and publishing a release

Release baseline: **0.40.0 Public Beta**, MIT. Windows x64 is the primary public target; Linux x64 has the validation limits described in `known-issues.md`. Development can continue after copying the immutable release directory to the chosen release host.

## Build requirements

- Windows x64, PowerShell 7, .NET 10 SDK (global.json), WSL with Python 3, tar and dpkg-deb for Linux packaging.
- NSIS **3.13** compiler, from [official downloads](https://nsis.sourceforge.io/Download). The Windows installer uses its zlib compressor. [License and notices](https://nsis.sourceforge.io/Docs/AppendixI.html) are retained under third-party/licenses.
- Tested compiler ZIP: `nsis-3.13.zip`, SHA256 `ba63dffc4410ee89193e1cb5a41989991bd77c61068da17e3156d136b7b0b3d8`. This is build tooling, not an application dependency. Place it under `.tools/nsis-3.13` or pass `-Compiler` to package-windows.ps1. Verify the publisher's release source before obtaining tools.
- No production keys, credentials or private configuration are required.

## Version and release gates

1. Start from a clean Git checkout. Change `Directory.Build.props` and update README, CHANGELOG, guides, known issues, `docs/releases/release-VERSION.md` and `docs/reports/release-verification-VERSION.md`. Version numbers identify immutable artifacts. Never replace published files or move a release tag under an existing version.
2. `dotnet restore ServerDesk.sln --locked-mode`, then `scripts/build.ps1 -Test`. Fix failures before packaging.
3. Audit dependencies: `dotnet list ServerDesk.sln package --vulnerable --include-transitive --format json`. A successful command is not enough: inspect advisory output and source errors. Retain a dated report. No advisory result is a complete security audit.
4. Build self-contained payloads sequentially: `scripts/publish.ps1 -Runtime win-x64 -Versioned`, then the same with `linux-x64`. Fresh staging avoids stale files. Notices are generated offline from restored packages and pinned upstream license texts; missing notices fail publication.
5. `scripts/package-windows.ps1` builds the per-user Setup EXE with `/INPUTCHARSET UTF8`. `Unicode true` alone does not control source decoding. Keep `packaging/windows/owned-files.json` in source: it is the cumulative exact-file ledger for uninstall and removal of retired binaries. No recursive deletion of the install directory or user-data directories is used.
6. In WSL at the repository root: `bash scripts/package-deb.sh 0.40.0`. This creates DEB and versioned TAR.GZ from the versioned Linux payload.
7. Run the full current headless UI list from `.github/workflows/ci.yml` on Windows/Linux (including `--mvp-hardening`), plus `--network-tools` and `--app-polish`. Run published native startup/RDP-host checks, `--published-restart <exe>`, `scripts/test-installer-text.ps1`, `scripts/test-installer.ps1` and `scripts/test-release-content.ps1`. The text check reads the actual compiled welcome page without advancing installation. Installer tests use `/NOREGISTER`, isolated workspace directories and no normal user database or credential store. `/NOREGISTER` skips Start menu/Apps registration; default installation includes both.
8. `scripts/assemble-release.ps1` collects only approved files, creates the source archive and writes SHA256SUMS/release.json. Review it before publishing. Run `scripts/test-release-content.ps1` again on the assembled set.
9. Commit the final validated source, rebuild/repackage from that clean commit and repeat the published-payload/installer/content gates before freezing the final set. The content check requires the EXE version and ProductVersion commit to match current source. Create the annotated `vVERSION` tag once and run `scripts/check-release-state.ps1 -RequireCurrentRelease`. The guard requires clean source at that tag, the complete local distribution set, valid SHA-256 digests and packaged source identical to tracked files. If verification changes a source file, make a new commit and rebuild/reassemble before freezing; never retag a frozen release. Link the versioned installer when handing over the result.

Small changes use a lightweight development cycle: a focused commit, relevant checks and an incremental Windows build in a development folder for manual testing. Do not automatically run the full test matrix, bump the version, package an installer or create a tag. Prefer `artifacts/dev-harness/ServerDesk.App.exe` when stopped; if it is in use, build to another development folder and provide that executable path without interrupting the running app. Commits ahead of the last release are expected.

The complete procedure above applies to larger changes or an explicit release request. New modules, substantial cross-module work, database migrations and changes to credentials or protocol/session lifecycles warrant broader validation. Run `scripts/check-release-state.ps1` when preparing that batch and finish with `-RequireCurrentRelease` after freezing its packages. Documentation-only maintenance remains a normal commit without an application release. Explicit session instructions override this cadence.

The source snapshot is built from an allowlist of directories and file types. It excludes `.tools`, `artifacts`, bin/obj, private keys, databases, environment files and test results. Lab fixture scripts/passwords in source are explicitly for isolated tests and are not user credentials. Production sessions must never be used as release fixtures.

## Installer behavior

Per-user installation, no elevation, stable uninstall identity and optional launch on the finish page. Refuses to replace/remove a payload with locked EXE/DLL files; it does not force-close sessions or schedule replacement at reboot. Files are copied directly after that check: a disk failure can leave an incomplete install, so rerun Setup after correcting it. This is not a transactional MSI.

Uninstall removes only owned application files and its matching registration/shortcut. Extra files in the install directory and all user data/credentials are preserved. ZIP/TAR upgrades use new directories. Backups and downgrade limits are explained in the guides.

## Publication and next versions

The prepared folder is `artifacts/releases/0.40.0`. Publish the Setup EXE, ZIP, DEB, TAR.GZ, source ZIP, release notes, verification report and checksums together. Mark the host release as **prerelease / beta**. A public repository, issue tracker and private security contact must be selected by the maintainer; no destination is fabricated or uploaded automatically.

This build has no trusted Authenticode signature. If signing is introduced, sign payload binaries before ZIP/Setup compilation, sign Setup after compilation, then regenerate checksums and repeat verification. Never claim SHA256 authenticates the publisher.

Freeze the validated release commit with its annotated version tag. Develop the next patch/minor version separately; distribute complete versioned packages. There is no automatic updater or update-server dependency. A new release must repeat the gates and disclose new/remaining limitations.
