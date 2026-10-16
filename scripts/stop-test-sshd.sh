#!/usr/bin/env bash
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
pid_file=/tmp/serverdesk-sshd-test/sshd.pid
if [ -f "$pid_file" ]; then
  test_pid="$(cat "$pid_file")"
  [[ "$test_pid" =~ ^[0-9]+$ ]] || exit 1
  expected="$(readlink -f "$repo/.tools/linux-ssh/root/usr/sbin/sshd")"
  actual="$(readlink "/proc/$test_pid/exe" || true)"
  actual="${actual% (deleted)}"
  if [ "$actual" = "$expected" ]; then kill "$test_pid"; echo 'Stopped isolated test sshd'; fi
fi
