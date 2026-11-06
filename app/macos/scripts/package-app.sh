#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BUILD_BIN="$ROOT/.build/release/SimpleHackintoshStudio"
INFO_PLIST="$ROOT/Resources/Info.plist"
DIST="$ROOT/dist"
APP="$DIST/Simple Hackintosh Studio.app"

if [[ ! -x "$BUILD_BIN" ]]; then
  echo "Missing release executable: $BUILD_BIN" >&2
  exit 1
fi
if [[ ! -f "$INFO_PLIST" ]]; then
  echo "Missing Info.plist: $INFO_PLIST" >&2
  exit 1
fi

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS"
cp "$BUILD_BIN" "$APP/Contents/MacOS/SimpleHackintoshStudio"
cp "$INFO_PLIST" "$APP/Contents/Info.plist"
/usr/bin/plutil -lint "$APP/Contents/Info.plist"

mkdir -p "$DIST"
ditto -c -k --sequesterRsrc --keepParent "$APP" "$DIST/Simple-Hackintosh-Studio-macos.zip"
echo "Created: $DIST/Simple-Hackintosh-Studio-macos.zip"
