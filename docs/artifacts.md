# Generated artifacts

The entire `artifacts/` directory is ignored by Git. Application source belongs in `src/`, `tests/`, `tools/` and `docs/`.

| Location | Retention |
|---|---|
| `artifacts/releases/VERSION/` | Frozen release packages, documentation and checksums. Preserve every published version unchanged. |
| `artifacts/reports/` | Selected final logs, TRX/JSON results and representative screenshots. Avoid copying runtimes, test databases or credentials here. |
| Other entries | Reproducible builds, harnesses, staging trees and temporary fixtures. Remove when no longer needed. |

A release includes alternative packages for Windows and Linux, source, notices, verification notes and checksums. Users select a package appropriate to their system; the complete set remains useful for distribution and verification.

## Cleanup

Run from the repository root. Close builds and test tools first. Preview the candidates before deleting them:

```powershell
./scripts/clean-artifacts.ps1 -WhatIf
./scripts/clean-artifacts.ps1 -Confirm:$false
```

The cleaner always retains top-level `releases`, `reports` and `README.md`. It also retains entries referenced by detected ServerDesk/build processes or, on Windows, ServerDesk shortcuts in the current user's Desktop and Start Menu. Detection is a precaution; use `-Keep` for other portable builds or work you still need:

```powershell
./scripts/clean-artifacts.ps1 -Keep 'win-x64-0.32.0' -WhatIf
```

`-Keep` accepts literal top-level names. `-Path` can select a disposable subtree inside this repository's `artifacts/`, but cannot select frozen release/report trees or traverse a symbolic link or junction. Links encountered during deletion are removed without traversing their targets. The cleaner never terminates a process. Failures are reported in `reports/cleanup-latest.json`; `-WhatIf` writes no report and removes nothing.

Keep one current harness build where practical and reuse it after stopping it. Use separate copies only when needed for concurrent tests. Copy useful final evidence into `artifacts/reports/` before cleanup. Release preparation is described in [the release procedure](releasing.md).

The disposable safety test checks root guards, preview behavior, preserved content, read-only files and link target protection:

```powershell
pwsh -File scripts/test-artifact-cleanup.ps1
```
