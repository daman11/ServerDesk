#!/usr/bin/env bash
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
root="$repo/.tools/linux-ssh/root"
work=/tmp/serverdesk-sshd-test
mkdir -p "$work" /run/sshd
chmod 755 "$work"
if [ ! -f "$work/host_key" ]; then ssh-keygen -q -t ed25519 -N '' -f "$work/host_key"; fi
if [ ! -f "$work/user_key" ]; then ssh-keygen -q -t ed25519 -N 'serverdesk-test-passphrase' -f "$work/user_key"; fi
cp "$work/user_key.pub" "$work/authorized_keys"
cp "$work/user_key" "$repo/.tools/linux-ssh/integration_key"
chmod 600 "$work/host_key" "$work/user_key"
chmod 644 "$work/authorized_keys"
cp /etc/passwd "$work/passwd"
cp /etc/group "$work/group"
echo 'sshd:x:65534:65534:isolated SSH privilege separation:/run/sshd:/usr/sbin/nologin' >> "$work/passwd"
cat > "$work/sshd_config" <<EOF
Port 22222
ListenAddress 0.0.0.0
HostKey $work/host_key
PidFile $work/sshd.pid
AuthorizedKeysFile $work/authorized_keys
StrictModes no
PasswordAuthentication no
KbdInteractiveAuthentication no
UsePAM no
PermitRootLogin no
AllowUsers damian
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
