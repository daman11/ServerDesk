#!/usr/bin/env bash
# Opt-in: exclusively owned loopback NLA server and two private Xvfb displays.
set -euo pipefail
ulimit -c 0
repo="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo"
harness="${1:-artifacts/ui-harness-linux-0.38.0/ServerDesk.UiSmoke.dll}"
output="${2:-artifacts/reports/embedded-linux-rdp-2026-10-16/native}"
tools="${SERVERDESK_RDP_LAB_TOOLS:-$repo/.tools/linux-ssh/root}"
[[ "$(uname -s)" == Linux && -f "$harness" ]] || { echo 'Requires Linux and a built UI harness.' >&2; exit 1; }
for name in Xvfb freerdp-shadow-cli xfreerdp winpr-hash; do [[ -x "$tools/usr/bin/$name" ]] || { echo "Missing opt-in lab tool: $name" >&2; exit 1; }; done
mkdir -p "$output"
output="$(cd "$output" && pwd)"
lab="$(mktemp -d "$repo/artifacts/embedded-rdp-lab.XXXXXX")"
pids=()
cleanup() {
  set +e
  for pid in "${pids[@]}"; do
    if [[ -f "/proc/$pid/stat" && "$(awk '{print $4}' "/proc/$pid/stat")" == "$$" ]]; then kill "$pid" 2>/dev/null; fi
  done
  for pid in "${pids[@]}"; do
    for attempt in {1..30}; do kill -0 "$pid" 2>/dev/null || break; sleep .1; done
    if [[ -f "/proc/$pid/stat" && "$(awk '{print $4}' "/proc/$pid/stat")" == "$$" ]]; then kill -KILL "$pid" 2>/dev/null; fi
    wait "$pid" 2>/dev/null
  done
  case "$lab" in "$repo"/artifacts/embedded-rdp-lab.*) rm -rf -- "$lab";; esac
}
trap cleanup EXIT
export PATH="$tools/usr/bin:$PATH"
export LD_LIBRARY_PATH="$tools/usr/lib/x86_64-linux-gnu:$tools/usr/lib/x86_64-linux-gnu/pulseaudio:${LD_LIBRARY_PATH:-}"
export XKB_BINDIR="$tools/usr/bin"
# Abstract local sockets also work when WSLg owns a read-only /tmp/.X11-unix.
"$tools/usr/bin/Xvfb" -displayfd 3 -screen 0 1024x768x24 -nolisten tcp -nolisten unix 3>"$lab/server.display" >"$lab/server-xvfb.log" 2>&1 & pids+=("$!")
"$tools/usr/bin/Xvfb" -displayfd 3 -screen 0 1600x1000x24 -nolisten tcp -nolisten unix 3>"$lab/client.display" >"$lab/client-xvfb.log" 2>&1 & pids+=("$!")
for attempt in {1..100}; do [[ -s "$lab/server.display" && -s "$lab/client.display" ]] && break; sleep .1; done
if [[ ! -s "$lab/server.display" || ! -s "$lab/client.display" ]]; then tail -n 20 "$lab/"*xvfb.log; exit 1; fi
export SERVERDESK_RDP_LAB_SERVER_DISPLAY=":$(cat "$lab/server.display")"
export DISPLAY=":$(cat "$lab/client.display")"
export XDG_CONFIG_HOME="$lab/config-server"
mkdir -p "$XDG_CONFIG_HOME/freerdp/shadow"
openssl req -x509 -newkey rsa:2048 -nodes -subj '/CN=serverdesk-rdp-fixture.invalid' -days 1 -keyout "$XDG_CONFIG_HOME/freerdp/shadow/shadow.key" -out "$XDG_CONFIG_HOME/freerdp/shadow/shadow.crt" >"$lab/certificate.log" 2>&1
"$tools/usr/bin/winpr-hash" -u rdp_fixture -p serverdesk-disposable-rdp -d LAB -f sam >"$lab/SAM"
chmod 600 "$lab/SAM" "$XDG_CONFIG_HOME/freerdp/shadow/shadow.key"
port="$(python3 -c 'import socket; s=socket.socket(); s.bind(("127.0.0.1",0)); print(s.getsockname()[1]); s.close()')"
DISPLAY="$SERVERDESK_RDP_LAB_SERVER_DISPLAY" "$tools/usr/bin/freerdp-shadow-cli" /port:"$port" /bind-address:127.0.0.1 /sec:nla /sam-file:"$lab/SAM" -auth >"$lab/shadow.log" 2>&1 & pids+=("$!")
sleep 1
kill -0 "${pids[2]}" || { cat "$lab/shadow.log"; exit 1; }
export SERVERDESK_RDP_LAB_SHA256="$(openssl x509 -in "$XDG_CONFIG_HOME/freerdp/shadow/shadow.crt" -noout -fingerprint -sha256 | cut -d= -f2)"
export SERVERDESK_DISPOSABLE_RDP_LAB=1
printf 'Owned NLA fixture and separate server/client Xvfb displays are ready.\n'
set +e
printf '%s\n' serverdesk-disposable-rdp | "$tools/usr/bin/xfreerdp" /v:127.0.0.1:"$port" /u:rdp_fixture /d:LAB /sec:nla /from-stdin:force /cert:deny,fingerprint:sha256:"$SERVERDESK_RDP_LAB_SHA256" /auth-only /log-level:ERROR >"$lab/fixture-auth.log" 2>&1
auth_result=$?
printf '%s\n' serverdesk-disposable-rdp | "$tools/usr/bin/xfreerdp" /v:127.0.0.1:"$port" /u:rdp_fixture /d:LAB /sec:nla /from-stdin:force /cert:deny,fingerprint:sha256:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA:AA /auth-only /log-level:ERROR >"$lab/fixture-certificate-denied.log" 2>&1
denied_result=$?
[[ "$auth_result" == 0 && "$denied_result" != 0 ]] || { echo 'Fixture auth/certificate guard failed.' >&2; exit 1; }
echo 'PASS fixture NLA authentication and rejection of a different untrusted certificate exception'
dotnet "$harness" "$output" --native-linux-rdp "$port" >"$output/native.log" 2>&1
result=$?
set -e
cp "$lab/shadow.log" "$output/shadow.log"
cat "$output/native.log"
exit "$result"
