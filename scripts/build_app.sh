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

SIGNING_DIR="${FREENOTCH_SIGNING_DIR:-$HOME/.freenotch-signing}"
KEYCHAIN="$SIGNING_DIR/signing.keychain-db"
if [[ -n "${CODESIGN_IDENTITY:-}" ]]; then
    codesign --force --sign "$CODESIGN_IDENTITY" --timestamp=none "$APP"
elif [[ -f "$KEYCHAIN" && -f "$SIGNING_DIR/password" ]]; then
    security unlock-keychain -p "$(cat "$SIGNING_DIR/password")" "$KEYCHAIN"
    codesign --force --keychain "$KEYCHAIN" --sign "FreeNotch Self-Signed" --timestamp=none "$APP"
else
    codesign --force --sign - --timestamp=none "$APP"
fi
echo "Built $APP"

if [[ "${1:-}" == "--run" ]]; then
    pkill -x FreeNotch 2>/dev/null || true
    sleep 0.5
    open "$APP"
fi
