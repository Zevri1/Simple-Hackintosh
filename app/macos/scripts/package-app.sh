#!/bin/bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
ARM_BIN="$ROOT/.build/arm64-apple-macosx/release/SimpleHackintoshStudio"
INTEL_BIN="$ROOT/.build/x86_64-apple-macosx/release/SimpleHackintoshStudio"
INFO_PLIST="$ROOT/Resources/Info.plist"
DIST="$ROOT/dist"
APP="$DIST/Simple Hackintosh Studio.app"
UNIVERSAL_BIN="$APP/Contents/MacOS/SimpleHackintoshStudio"

for binary in "$ARM_BIN" "$INTEL_BIN"; do
  if [[ ! -x "$binary" ]]; then
    echo "Missing architecture build: $binary" >&2
    exit 1
  fi
done
if [[ ! -f "$INFO_PLIST" ]]; then
  echo "Missing Info.plist: $INFO_PLIST" >&2
  exit 1
fi

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS"
lipo -create "$ARM_BIN" "$INTEL_BIN" -output "$UNIVERSAL_BIN"
cp "$INFO_PLIST" "$APP/Contents/Info.plist"
/usr/bin/plutil -lint "$APP/Contents/Info.plist"
lipo -info "$UNIVERSAL_BIN"

mkdir -p "$DIST"
ditto -c -k --sequesterRsrc --keepParent "$APP" "$DIST/Simple-Hackintosh-Studio-macos.zip"
echo "Created universal app: $DIST/Simple-Hackintosh-Studio-macos.zip"
