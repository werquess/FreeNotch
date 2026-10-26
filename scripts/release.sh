#!/bin/zsh
# Builds FreeNotch and packages it for a GitHub release:
#   dist/FreeNotch.dmg  - drag-to-Applications disk image (README download link)
#   dist/FreeNotch.zip  - used by the in-app updater
# Usage: ./scripts/release.sh 1.0.1
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
VERSION="${1:-}"
cd "$ROOT"

if [[ -n "$VERSION" ]]; then
    /usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $VERSION" Resources/Info.plist
    BUILD=$(/usr/libexec/PlistBuddy -c "Print :CFBundleVersion" Resources/Info.plist)
    /usr/libexec/PlistBuddy -c "Set :CFBundleVersion $((BUILD + 1))" Resources/Info.plist
fi

./scripts/build_app.sh

rm -rf dist && mkdir -p dist/dmg
ditto -c -k --keepParent build/FreeNotch.app dist/FreeNotch.zip

cp -R build/FreeNotch.app dist/dmg/
ln -s /Applications dist/dmg/Applications
hdiutil create -volname FreeNotch -srcfolder dist/dmg -ov -format UDZO dist/FreeNotch.dmg >/dev/null
rm -rf dist/dmg

echo "Release files:"
ls -lh dist
echo
echo "Publish with:"
echo "  gh release create v${VERSION:-X.Y.Z} dist/FreeNotch.dmg dist/FreeNotch.zip --title \"FreeNotch ${VERSION:-X.Y.Z}\" --notes-file CHANGELOG.md"
