# Contributing to ServerDesk

ServerDesk is licensed under MIT. Contributions should retain the applicable copyright and third-party notices.

Install Git, PowerShell 7 and the .NET SDK specified in `global.json`. A clean checkout does not need the maintainer's `.tools` or `artifacts` directories. Run:

```powershell
./scripts/check-repository.ps1
dotnet restore ServerDesk.sln --locked-mode
dotnet build ServerDesk.sln -c Release --no-restore -m:1
dotnet test tests/ServerDesk.Tests -c Release --no-build
```

For UI changes build and run the isolated headless harness:

```powershell
dotnet build tools/ServerDesk.UiSmoke -c Release -m:1 -p:RestoreLockedMode=true -o artifacts/ci-harness
dotnet artifacts/ci-harness/ServerDesk.UiSmoke.dll artifacts/ui-localization --localization
dotnet artifacts/ci-harness/ServerDesk.UiSmoke.dll artifacts/ui-restart --restart-design
```

Linux UI library requirements and opt-in network/native/hardware checks are in [development.md](docs/development.md). Do not use production credentials or servers for acceptance fixtures. The [CI workflow](.github/workflows/ci.yml) runs the bounded Windows/Linux matrix after push or pull request. Packaging instructions are in [releasing.md](docs/releasing.md).

Create a short branch such as `fix/settings-layout`, make a focused change, run relevant checks and commit with a clear English message. Include the problem, resulting behavior and validation in the pull request. Keep `packages.lock.json` tracked; use an intentional unlocked restore only when changing dependencies. Follow `.editorconfig` and preserve upstream notices/assets with pinned byte hashes. See [Git and release versioning](docs/github.md) for the first upload and tag workflow.

For a bug report include the exact version, operating system, display scaling, protocol, steps, expected result and actual result. Remove hostnames, addresses, usernames and tokens from logs and screenshots before sharing. Never include passwords, private keys, credential files or a profile database.

Submit changes through the repository where you obtained this source release. Report vulnerabilities privately to the release maintainer, following `SECURITY.md`.
