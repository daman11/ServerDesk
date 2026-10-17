#!/usr/bin/env bash
set -euo pipefail
if [[ $# != 3 ]]; then echo 'Usage: sudo bash test-data-disk-full.sh <dotnet> <DataProbe.dll> <new-report-directory>' >&2; exit 2; fi
[[ $(id -u) == 0 ]] || { echo 'An isolated tmpfs mount needs root; use a disposable lab/WSL environment.' >&2; exit 2; }
dotnet_path="$(realpath "$1")"; probe_path="$(realpath "$2")"; report_path="$(realpath -m "$3")"
[[ ! -e "$report_path" ]] || { echo 'Report destination already exists' >&2; exit 2; }
fixture_path="$(mktemp -d /tmp/serverdesk-disk-full-XXXXXXXX)"
cleanup() {
    if mountpoint -q "$fixture_path"; then umount "$fixture_path" || return; fi
    rmdir -- "$fixture_path"
}
trap cleanup EXIT
mount -t tmpfs -o size=8m,nosuid,nodev,mode=700 serverdesk-data-probe "$fixture_path"
set +e
"$dotnet_path" "$probe_path" --disk-full "$fixture_path/run"
result=$?
set -e
mkdir -p -- "$report_path"
if [[ -f "$fixture_path/run/results.json" ]]; then cp -- "$fixture_path/run/results.json" "$report_path/results.json"; fi
exit "$result"
