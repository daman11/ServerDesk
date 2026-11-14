#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
version="$(sed -n 's/.*<Version>\([^<]*\)<\/Version>.*/\1/p' Directory.Build.props)"
if (( $# )) && [[ "$1" != --* ]]; then version="$1"; shift; fi
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo 'Invalid version' >&2; exit 1; }
payload="artifacts/linux-x64-$version"
if (( $# )) && [[ "$1" != --* ]]; then payload="$1"; shift; fi
output=artifacts; revision=""; deb_only=false
while (( $# )); do
    case "$1" in
        --output-dir) (( $# >= 2 )) || { echo '--output-dir requires a path' >&2; exit 2; }; output="$2"; shift 2;;
        --revision) (( $# >= 2 )) || { echo '--revision requires a positive integer' >&2; exit 2; }; revision="$2"; [[ "$revision" =~ ^[1-9][0-9]*$ ]] || { echo 'Invalid Debian revision' >&2; exit 2; }; shift 2;;
        --deb-only) deb_only=true; shift;;
        *) echo 'Usage: package-deb.sh [VERSION [PAYLOAD]] [--output-dir PATH] [--revision N] [--deb-only]' >&2; exit 2;;
    esac
done
package_version="$version${revision:+-$revision}"
output="$(realpath -m -- "$output")"
frozen="$(realpath -m -- artifacts/releases)"
[[ "$output" != "$frozen" && "$output" != "$frozen/"* ]] || { echo 'Never write directly into frozen releases' >&2; exit 2; }
mkdir -p -- "$output"
test -f "$payload/ServerDesk.App"
python3 -c 'import json,sys; d=json.load(open(sys.argv[1])); assert d["version"]==sys.argv[2] and d["runtime"]=="linux-x64"' "$payload/RELEASE.json" "$version"
package="$(mktemp -d /tmp/serverdesk-deb.XXXXXX)"
trap 'case "$package" in /tmp/serverdesk-deb.*) rm -rf -- "$package";; esac' EXIT
mkdir -p "$package/DEBIAN" "$package/opt/serverdesk" "$package/usr/bin" "$package/usr/share/applications" "$package/usr/share/icons/hicolor/scalable/apps"
cp -a "$payload/." "$package/opt/serverdesk/"
chmod +x "$package/opt/serverdesk/ServerDesk.App"
printf '#!/bin/sh\nexec /opt/serverdesk/ServerDesk.App "$@"\n' > "$package/usr/bin/serverdesk"
chmod +x "$package/usr/bin/serverdesk"
cp packaging/linux/serverdesk.desktop "$package/usr/share/applications/"
cp packaging/linux/serverdesk.svg "$package/usr/share/icons/hicolor/scalable/apps/"
cat > "$package/DEBIAN/control" <<EOF
Package: serverdesk
Version: $package_version
Architecture: amd64
Maintainer: ServerDesk Project
Section: net
Priority: optional
Depends: libc6 (>= 2.35), libstdc++6, libgcc-s1, libfontconfig1, libx11-6, libxcb1, libxext6, libxrender1, libice6, libsm6, libxrandr2, libxi6, libxcursor1, libssl3 | libssl3t64, libicu78 | libicu77 | libicu76 | libicu75 | libicu74 | libicu73 | libicu72 | libicu70
Recommends: libsecret-tools, iputils-ping, freerdp3-x11 | freerdp2-x11
Description: Server administration workspace (public beta)
 SSH, SFTP, monitoring, VNC, serial sessions and Network Tools in Avalonia.
 Linux RDP is hosted in a ServerDesk tab using X11 FreeRDP. MIT licensed.
EOF
find "$package" -type d -exec chmod 755 {} +
find "$package" -type f -exec chmod 644 {} +
chmod 755 "$package/opt/serverdesk/ServerDesk.App" "$package/usr/bin/serverdesk"
chmod 755 "$package/opt/serverdesk/components/bitwarden/bw"
dpkg-deb --root-owner-group --build "$package" "$output/serverdesk_${package_version}_amd64.deb"
if [[ "$deb_only" != true ]]; then tar -czf "$output/ServerDesk-$version-linux-x64.tar.gz" -C "$package/opt/serverdesk" .; fi
