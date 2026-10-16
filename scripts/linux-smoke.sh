#!/usr/bin/env bash
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo/.tools/linux-ssh"
apt-get -o "Dir::State::lists=$(pwd)/lists" download libice6 libsm6 libfontconfig1 libfreetype6 libxrandr2 libxi6 libxcursor1 libxrender1 x11-utils libxcb-shape0 libxcb-render0 libxcb-xfixes0
for package in ./*.deb; do dpkg-deb -x "$package" root; done
cd "$repo/artifacts/linux-x64"
chmod +x ServerDesk.App
export LD_LIBRARY_PATH="$repo/.tools/linux-ssh/root/usr/lib/x86_64-linux-gnu:${LD_LIBRARY_PATH:-}"
export SERVERDESK_DATA_HOME="$repo/.tools/linux-smoke-data"
ulimit -c 0
./ServerDesk.App > "$repo/.tools/linux-smoke.log" 2>&1 &
app_pid=$!
trap 'kill "$app_pid" 2>/dev/null || true' EXIT
sleep 5
kill -0 "$app_pid"
if [ -x "$repo/.tools/linux-ssh/root/usr/bin/xwininfo" ]; then "$repo/.tools/linux-ssh/root/usr/bin/xwininfo" -root -tree; fi
echo "PASS Linux GUI process alive, pid $app_pid"
cat "$repo/.tools/linux-smoke.log"
