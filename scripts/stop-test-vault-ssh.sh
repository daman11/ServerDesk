#!/usr/bin/env bash
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
pid_file=/tmp/serverdesk-vault-ssh-test/sshd.pid
if [ -f "$pid_file" ]; then
  fixture_pid="$(cat "$pid_file")"; [[ "$fixture_pid" =~ ^[0-9]+$ ]] || exit 1
  expected="$(readlink -f "$repo/.tools/linux-ssh/root/usr/sbin/sshd")"
  actual="$(readlink "/proc/$fixture_pid/exe" || true)"; actual="${actual% (deleted)}"
  if [ "$actual" = "$expected" ] && grep -zq '/tmp/serverdesk-vault-ssh-test/sshd_config' "/proc/$fixture_pid/cmdline"; then
    kill "$fixture_pid"; echo 'Stopped disposable vault-key OpenSSH fixture.'
  fi
fi
