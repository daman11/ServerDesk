# Contributing to ServerDesk

ServerDesk is licensed under MIT. Contributions should retain the applicable copyright and third-party notices.

Install Git, PowerShell 7 and the .NET SDK specified in `global.json`. A clean checkout does not need the maintainer's `.tools` or `artifacts` directories.

For small changes, use an incremental build of the affected project and only relevant checks. The maintainer tests application changes directly from a development folder; provide the executable path. Prefer `artifacts/dev-harness` when stopped, or another development folder if the current app is running. Do not interrupt active sessions. Small changes do not require the full test matrix, version bumps, installers or release tags; keep focused local commits. Documentation-only changes need content/link/diff checks rather than an application build.

For larger changes, clean-checkout verification and release preparation, run:

```powershell
./scripts/check-repository.ps1
dotnet restore ServerDesk.sln --locked-mode
./scripts/audit-dependencies.ps1
./scripts/test-dependency-audit.ps1
dotnet build ServerDesk.sln -c Release --no-restore -m:1
dotnet test tests/ServerDesk.Tests -c Release --no-build
```

When UI verification is needed, build the isolated headless harness and select the scenario relevant to the change. For example:

```powershell
dotnet build tools/ServerDesk.UiSmoke -c Release -m:1 -p:RestoreLockedMode=true -o artifacts/ci-harness
dotnet artifacts/ci-harness/ServerDesk.UiSmoke.dll artifacts/ui-localization --localization
dotnet artifacts/ci-harness/ServerDesk.UiSmoke.dll artifacts/ui-restart --restart-design
```

Linux UI library requirements and opt-in network/native/hardware checks are in [development.md](docs/development.md). Do not use production credentials or servers for acceptance fixtures. The [CI workflow](.github/workflows/ci.yml) runs the bounded Windows/Linux matrix after push or pull request. Packaging instructions are in [releasing.md](docs/releasing.md).

Create a short branch such as `codex/settings-layout`, make a focused change, run relevant checks and commit with a clear English message. Include the problem, resulting behavior and validation in the pull request. Keep `packages.lock.json` tracked; use an intentional unlocked restore only when changing dependencies. Follow `.editorconfig` and preserve upstream notices/assets with pinned byte hashes. See [Git and release versioning](docs/github.md) for the first upload and tag workflow.

For a bug report include the exact version, operating system, display scaling, protocol, steps, expected result and actual result. Remove hostnames, addresses, usernames and tokens from logs and screenshots before sharing. Never include passwords, private keys, credential files or a profile database.

Submit changes through the repository where you obtained this source release. Report vulnerabilities privately to the release maintainer, following `SECURITY.md`.
