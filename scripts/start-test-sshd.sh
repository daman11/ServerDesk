#!/usr/bin/env bash
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
root="$repo/.tools/linux-ssh/root"
work=/tmp/serverdesk-sshd-test
fixture_os_user="${SERVERDESK_TEST_OS_USER:-${SUDO_USER:-$(id -un)}}"
fixture_uid="$(id -u "$fixture_os_user")"; fixture_gid="$(id -g "$fixture_os_user")"
[[ "$fixture_uid" != 0 ]] || { echo 'Run with sudo from an ordinary account, or set SERVERDESK_TEST_OS_USER to a non-root test account.' >&2; exit 1; }
listen_address="${SERVERDESK_TEST_LISTEN_ADDRESS:-127.0.0.1}"
mkdir -p "$work/home" /run/sshd
chmod 755 "$work" "$work/home"
chown "$fixture_uid:$fixture_gid" "$work/home"
if [ -f "$work/sshd.pid" ] && kill -0 "$(cat "$work/sshd.pid")" 2>/dev/null; then echo 'Owned SSH fixture is already running; stop it first.' >&2; exit 1; fi
if [ ! -f "$work/host_key" ]; then ssh-keygen -q -t ed25519 -N '' -f "$work/host_key"; fi
if [ ! -f "$work/user_key" ]; then ssh-keygen -q -t ed25519 -N 'serverdesk-test-passphrase' -f "$work/user_key"; fi
cp "$work/user_key.pub" "$work/authorized_keys"
cp "$work/user_key" "$repo/.tools/linux-ssh/integration_key"
chmod 600 "$work/host_key" "$work/user_key" "$repo/.tools/linux-ssh/integration_key"
chmod 644 "$work/authorized_keys"
cp /etc/passwd "$work/passwd"
cp /etc/group "$work/group"
printf 'serverdesk_fixture:x:%s:%s:ServerDesk test:%s:/bin/bash\n' "$fixture_uid" "$fixture_gid" "$work/home" >> "$work/passwd"
echo 'sshd:x:65534:65534:isolated SSH privilege separation:/run/sshd:/usr/sbin/nologin' >> "$work/passwd"
cat > "$work/sshd_config" <<EOF
Port 22222
ListenAddress $listen_address
HostKey $work/host_key
PidFile $work/sshd.pid
AuthorizedKeysFile $work/authorized_keys
StrictModes no
PasswordAuthentication no
KbdInteractiveAuthentication no
UsePAM no
PermitRootLogin no
AllowUsers serverdesk_fixture
AllowTcpForwarding no
X11Forwarding no
Subsystem sftp internal-sftp
LogLevel VERBOSE
EOF
export LD_LIBRARY_PATH="$root/usr/lib/x86_64-linux-gnu:${LD_LIBRARY_PATH:-}"
export LD_PRELOAD="$root/usr/lib/x86_64-linux-gnu/libnss_wrapper.so"
export NSS_WRAPPER_PASSWD="$work/passwd"
export NSS_WRAPPER_GROUP="$work/group"
"$root/usr/sbin/sshd" -f "$work/sshd_config" -E "$work/sshd.log" || { cat "$work/sshd.log"; exit 1; }
echo "Isolated test sshd started on port 22222, pid $(cat "$work/sshd.pid")"
hostname -I
