# Contributing to ServerDesk

ServerDesk is licensed under MIT. Contributions should retain the applicable copyright and third-party notices.

Install the .NET 10 SDK, then run:

```powershell
dotnet restore ServerDesk.sln --locked-mode
dotnet build ServerDesk.sln -c Release --no-restore -m:1
dotnet test tests/ServerDesk.Tests -c Release --no-build
```

UI smoke tools are in `tools/ServerDesk.UiSmoke`. Packaging instructions are in `docs/releasing.md`.

For a bug report include the exact version, operating system, display scaling, protocol, steps, expected result and actual result. Remove hostnames, addresses, usernames and tokens from logs and screenshots before sharing. Never include passwords, private keys, credential files or a profile database.

Submit changes through the repository where you obtained this source release. Report vulnerabilities privately to the release maintainer, following `SECURITY.md`.
