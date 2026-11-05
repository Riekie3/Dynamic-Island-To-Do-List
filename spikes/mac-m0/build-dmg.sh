#!/bin/bash
# Phase M0: builds the throwaway test app into an ad-hoc signed .dmg. Runs on macOS only (CI).
# usage: build-dmg.sh osx-arm64|osx-x64 output-folder
set -euo pipefail

RID="$1"
OUT="$2"
HERE="$(cd "$(dirname "$0")" && pwd)"
WORK="$(mktemp -d)"
APP="$WORK/stage/TaskIsland Spike.app"

dotnet publish "$HERE/MacSpike.csproj" -c Release -r "$RID" --self-contained -o "$WORK/pub"

mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
cp -R "$WORK/pub/." "$APP/Contents/MacOS/"
cp "$HERE/Info.plist" "$APP/Contents/Info.plist"

# Apple Silicon refuses unsigned code, so sign the native libraries first and then the bundle (ad-hoc: no certificate).
find "$APP/Contents/MacOS" -type f \( -name '*.dylib' -o -name 'TaskIslandSpike' \) -exec codesign --force --sign - {} \;
codesign --force --sign - "$APP"
codesign --verify --strict --verbose=2 "$APP"

ln -s /Applications "$WORK/stage/Applications"
mkdir -p "$OUT"
DMG="$OUT/TaskIsland-Spike-$RID.dmg"
hdiutil create -volname "TaskIsland Spike" -srcfolder "$WORK/stage" -ov -format UDZO "$DMG"
shasum -a 256 "$DMG"
