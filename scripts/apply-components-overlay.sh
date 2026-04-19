#!/bin/bash
set -e
set -u

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
TARGET_CS2_DIR="${1:-$ROOT_DIR/server/game/csgo}"

mkdir -p "$TARGET_CS2_DIR"

rm -rf "$TARGET_CS2_DIR/addons"
rm -rf "$TARGET_CS2_DIR/cfg/settings"
cp -r "$ROOT_DIR/components/csgo/." "$TARGET_CS2_DIR/"

if [ -d "$ROOT_DIR/components/csgo/addons/linux" ]; then
    cp -r "$ROOT_DIR/components/csgo/addons/linux/." "$TARGET_CS2_DIR/"
fi
