#!/usr/bin/env bash
# Registers this published folder with the desktop: icon, menu entry, and the file manager icon on the binary.
#
# Run it from inside the published folder (or give the folder as the first argument):
#   ./install.sh
#
# Everything is written under the current user's home; nothing needs root and nothing is copied out of this folder,
# so the program can stay wherever it was extracted. Run uninstall with --remove.
set -euo pipefail

DIR="$(cd "${1:-$(dirname "${BASH_SOURCE[0]}")}" && pwd)"
BIN="$DIR/PKHeX.Avalonia"
ICON_SRC="$DIR/pkhex.png"

APPS="${XDG_DATA_HOME:-$HOME/.local/share}/applications"
ICONS="${XDG_DATA_HOME:-$HOME/.local/share}/icons/hicolor/64x64/apps"
DESKTOP="$APPS/pkhex.desktop"
ICON_DST="$ICONS/pkhex.png"

if [[ "${1:-}" == "--remove" ]]; then
  rm -f "$DESKTOP" "$ICON_DST"
  command -v update-desktop-database >/dev/null && update-desktop-database "$APPS" || true
  echo "Removed the menu entry and icon."
  exit 0
fi

[[ -x "$BIN" ]] || { echo "PKHeX.Avalonia not found or not executable in $DIR" >&2; exit 1; }

install -Dm644 "$ICON_SRC" "$ICON_DST"
install -Dm644 "$DIR/pkhex.desktop" "$DESKTOP"
# Point the entry at this copy of the program.
sed -i "s|^Exec=.*|Exec=$BIN %f|" "$DESKTOP"

command -v update-desktop-database >/dev/null && update-desktop-database "$APPS" || true
command -v gtk-update-icon-cache >/dev/null && gtk-update-icon-cache -q -t -f "${ICONS%/64x64/apps}" 2>/dev/null || true

# ELF binaries carry no icon of their own; this is what makes the file manager show one on the executable.
command -v gio >/dev/null && gio set "$BIN" metadata::custom-icon "file://$ICON_DST" 2>/dev/null || true

echo "Installed the menu entry for: $BIN"
echo "Look for PKHeX in the application menu, or run the binary directly."
