#!/usr/bin/env bash
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
root="$repo/.tools/linux-ssh/root"
work=/tmp/serverdesk-ssh-password-test
mkdir -p "$work/home" /run/sshd
chmod 755 "$work" "$work/home"
if [ -f "$work/sshd.pid" ] && kill -0 "$(cat "$work/sshd.pid")" 2>/dev/null; then
  echo 'Password fixture is already running; stop it before restarting.' >&2
  exit 1
fi
if [ ! -f "$work/host_key" ]; then ssh-keygen -q -t ed25519 -N '' -f "$work/host_key"; fi
chmod 600 "$work/host_key"
# Known development-only fixture password, never an OS/user account credential.
# NSS wrapper presents a temporary alias of the ordinary WSL uid, with a hash in
# this private passwd file. No useradd/passwd/PAM/system-service changes are made.
fixture_hash="$(printf '%s' 'ServerDesk-fixture-only-0280!' | openssl passwd -6 -stdin)"
fixture_uid="$(id -u damian)"; fixture_gid="$(id -g damian)"
chown "$fixture_uid:$fixture_gid" "$work/home"
cp /etc/passwd "$work/passwd"; cp /etc/group "$work/group"
printf 'serverdesk_fixture:%s:%s:%s:ServerDesk test:%s:/bin/bash\n' "$fixture_hash" "$fixture_uid" "$fixture_gid" "$work/home" >> "$work/passwd"
printf 'sshd:x:65534:65534:isolated SSH privilege separation:/run/sshd:/usr/sbin/nologin\n' >> "$work/passwd"
chmod 600 "$work/passwd"
cat > "$work/sshd_config" <<EOF
Port 22223
ListenAddress 0.0.0.0
HostKey $work/host_key
PidFile $work/sshd.pid
PasswordAuthentication yes
PubkeyAuthentication no
KbdInteractiveAuthentication no
UsePAM no
PermitRootLogin no
PermitEmptyPasswords no
AllowUsers serverdesk_fixture
AllowTcpForwarding no
X11Forwarding no
PermitUserEnvironment no
Subsystem sftp internal-sftp
LogLevel VERBOSE
EOF
export LD_LIBRARY_PATH="$root/usr/lib/x86_64-linux-gnu:${LD_LIBRARY_PATH:-}"
export LD_PRELOAD="$root/usr/lib/x86_64-linux-gnu/libnss_wrapper.so"
export NSS_WRAPPER_PASSWD="$work/passwd"
export NSS_WRAPPER_GROUP="$work/group"
"$root/usr/sbin/sshd" -f "$work/sshd_config" -E "$work/sshd.log" || { cat "$work/sshd.log"; exit 1; }
echo "Isolated password-only OpenSSH fixture started on 22223, pid $(cat "$work/sshd.pid"); temporary NSS alias, no OS account changes"
hostname -I
