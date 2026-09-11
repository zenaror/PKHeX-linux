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
install -Dm755 "$ROOT/PKHeX.Avalonia/Packaging/install.sh" "$OUT/install.sh"

echo "Published to: $OUT"
echo "Run with:     $OUT/PKHeX.Avalonia"
echo
echo "To add the icon and the menu entry for the current user:"
echo "  $OUT/install.sh"
