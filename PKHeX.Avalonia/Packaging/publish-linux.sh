#!/usr/bin/env bash
# Publishes the Linux build and lays out a runnable folder (binary, icon, .desktop entry).
#
# usage: publish-linux.sh [output-dir] [--self-contained|--framework-dependent]
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
OUT="${1:-$ROOT/publish/linux-x64}"
MODE="${2:---self-contained}"

case "$MODE" in
  --self-contained)      SC=true  ;;
  --framework-dependent) SC=false ;;
  *) echo "unknown mode: $MODE" >&2; exit 1 ;;
esac

dotnet publish "$ROOT/PKHeX.Avalonia/PKHeX.Avalonia.csproj" \
  -c Release -r linux-x64 --self-contained "$SC" -o "$OUT"

install -Dm644 "$ROOT/icon.png" "$OUT/pkhex.png"
install -Dm644 "$ROOT/PKHeX.Avalonia/Packaging/pkhex.desktop" "$OUT/pkhex.desktop"

echo "Published to: $OUT"
echo "Run with:     $OUT/PKHeX.Avalonia"
echo
echo "To install the desktop entry for the current user:"
echo "  install -Dm644 $OUT/pkhex.png  ~/.local/share/icons/hicolor/128x128/apps/pkhex.png"
echo "  sed \"s|^Exec=.*|Exec=$OUT/PKHeX.Avalonia %f|\" $OUT/pkhex.desktop > ~/.local/share/applications/pkhex.desktop"
