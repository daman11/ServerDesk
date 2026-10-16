#!/usr/bin/env bash
set -euo pipefail
repo="$(cd "$(dirname "$0")/.." && pwd)"
work="$repo/.tools/terminal-test-apps"
mkdir -p "$work/downloads" "$work/root"
cd "$work/downloads"
# Download/extract only. No apt install, system configuration or user dotfiles.
apt-get download htop mc mc-data zsh zsh-common fish fish-common libnl-genl-3-200 libnl-3-200 libgpm2 libslang2 libpcre2-32-0 libssh2-1t64
: > "$work/PACKAGES.txt"
for package in ./*.deb; do
  dpkg-deb -f "$package" Package Version >> "$work/PACKAGES.txt"
  dpkg-deb -x "$package" "$work/root"
done
cat "$work/PACKAGES.txt"
echo 'Prepared test-only terminal applications under .tools; no packages installed into WSL'
