#!/usr/bin/env bash
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo/.tools/linux-ssh"
mkdir -p freerdp-packages/partial
apt-get --download-only --no-install-recommends --assume-yes -o Debug::NoLocking=true -o "Dir::State::lists=$(pwd)/lists" -o "Dir::Cache::archives=$(pwd)/freerdp-packages" -o Dir::Cache::pkgcache= -o Dir::Cache::srcpkgcache= install freerdp2-x11
for package in freerdp-packages/*.deb; do dpkg-deb -x "$package" root; done
export LD_LIBRARY_PATH="$(pwd)/root/usr/lib/x86_64-linux-gnu:$(pwd)/root/usr/lib/x86_64-linux-gnu/pulseaudio:${LD_LIBRARY_PATH:-}"
root/usr/bin/xfreerdp /help > "$repo/.tools/freerdp-help.txt" 2>&1 || true
ulimit -c 0
set +e
printf 'serverdesk-disposable-test-password\n' | timeout 8s root/usr/bin/xfreerdp /v:127.0.0.1:49999 /u:serverdesk-test /d:TEST /size:1280x720 /sec:nla /from-stdin:force /dynamic-resolution -clipboard > "$repo/.tools/freerdp-cli-test.txt" 2>&1
status=$?
set -e
echo "FreeRDP CLI exited with code $status against intentionally unavailable loopback port. This is not an RDP connection test."
cat "$repo/.tools/freerdp-cli-test.txt"
