#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_DIR="$ROOT_DIR/dist/ChatUnpack.app"
EXECUTABLE="$APP_DIR/Contents/MacOS/ChatUnpackApp"

if [[ ! -d "$APP_DIR" || ! -x "$EXECUTABLE" ]]; then
  echo "未找到 dist/ChatUnpack.app，请先运行 scripts/build-app.sh" >&2
  exit 1
fi

EXPECTED_IDENTIFIER="com.zaynzhu.ChatUnpack"
ACTUAL_IDENTIFIER="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$APP_DIR/Contents/Info.plist")"
if [[ "$ACTUAL_IDENTIFIER" != "$EXPECTED_IDENTIFIER" ]]; then
  echo "Bundle identifier 不符合预期：$ACTUAL_IDENTIFIER" >&2
  exit 1
fi

file "$EXECUTABLE"
ARCHITECTURES="$(lipo -archs "$EXECUTABLE")"
if [[ "$ARCHITECTURES" != "arm64" ]]; then
  echo "可执行文件架构不符合预期：$ARCHITECTURES" >&2
  exit 1
fi

MINIMUM_SYSTEM="$(/usr/libexec/PlistBuddy -c 'Print :LSMinimumSystemVersion' "$APP_DIR/Contents/Info.plist")"
if [[ "$MINIMUM_SYSTEM" != "13.0" ]]; then
  echo "最低系统版本不符合预期：$MINIMUM_SYSTEM" >&2
  exit 1
fi

SHORT_VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleShortVersionString' "$APP_DIR/Contents/Info.plist")"
BUILD_VERSION="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleVersion' "$APP_DIR/Contents/Info.plist")"
if [[ "$SHORT_VERSION" != "0.1.9" || "$BUILD_VERSION" != "10" ]]; then
  echo "应用版本不符合预期：$SHORT_VERSION ($BUILD_VERSION)" >&2
  exit 1
fi

ICON_FILE="$APP_DIR/Contents/Resources/AppIcon.icns"
if [[ ! -s "$ICON_FILE" ]]; then
  echo "应用图标缺失或为空" >&2
  exit 1
fi
ICON_NAME="$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIconFile' "$APP_DIR/Contents/Info.plist")"
if [[ "$ICON_NAME" != "AppIcon" ]]; then
  echo "应用图标配置不符合预期：$ICON_NAME" >&2
  exit 1
fi

codesign --verify --deep --strict "$APP_DIR"
SIGNING_INFORMATION="$(codesign -d --verbose=4 "$APP_DIR" 2>&1)"
if ! grep -F 'Authority=ChatUnpack Local Signing' <<<"$SIGNING_INFORMATION" >/dev/null; then
  echo "应用未使用 ChatUnpack 专用签名身份" >&2
  exit 1
fi

DESIGNATED_REQUIREMENT="$(codesign -d -r- "$APP_DIR" 2>&1)"
if grep -F 'cdhash' <<<"$DESIGNATED_REQUIREMENT" >/dev/null; then
  echo "应用仍在使用会随构建变化的 cdhash 身份" >&2
  exit 1
fi
echo "ChatUnpack.app 校验通过"
