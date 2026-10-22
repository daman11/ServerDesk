#!/usr/bin/env bash
# Opt-in disposable loopback OpenSSH fixture. No real OS account or user keys.
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
root="$repo/.tools/linux-ssh/root"
work=/tmp/serverdesk-vault-ssh-test
out="$repo/.tools/linux-ssh/vault-key-fixture"
fixture_os_user="${SERVERDESK_TEST_OS_USER:-${SUDO_USER:-$(id -un)}}"
fixture_uid="$(id -u "$fixture_os_user")"; fixture_gid="$(id -g "$fixture_os_user")"
[[ "$fixture_uid" != 0 ]] || { echo 'Select a non-root fixture OS identity.' >&2; exit 1; }
mkdir -p "$work/home" "$out" /run/sshd
if [ -f "$work/sshd.pid" ] && kill -0 "$(cat "$work/sshd.pid")" 2>/dev/null; then echo 'Owned vault SSH fixture already running.' >&2; exit 1; fi
chmod 755 "$work" "$work/home"; chown "$fixture_uid:$fixture_gid" "$work/home"
for name in host ed25519 rsa; do
  type=ed25519; [[ "$name" != rsa ]] || type=rsa
  if [ ! -f "$work/$name.key" ]; then ssh-keygen -q -t "$type" -N '' -f "$work/$name.key"; fi
done
cat "$work/ed25519.key.pub" "$work/rsa.key.pub" > "$work/authorized_keys"
chmod 600 "$work/host.key" "$work/ed25519.key" "$work/rsa.key"; chmod 644 "$work/authorized_keys"
cp "$work/ed25519.key" "$out/ed25519.key"; cp "$work/rsa.key" "$out/rsa.key"
cp /etc/passwd "$work/passwd"; cp /etc/group "$work/group"
printf 'serverdesk_fixture:x:%s:%s:ServerDesk vault key fixture:%s:/bin/bash\n' "$fixture_uid" "$fixture_gid" "$work/home" >> "$work/passwd"
printf 'sshd:x:65534:65534:isolated SSH privilege separation:/run/sshd:/usr/sbin/nologin\n' >> "$work/passwd"
cat > "$work/sshd_config" <<EOF
Port 22224
ListenAddress 127.0.0.1
HostKey $work/host.key
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
fingerprint="$(ssh-keygen -lf "$work/host.key.pub" -E sha256 | awk '{print $2}')"
printf '{"host":"127.0.0.1","port":22224,"fingerprint":"%s","remoteHome":"%s/home"}\n' "$fingerprint" "$work" > "$out/fixture.json"
export LD_LIBRARY_PATH="$root/usr/lib/x86_64-linux-gnu:${LD_LIBRARY_PATH:-}"
export LD_PRELOAD="$root/usr/lib/x86_64-linux-gnu/libnss_wrapper.so"
export NSS_WRAPPER_PASSWD="$work/passwd" NSS_WRAPPER_GROUP="$work/group"
"$root/usr/sbin/sshd" -f "$work/sshd_config" -E "$work/sshd.log" || { cat "$work/sshd.log"; exit 1; }
echo 'Disposable vault-key OpenSSH fixture started on 127.0.0.1:22224; test keys only, no OS account changes.'
