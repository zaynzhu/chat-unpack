#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST_DIR="$ROOT_DIR/dist"
APP_DIR="$DIST_DIR/ChatUnpack.app"

cd "$ROOT_DIR"
swift build -c release --arch arm64 --product ChatUnpackApp
BIN_DIR="$(swift build -c release --arch arm64 --show-bin-path)"

rm -rf "$APP_DIR"
mkdir -p "$APP_DIR/Contents/MacOS" "$APP_DIR/Contents/Resources"
cp "$BIN_DIR/ChatUnpackApp" "$APP_DIR/Contents/MacOS/ChatUnpackApp"
cp "$ROOT_DIR/Resources/Info.plist" "$APP_DIR/Contents/Info.plist"

codesign --force --deep --sign - "$APP_DIR"

echo "已生成：$APP_DIR"
file "$APP_DIR/Contents/MacOS/ChatUnpackApp"
codesign --verify --deep --strict "$APP_DIR"
echo "签名校验通过（ad-hoc）"
