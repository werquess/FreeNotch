#!/bin/zsh
# Builds a release FreeNotch.app into ./build
# Usage: ./scripts/build_app.sh [--run]
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
APP="$ROOT/build/FreeNotch.app"

cd "$ROOT"
swift build -c release --arch arm64
BIN="$(swift build -c release --arch arm64 --show-bin-path)/FreeNotch"

rm -rf "$APP"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp "$BIN" "$APP/Contents/MacOS/FreeNotch"
cp "$ROOT/Resources/Info.plist" "$APP/Contents/Info.plist"
[ -f "$ROOT/Resources/AppIcon.icns" ] && cp "$ROOT/Resources/AppIcon.icns" "$APP/Contents/Resources/"
for lproj in "$ROOT"/Resources/Localization/*.lproj(N); do
    cp -R "$lproj" "$APP/Contents/Resources/"
done

IDENTITY="${CODESIGN_IDENTITY:--}"
if [[ "$IDENTITY" == "-" ]]; then
    BUNDLE_ID="$(/usr/libexec/PlistBuddy -c "Print CFBundleIdentifier" "$APP/Contents/Info.plist")"
    codesign --force --sign - --timestamp=none \
        --requirements "=designated => identifier \"$BUNDLE_ID\"" "$APP"
else
    codesign --force --sign "$IDENTITY" --timestamp=none "$APP"
fi
echo "Built $APP"

if [[ "${1:-}" == "--run" ]]; then
    pkill -x FreeNotch 2>/dev/null || true
    sleep 0.5
    open "$APP"
fi
