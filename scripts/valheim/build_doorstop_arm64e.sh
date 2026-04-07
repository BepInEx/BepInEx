#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
UNITY_DOORSTOP_TAG="${UNITY_DOORSTOP_TAG:-v4.5.0}"
WORKDIR="${1:-${TMPDIR:-/tmp}/UnityDoorstop-arm64e}"

rm -rf "$WORKDIR"
git clone --depth 1 --branch "$UNITY_DOORSTOP_TAG" https://github.com/NeighTools/UnityDoorstop "$WORKDIR"

# Newer Apple Silicon machines can require arm64e for injected dylibs.
perl -0pi -e 's/set_arch\("arm64"\)/set_arch("arm64e")/' "$WORKDIR/xmake.lua"

(
  cd "$WORKDIR"
  ./build.sh
)

OUTPUT_DIR="$WORKDIR/build/macosx/universal/release"
if ! lipo -archs "$OUTPUT_DIR/libdoorstop.dylib" | grep -q 'arm64e'; then
  echo "Expected arm64e slice in $OUTPUT_DIR/libdoorstop.dylib" >&2
  exit 1
fi

echo "$OUTPUT_DIR"
