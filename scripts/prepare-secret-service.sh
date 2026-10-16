#!/usr/bin/env bash
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
cd "$repo/.tools/linux-ssh"
mkdir -p secret-packages/partial
apt-get --download-only --no-install-recommends --assume-yes -o Debug::NoLocking=true -o "Dir::State::lists=$(pwd)/lists" -o "Dir::Cache::archives=$(pwd)/secret-packages" -o Dir::Cache::pkgcache= -o Dir::Cache::srcpkgcache= install libsecret-tools gnome-keyring
for package in secret-packages/*.deb; do dpkg-deb -x "$package" root; done
export LD_LIBRARY_PATH="$(pwd)/root/usr/lib/x86_64-linux-gnu:${LD_LIBRARY_PATH:-}"
ldd root/usr/bin/secret-tool
ldd root/usr/bin/gnome-keyring-daemon
