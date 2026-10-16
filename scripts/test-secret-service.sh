#!/usr/bin/env bash
set -euo pipefail
ulimit -c 0
repo="$(cd "$(dirname "$0")/.." && pwd)"
if [ "${SERVERDESK_ISOLATED_BUS:-}" != yes ]; then
  export SERVERDESK_ISOLATED_BUS=yes
  exec dbus-run-session -- bash "$0"
fi
work="$(mktemp -d /tmp/serverdesk-keyring.XXXXXX)"
export XDG_DATA_HOME="$work/data" XDG_CONFIG_HOME="$work/config" XDG_RUNTIME_DIR="$work/runtime"
mkdir -p "$XDG_DATA_HOME" "$XDG_CONFIG_HOME" "$XDG_RUNTIME_DIR"
chmod 700 "$work" "$XDG_RUNTIME_DIR"
root="$repo/.tools/linux-ssh/root"
export PATH="$root/usr/bin:$PATH" LD_LIBRARY_PATH="$root/usr/lib/x86_64-linux-gnu:${LD_LIBRARY_PATH:-}"
printf 'serverdesk-disposable-keyring-password' | gnome-keyring-daemon --unlock --components=secrets --foreground > "$work/daemon.log" 2>&1 &
daemon_pid=$!
trap 'kill "$daemon_pid" 2>/dev/null || true; case "$work" in /tmp/serverdesk-keyring.*) rm -rf -- "$work";; esac' EXIT
sleep 1
"$repo/artifacts/secret-probe-linux/ServerDesk.SecretProbe"
