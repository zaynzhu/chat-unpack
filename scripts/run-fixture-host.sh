#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

swift build -c debug --arch arm64 --product ChatUnpackFixtureHost
BIN_DIR="$(swift build -c debug --arch arm64 --show-bin-path)"
CHATUNPACK_FIXTURE_MODE=1 "$BIN_DIR/ChatUnpackFixtureHost"
