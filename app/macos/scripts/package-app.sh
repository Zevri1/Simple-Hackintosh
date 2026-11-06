#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
BUILD_DIR="$PROJECT_DIR/.build"
BINARY="$BUILD_DIR/release/SimpleHackintoshMacApp"
APP_DIR="$BUILD_DIR/packaged/Simple Hackintosh.app"
ARCH="$(uname -m)"
ZIP_PATH="$BUILD_DIR/packaged/Simple-Hackintosh-macOS-0.1.0-$ARCH.zip"

if [[ "$(uname -s)" != "Darwin" ]]; then
  echo "This packaging script must run on macOS." >&2
  exit 2
fi

swift build --package-path "$PROJECT_DIR" -c release
[[ -x "$BINARY" ]] || { echo "App executable was not produced: $BINARY" >&2; exit 1; }

rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"
cp "$BINARY" "$APP_DIR/Contents/MacOS/SimpleHackintoshMacApp"
cat > "$APP_DIR/Contents/Info.plist" <<'PLIST'
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDisplayName</key><string>Simple Hackintosh</string>
    <key>CFBundleExecutable</key><string>SimpleHackintoshMacApp</string>
    <key>CFBundleIdentifier</key><string>com.zevri1.simplehackintosh</string>
    <key>CFBundleName</key><string>Simple Hackintosh</string>
    <key>CFBundlePackageType</key><string>APPL</string>
    <key>CFBundleShortVersionString</key><string>0.1.0</string>
    <key>CFBundleVersion</key><string>0.1.0</string>
    <key>LSMinimumSystemVersion</key><string>13.0</string>
    <key>NSHighResolutionCapable</key><true/>
    <key>NSPrincipalClass</key><string>NSApplication</string>
</dict>
</plist>
PLIST

codesign --force --deep --sign - "$APP_DIR"
mkdir -p "$(dirname "$ZIP_PATH")"
rm -f "$ZIP_PATH"
ditto -c -k --sequesterRsrc --keepParent "$APP_DIR" "$ZIP_PATH"
echo "Created: $APP_DIR"
echo "Created: $ZIP_PATH"
