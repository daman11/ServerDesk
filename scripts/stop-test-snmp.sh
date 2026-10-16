#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
root="$PWD/.tools/network-lab"
if [[ ! -f "$root/pid" ]]; then exit 0; fi
owned_pid="$(cat "$root/pid")"
if [[ ! "$owned_pid" =~ ^[0-9]+$ ]]; then echo 'Invalid test PID'; exit 1; fi
if kill -0 "$owned_pid" 2>/dev/null; then
    actual="$(readlink -f "/proc/$owned_pid/exe")"
    expected="$(readlink -f "$root/root/usr/sbin/snmpd")"
    if [[ "$actual" != "$expected" ]]; then echo 'Refusing to stop unrelated process'; exit 1; fi
    kill "$owned_pid"
fi
rm -f -- "$root/pid"
echo 'Stopped owned SNMP fixture'
