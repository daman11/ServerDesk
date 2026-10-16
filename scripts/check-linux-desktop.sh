#!/usr/bin/env bash
# Read-only preflight. Never install packages, unlock wallets or inspect saved secrets.
set -euo pipefail
if [[ "$(uname -s)" != Linux ]]; then echo 'Run this check in the Linux desktop session.' >&2; exit 2; fi
if (( $# > 1 )); then echo 'Usage: check-linux-desktop.sh [path/to/ServerDesk.App]' >&2; exit 2; fi
printf 'ServerDesk Linux desktop preflight (not an acceptance certificate)\n'
if [[ -r /etc/os-release ]]; then sed -n '/^PRETTY_NAME=/p' /etc/os-release; fi
printf 'Architecture: %s\nDesktop: %s\nSession: %s\n' "$(uname -m)" "${XDG_CURRENT_DESKTOP:-unknown}" "${XDG_SESSION_TYPE:-unknown}"
getconf GNU_LIBC_VERSION 2>/dev/null || true
present() { if [[ -n "${!1:-}" ]]; then printf '%s: present\n' "$1"; else printf '%s: absent\n' "$1"; fi; }
present DISPLAY; present WAYLAND_DISPLAY; present DBUS_SESSION_BUS_ADDRESS
printf 'Backend: this build uses Avalonia X11, including XWayland in a Wayland session.\n'
if [[ -z "${DISPLAY:-}" ]]; then printf 'CHECK: no X11 display available; this build needs X11/XWayland.\n'; fi
for tool in secret-tool busctl gdbus xfreerdp3 xfreerdp; do
    if command -v "$tool" >/dev/null 2>&1; then printf '%s: available\n' "$tool"; else printf '%s: not on PATH\n' "$tool"; fi
done
if [[ -n "${DBUS_SESSION_BUS_ADDRESS:-}" ]] && command -v busctl >/dev/null 2>&1; then
    if names="$(busctl --user --timeout=3 --no-pager --no-legend --acquired list 2>/dev/null)"; then
        if awk '{print $1}' <<< "$names" | grep -Fx org.freedesktop.secrets >/dev/null; then
            printf 'Secret Service: owned on session bus (locked state not inspected).\n'
        else printf 'Secret Service: not currently owned; activation/unlock not attempted.\n'; fi
    else printf 'Secret Service: session bus could not be queried.\n'; fi
else printf 'Secret Service: not checked; busctl/session bus unavailable.\n'; fi
if command -v ldconfig >/dev/null 2>&1; then
    libraries="$(ldconfig -p 2>/dev/null || true)"
    for library in libfontconfig.so.1 libICE.so.6 libSM.so.6 libX11.so.6 libXext.so.6 libXrender.so.1 libXrandr.so.2 libXi.so.6 libXcursor.so.1; do
        if awk '{print $1}' <<< "$libraries" | grep -Fx "$library" >/dev/null; then printf '%s: cached\n' "$library"; else printf '%s: CHECK system packages or LD_LIBRARY_PATH\n' "$library"; fi
    done
fi
if (( $# == 1 )); then
    app="$1"
    if [[ ! -f "$app" || ! -x "$app" ]]; then echo 'App is missing or not executable; copy the complete Linux build and preserve executable permissions.' >&2; exit 1; fi
    app_dir="$(cd -- "$(dirname -- "$app")" && pwd)"
    for library in libSkiaSharp.so libHarfBuzzSharp.so libe_sqlite3.so; do
        if [[ ! -f "$app_dir/$library" ]]; then printf '%s: absent from app directory\n' "$library"; continue; fi
        if command -v ldd >/dev/null 2>&1; then
            links="$(ldd "$app_dir/$library" 2>&1 || true)"
            if grep -Fq 'not found' <<< "$links"; then printf '%s: unresolved dependencies\n%s\n' "$library" "$links"; else printf '%s: no unresolved ldd dependencies\n' "$library"; fi
        fi
    done
fi
printf 'Manual checks still required: window/input/clipboard, file pickers, wallet unlocked/locked, DPI and long sessions.\n'
