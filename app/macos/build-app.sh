#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
APP_ROOT="$SCRIPT_DIR"
OUTPUT_DIR="$SCRIPT_DIR/../../dist/macos"
MIST_BIN=""

if [[ "$#" -ge 1 ]]; then OUTPUT_DIR="$1"; fi
if [[ "$#" -ge 2 ]]; then MIST_BIN="$2"; fi

if [[ -z "$MIST_BIN" || ! -x "$MIST_BIN" ]]; then
    echo "Usage: $0 <output-directory> <path-to-built-mist-cli>" >&2
    exit 2
fi

swift build --package-path "$APP_ROOT" --configuration release --arch arm64 --arch x86_64

BINARY="$APP_ROOT/.build/apple/Products/release/SimpleHackintoshMac"
if [[ ! -x "$BINARY" ]]; then
    echo "Swift package build did not produce the expected universal executable: $BINARY" >&2
    exit 1
fi

APP="$OUTPUT_DIR/Simple Hackintosh Studio.app"
ZIP="$OUTPUT_DIR/Simple-Hackintosh-Studio-macOS-universal.zip"

mkdir -p "$OUTPUT_DIR"
rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Helpers" "$APP/Contents/Resources"

cp "$BINARY" "$APP/Contents/MacOS/SimpleHackintoshMac"
cp "$MIST_BIN" "$APP/Contents/Helpers/mist"
chmod 755 "$APP/Contents/MacOS/SimpleHackintoshMac" "$APP/Contents/Helpers/mist"
cp "$APP_ROOT/THIRD_PARTY_NOTICES.md" "$APP/Contents/Resources/THIRD_PARTY_NOTICES.md"

cat > "$APP/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>SimpleHackintoshMac</string>
    <key>CFBundleIdentifier</key>
    <string>dev.zevri1.simplehackintosh.studio</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>Simple Hackintosh Studio</string>
    <key>CFBundlePackageType</key>
    <string>APPL</string>
    <key>CFBundleShortVersionString</key>
    <string>0.1.0</string>
    <key>CFBundleVersion</key>
    <string>0.1.0</string>
    <key>LSMinimumSystemVersion</key>
    <string>13.0</string>
    <key>NSHighResolutionCapable</key>
    <true/>
    <key>NSPrincipalClass</key>
    <string>NSApplication</string>
</dict>
</plist>
PLIST

/usr/libexec/PlistBuddy -c "Print :CFBundleIdentifier" "$APP/Contents/Info.plist" >/dev/null
codesign --force --sign - "$APP/Contents/Helpers/mist"
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"
rm -f "$ZIP"
ditto -c -k --sequesterRsrc --keepParent "$APP" "$ZIP"
shasum -a 256 "$ZIP" > "$ZIP.sha256"

echo "Created: $APP"
echo "Created: $ZIP"
cat "$ZIP.sha256"
