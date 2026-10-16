#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
root="$PWD/.tools/network-lab"
mkdir -p "$root/state"
if [[ -f "$root/pid" ]] && kill -0 "$(cat "$root/pid")" 2>/dev/null; then echo 'Owned SNMP test agent already running'; exit 0; fi
export LD_LIBRARY_PATH="$root/root/usr/lib/x86_64-linux-gnu:$PWD/.tools/linux-ssh/root/usr/lib/x86_64-linux-gnu:${LD_LIBRARY_PATH:-}"
export SNMP_PERSISTENT_DIR="$root/state"
export MIBS=''
test_ip="${SERVERDESK_TEST_LISTEN_ADDRESS:-127.0.0.1}"
cat > "$root/snmpd.conf" <<EOF
agentaddress udp:$test_ip:21161
sysName ServerDesk-SNMP-fixture
sysLocation isolated-test-agent
rouser fixture-none noauth .1
rouser fixture-256 auth .1
rouser fixture-384 auth .1
rouser fixture-512 auth .1
EOF
cat > "$root/state/snmpd.conf" <<'EOF'
createUser fixture-none
createUser fixture-256 SHA-256 fixture-auth-2026 AES fixture-priv-2026
createUser fixture-384 SHA-384 fixture-auth-2026 AES fixture-priv-2026
createUser fixture-512 SHA-512 fixture-auth-2026 AES fixture-priv-2026
EOF
"$root/root/usr/sbin/snmpd" -f -Lo -C -c "$root/snmpd.conf,$root/state/snmpd.conf" -p "$root/pid" > "$root/agent.log" 2>&1 &
owned_pid=$!
sleep 1
kill -0 "$owned_pid"
echo "$owned_pid" > "$root/pid"
echo "Owned Net-SNMP fixture running at $test_ip:21161, pid $owned_pid"
