# Build and validation scripts

| Purpose | Entry points |
|---|---|
| Repository checks | `check-repository.ps1` |
| Generated-output cleanup | `clean-artifacts.ps1 -WhatIf`, `test-artifact-cleanup.ps1` ([retention guide](../docs/artifacts.md)) |
| Build and unit tests | `build.ps1 -Test`, `bash build.sh` |
| Self-contained payload | `publish.ps1`, `publish.sh` |
| Packaging | `package-windows.ps1`, `package-deb.sh` |
| Release assembly | `assemble-release.ps1`, `generate-notices.ps1` |
| Package acceptance | `test-installer-text.ps1`, `test-installer.ps1`, `test-release-content.ps1`, `test-linux-release.py` |
| Optional integration lab | `prepare-*`, `start-test-*`, `stop-test-*`, `test-secret-service.sh`, `test-freerdp-cli.sh` |

Run from the repository root; scripts resolve their own project root. See [release procedure](../docs/releasing.md) and [development guide](../docs/development.md). Lab scripts run disposable services, may need elevated rights and are not a production deployment configuration.
