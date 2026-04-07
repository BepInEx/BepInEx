#!/usr/bin/env bash
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd -P)"
DEFAULT_APP="/Users/$USER/Library/Application Support/Steam/steamapps/common/Valheim/valheim.app"
APP_PATH="$DEFAULT_APP"
DOORSTOP_DIR=""
EXPORT_PATH=""
SKIP_RUNTIME_INSTALL=0
WITH_SMOKE_TESTS=0
WORKDIR="${TMPDIR:-/tmp}/valheim-gale-export"

usage() {
  cat <<'EOF'
Usage:
  install_gale_export.sh --export /path/to/profile.r2z [--app /path/to/valheim.app] [--doorstop-dir DIR]
                         [--skip-runtime-install] [--with-smoke-tests]

Examples:
  scripts/valheim/install_gale_export.sh --export ~/Downloads/Servidor.r2z
  scripts/valheim/install_gale_export.sh --export ~/Downloads/Servidor.r2z --app "/Users/me/Library/Application Support/Steam/steamapps/common/Valheim/valheim.app"
EOF
}

while [ $# -gt 0 ]; do
  case "$1" in
    --export)
      EXPORT_PATH="$2"
      shift 2
      ;;
    --app)
      APP_PATH="$2"
      shift 2
      ;;
    --doorstop-dir)
      DOORSTOP_DIR="$2"
      shift 2
      ;;
    --skip-runtime-install)
      SKIP_RUNTIME_INSTALL=1
      shift
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
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

if [ -z "$EXPORT_PATH" ]; then
  echo "Missing required --export argument" >&2
  usage >&2
  exit 1
fi

if [ ! -f "$EXPORT_PATH" ]; then
  echo "Export file not found: $EXPORT_PATH" >&2
  exit 1
fi

if [ -d "$APP_PATH/Contents/MacOS" ]; then
  MACOS_DIR="$APP_PATH/Contents/MacOS"
else
  echo "Could not resolve a Valheim app bundle from: $APP_PATH" >&2
  exit 1
fi

if [ "$SKIP_RUNTIME_INSTALL" = "0" ]; then
  install_args=("$APP_PATH")
  if [ -n "$DOORSTOP_DIR" ]; then
    install_args+=(--doorstop-dir "$DOORSTOP_DIR")
  fi
  if [ "$WITH_SMOKE_TESTS" = "1" ]; then
    install_args+=(--with-smoke-tests)
  fi

  "$REPO_ROOT/scripts/valheim/install_macos_arm64.sh" "${install_args[@]}"
fi

rm -rf "$WORKDIR"
mkdir -p "$WORKDIR/export" "$WORKDIR/downloads" "$WORKDIR/packages" "$WORKDIR/stage/BepInEx/plugins" "$WORKDIR/stage/BepInEx/patchers" "$WORKDIR/stage/BepInEx/config" "$WORKDIR/stage/BepInEx/core"
ditto -x -k "$EXPORT_PATH" "$WORKDIR/export"

MANIFEST_PATH="$WORKDIR/export/export.r2x"
if [ ! -f "$MANIFEST_PATH" ]; then
  echo "Could not find export.r2x inside $EXPORT_PATH" >&2
  exit 1
fi

if [ -d "$WORKDIR/export/BepInEx/plugins" ]; then
  rsync -a "$WORKDIR/export/BepInEx/plugins/" "$WORKDIR/stage/BepInEx/plugins/"
fi
if [ -d "$WORKDIR/export/BepInEx/patchers" ]; then
  rsync -a "$WORKDIR/export/BepInEx/patchers/" "$WORKDIR/stage/BepInEx/patchers/"
fi
if [ -d "$WORKDIR/export/BepInEx/config" ]; then
  rsync -a "$WORKDIR/export/BepInEx/config/" "$WORKDIR/stage/BepInEx/config/"
fi
if [ -d "$WORKDIR/export/BepInEx/core" ]; then
  rsync -a "$WORKDIR/export/BepInEx/core/" "$WORKDIR/stage/BepInEx/core/"
fi

package_rows=()
while IFS= read -r line; do
  package_rows+=("$line")
done < <(
  ruby - "$MANIFEST_PATH" <<'RUBY'
require "yaml"

manifest = YAML.safe_load(File.read(ARGV[0]))
mods = manifest.fetch("mods", [])

mods.each do |mod|
  next unless mod["enabled"]

  author, package = mod.fetch("name").split("-", 2)
  version = mod.fetch("version")

  puts [author, package, "#{version["major"]}.#{version["minor"]}.#{version["patch"]}"].join("\t")
end
RUBY
)

if [ "${#package_rows[@]}" -eq 0 ]; then
  echo "No enabled packages found in $MANIFEST_PATH" >&2
  exit 1
fi

downloaded_count=0
for row in "${package_rows[@]}"; do
  IFS=$'\t' read -r author package version <<<"$row"

  if [ "$author" = "denikson" ] && [ "$package" = "BepInExPack_Valheim" ]; then
    echo "Skipping runtime package $author-$package-$version; using custom macOS runtime instead"
    continue
  fi

  archive_path="$WORKDIR/downloads/${author}-${package}-${version}.zip"
  package_dir="$WORKDIR/packages/${author}-${package}-${version}"
  package_url="https://thunderstore.io/package/download/${author}/${package}/${version}/"

  echo "Downloading $author-$package-$version"
  curl -fsSL "$package_url" -o "$archive_path"
  rm -rf "$package_dir"
  mkdir -p "$package_dir"
  ditto -x -k "$archive_path" "$package_dir"

  while IFS= read -r windows_path; do
    relative_path="${windows_path#$package_dir/}"
    normalized_relative_path="${relative_path//\\//}"
    normalized_path="$package_dir/$normalized_relative_path"
    mkdir -p "$(dirname "$normalized_path")"
    mv "$windows_path" "$normalized_path"
  done < <(find "$package_dir" -type f -name '*\\*')

  find "$package_dir" -maxdepth 1 -type f \( -name '*.dll' -o -name '*.pdb' -o -name '*.mdb' -o -name '*.xml' \) -exec cp {} "$WORKDIR/stage/BepInEx/plugins/" \;

  for relative_dir in plugins patchers config core; do
    if [ -d "$package_dir/$relative_dir" ]; then
      rsync -a "$package_dir/$relative_dir/" "$WORKDIR/stage/BepInEx/$relative_dir/"
    fi
    if [ -d "$package_dir/BepInEx/$relative_dir" ]; then
      rsync -a "$package_dir/BepInEx/$relative_dir/" "$WORKDIR/stage/BepInEx/$relative_dir/"
    fi
  done

  downloaded_count=$((downloaded_count + 1))
done

PROFILE_BACKUP_DIR="$MACOS_DIR/.bepinex-profile-backup-$(date +%Y%m%d-%H%M%S)"
mkdir -p "$PROFILE_BACKUP_DIR"
for relative_dir in plugins patchers config; do
  if [ -d "$MACOS_DIR/BepInEx/$relative_dir" ]; then
    rsync -a "$MACOS_DIR/BepInEx/$relative_dir/" "$PROFILE_BACKUP_DIR/$relative_dir/"
  fi
  rm -rf "$MACOS_DIR/BepInEx/$relative_dir"
  mkdir -p "$MACOS_DIR/BepInEx/$relative_dir"
done

if [ -d "$WORKDIR/stage/BepInEx/core" ] && [ -n "$(find "$WORKDIR/stage/BepInEx/core" -mindepth 1 -maxdepth 1 -print -quit)" ]; then
  rsync -a "$WORKDIR/stage/BepInEx/core/" "$MACOS_DIR/BepInEx/core/"
fi
rsync -a "$WORKDIR/stage/BepInEx/plugins/" "$MACOS_DIR/BepInEx/plugins/"
rsync -a "$WORKDIR/stage/BepInEx/patchers/" "$MACOS_DIR/BepInEx/patchers/"
rsync -a "$WORKDIR/stage/BepInEx/config/" "$MACOS_DIR/BepInEx/config/"

echo
echo "Installed Gale export from: $EXPORT_PATH"
echo "Downloaded packages: $downloaded_count"
echo "Profile backup: $PROFILE_BACKUP_DIR"
echo "Valheim path: $MACOS_DIR"
echo
echo "Run Valheim with:"
echo "  cd \"$MACOS_DIR\" && ./run_bepinex.sh ./Valheim"
