#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
runtime="${1:-linux-x64}"
case "$runtime" in linux-x64|win-x64) ;; *) echo 'Supported: linux-x64 or win-x64' >&2; exit 1;; esac
command -v pwsh >/dev/null || { echo 'Publishing requires PowerShell 7 (pwsh) to collect and verify full license notices. See docs/releasing.md.' >&2; exit 1; }
exec pwsh -NoProfile -File scripts/publish.ps1 -Runtime "$runtime" -Versioned
