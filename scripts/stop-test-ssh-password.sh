#!/usr/bin/env bash
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
work=/tmp/serverdesk-ssh-password-test
if [ -f "$work/sshd.pid" ]; then
  fixture_pid="$(cat "$work/sshd.pid")"
  [[ "$fixture_pid" =~ ^[0-9]+$ ]] || exit 1
  expected="$(readlink -f "$repo/.tools/linux-ssh/root/usr/sbin/sshd")"
  actual="$(readlink "/proc/$fixture_pid/exe" || true)"; actual="${actual% (deleted)}"
  if [ "$actual" = "$expected" ]; then kill "$fixture_pid"; echo 'Stopped isolated password-only SSH fixture'; fi
fi
