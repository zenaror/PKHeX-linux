#!/usr/bin/env bash
# Builds a single-file AppImage from the self-contained Linux publish.
#
# usage: build-appimage.sh [output-dir]
#
# appimagetool is not vendored here. Provide it in one of these ways:
#   APPIMAGETOOL=/path/to/appimagetool-x86_64.AppImage build-appimage.sh
#   appimagetool on PATH
#   build-appimage.sh --download-tool        (fetches it into ~/.cache/pkhex-packaging)
#
# The AppImage is read only, so the program runs in its XDG layout: settings in
# $XDG_CONFIG_HOME/PKHeX and the local resource folders (pkmdb, bak, template, plugins) in
# $XDG_DATA_HOME/PKHeX. Plugins therefore live in ~/.local/share/PKHeX/plugins and keep working.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CACHE="${XDG_CACHE_HOME:-$HOME/.cache}/pkhex-packaging"
TOOL_URL="https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"

OUT=""
DOWNLOAD=false
for arg in "$@"; do
  case "$arg" in
    --download-tool) DOWNLOAD=true ;;
    -*) echo "unknown option: $arg" >&2; exit 1 ;;
    *) OUT="$arg" ;;
  esac
done
OUT="${OUT:-$ROOT/publish/appimage}"

find_tool() {
  if [[ -n "${APPIMAGETOOL:-}" ]]; then
    echo "$APPIMAGETOOL"; return 0
  fi
  if command -v appimagetool >/dev/null 2>&1; then
    command -v appimagetool; return 0
  fi
  if [[ -x "$CACHE/appimagetool" ]]; then
    echo "$CACHE/appimagetool"; return 0
  fi
  if $DOWNLOAD; then
    mkdir -p "$CACHE"
    echo "Downloading appimagetool from $TOOL_URL" >&2
    curl -fsSL "$TOOL_URL" -o "$CACHE/appimagetool"
    chmod +x "$CACHE/appimagetool"
    echo "$CACHE/appimagetool"; return 0
  fi
  return 1
}

if ! TOOL="$(find_tool)"; then
  cat >&2 <<EOF
appimagetool not found.

Install it, point APPIMAGETOOL at it, or re-run with --download-tool:
  $TOOL_URL
EOF
  exit 1
fi

APPDIR="$OUT/PKHeX.AppDir"
rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/bin"

# The payload is the normal self-contained publish; no .NET runtime is needed on the target machine.
dotnet publish "$ROOT/PKHeX.Avalonia/PKHeX.Avalonia.csproj" \
  -c Release -r linux-x64 --self-contained true -o "$APPDIR/usr/bin"

# Desktop integration: appimagetool wants the .desktop and the icon at the AppDir root,
# and desktop environments read the copies under usr/share.
install -Dm644 "$ROOT/PKHeX.Avalonia/Packaging/pkhex.desktop" "$APPDIR/usr/share/applications/pkhex.desktop"
install -Dm644 "$ROOT/icon.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/pkhex.png"
cp "$APPDIR/usr/share/applications/pkhex.desktop" "$APPDIR/pkhex.desktop"
cp "$ROOT/icon.png" "$APPDIR/pkhex.png"
cp "$ROOT/icon.png" "$APPDIR/.DirIcon"

cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/sh
# Entry point of the AppImage: run the published binary from inside the mounted image.
HERE="$(dirname "$(readlink -f "$0")")"
exec "$HERE/usr/bin/PKHeX.Avalonia" "$@"
EOF
chmod +x "$APPDIR/AppRun"

VERSION="$(grep -oP '(?<=<Version>)[^<]+' "$ROOT/PKHeX.Avalonia/PKHeX.Avalonia.csproj" | head -1 || true)"
export ARCH=x86_64
[[ -n "$VERSION" ]] && export VERSION

# The tool itself is an AppImage; extract-and-run works without FUSE being available.
"$TOOL" --appimage-extract-and-run --no-appstream "$APPDIR" "$OUT/PKHeX-x86_64.AppImage"

cat <<EOF

AppImage: $OUT/PKHeX-x86_64.AppImage
Run with: chmod +x PKHeX-x86_64.AppImage && ./PKHeX-x86_64.AppImage

Settings go to \${XDG_CONFIG_HOME:-~/.config}/PKHeX/cfg.json and the local resource folders to
\${XDG_DATA_HOME:-~/.local/share}/PKHeX. Plugins are read from
\${XDG_DATA_HOME:-~/.local/share}/PKHeX/plugins, which the AppImage cannot contain: drop the
plugin .dll there.
EOF
