#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/../.tools/linux-ssh"
mkdir -p lists/partial
apt-get -o "Dir::State::lists=$(pwd)/lists" -o "Dir::Cache::pkgcache=" -o "Dir::Cache::srcpkgcache=" update
apt-get -o "Dir::State::lists=$(pwd)/lists" download openssh-server libwrap0 libnss-wrapper
for package in ./*.deb; do dpkg-deb -x "$package" root; done
ldd root/usr/sbin/sshd
