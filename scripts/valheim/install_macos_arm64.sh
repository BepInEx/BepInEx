#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
DEFAULT_APP="/Users/$USER/Library/Application Support/Steam/steamapps/common/Valheim/valheim.app"
APP_PATH="$DEFAULT_APP"
DOORSTOP_DIR=""
MODS_DIR=""
WITH_SMOKE_TESTS=0
STAGE_DIR="${TMPDIR:-/tmp}/bepinex-valheim-macos-arm64"

usage() {
  cat <<'EOF'
Usage:
  install_macos_arm64.sh [path/to/valheim.app] [--doorstop-dir DIR] [--mods-dir DIR] [--with-smoke-tests]

Examples:
  scripts/valheim/install_macos_arm64.sh
  scripts/valheim/install_macos_arm64.sh "/Users/me/Library/Application Support/Steam/steamapps/common/Valheim/valheim.app" --with-smoke-tests
  scripts/valheim/install_macos_arm64.sh --mods-dir /path/to/exported/BepInEx/plugins
EOF
}

while [ $# -gt 0 ]; do
  case "$1" in
    --doorstop-dir)
      DOORSTOP_DIR="$2"
      shift 2
      ;;
    --mods-dir)
      MODS_DIR="$2"
      shift 2
      ;;
    --with-smoke-tests)
      WITH_SMOKE_TESTS=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      APP_PATH="$1"
      shift
      ;;
  esac
done

if [ -d "$APP_PATH/Contents/MacOS" ]; then
  MACOS_DIR="$APP_PATH/Contents/MacOS"
elif [ -d "$APP_PATH" ] && [ -x "$APP_PATH/Valheim" ]; then
  MACOS_DIR="$APP_PATH"
else
  echo "Could not resolve a Valheim app bundle from: $APP_PATH" >&2
  exit 1
fi

mkdir -p "$STAGE_DIR/BepInEx/core" "$STAGE_DIR/BepInEx/plugins" "$STAGE_DIR/BepInEx/patchers"
rm -rf "$STAGE_DIR/BepInEx/core"/*

dotnet publish "$REPO_ROOT/BepInEx.Preloader/BepInEx.Preloader.csproj" -c Release -o "$STAGE_DIR/BepInEx/core"
cp "$REPO_ROOT/doorstop/run_bepinex.sh" "$STAGE_DIR/run_bepinex.sh"
chmod +x "$STAGE_DIR/run_bepinex.sh"

if [ -z "$DOORSTOP_DIR" ]; then
  if [ "$(sysctl -n machdep.ptrauth_enabled 2>/dev/null || echo 0)" = "1" ]; then
    DOORSTOP_DIR="$("$REPO_ROOT/scripts/valheim/build_doorstop_arm64e.sh" "${TMPDIR:-/tmp}/UnityDoorstop-arm64e")"
  else
    OFFICIAL_DIR="${TMPDIR:-/tmp}/doorstop-macos-official"
    rm -rf "$OFFICIAL_DIR"
    mkdir -p "$OFFICIAL_DIR"
    curl -L "https://github.com/NeighTools/UnityDoorstop/releases/download/v4.5.0/doorstop_macos_release_4.5.0.zip" -o "$OFFICIAL_DIR/doorstop_macos.zip"
    unzip -q -o "$OFFICIAL_DIR/doorstop_macos.zip" -d "$OFFICIAL_DIR/unpacked"
    DOORSTOP_DIR="$OFFICIAL_DIR/unpacked/universal"
  fi
fi

cp "$DOORSTOP_DIR/libdoorstop.dylib" "$STAGE_DIR/libdoorstop.dylib"
cp "$DOORSTOP_DIR/.doorstop_version" "$STAGE_DIR/.doorstop_version"

if [ "$WITH_SMOKE_TESTS" = "1" ]; then
  dotnet build "$REPO_ROOT/TestPlugins/ValheimArm64Smoke.NoHarmony/ValheimArm64Smoke.NoHarmony.csproj" -c Release
  dotnet build "$REPO_ROOT/TestPlugins/ValheimArm64Smoke.Harmony/ValheimArm64Smoke.Harmony.csproj" -c Release
  cp "$REPO_ROOT/TestPlugins/ValheimArm64Smoke.NoHarmony/bin/Release/net35/ValheimArm64Smoke.NoHarmony.dll" "$STAGE_DIR/BepInEx/plugins/"
  cp "$REPO_ROOT/TestPlugins/ValheimArm64Smoke.Harmony/bin/Release/net35/ValheimArm64Smoke.Harmony.dll" "$STAGE_DIR/BepInEx/plugins/"
fi

if [ -n "$MODS_DIR" ]; then
  find "$MODS_DIR" -maxdepth 1 -type f \( -name '*.dll' -o -name '*.pdb' -o -name '*.mdb' \) -exec cp {} "$STAGE_DIR/BepInEx/plugins/" \;
fi

BACKUP_DIR=""
for path in BepInEx libdoorstop.dylib run_bepinex.sh .doorstop_version; do
  if [ -e "$MACOS_DIR/$path" ]; then
    if [ -z "$BACKUP_DIR" ]; then
      BACKUP_DIR="$MACOS_DIR/.bepinex-backup-$(date +%Y%m%d-%H%M%S)"
      mkdir -p "$BACKUP_DIR"
    fi
    rsync -a "$MACOS_DIR/$path" "$BACKUP_DIR/"
  fi
done

rsync -a "$STAGE_DIR/" "$MACOS_DIR/"

echo "Installed BepInEx into: $MACOS_DIR"
if [ -n "$BACKUP_DIR" ]; then
  echo "Backup created at: $BACKUP_DIR"
fi
echo
echo "Run Valheim with:"
echo "  cd \"$MACOS_DIR\" && ./run_bepinex.sh ./Valheim"
