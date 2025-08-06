#!/bin/sh
# BepInEx start script
#
# Run the script to start the game with BepInEx enabled
#
# There are two ways to use this script
#
# 1. Via CLI: Run ./run_bepinex.sh <path to game> [doorstop arguments] [game arguments]
# 2. Via config: edit the options below and run ./run.sh without any arguments

# LINUX: name of Unity executable
# MACOS: name of the .app directory
executable_name="valheim.x86_64"

# All of the below can be overriden with command line args

# General Config Options

# Enable Doorstop?
# 0 is false, 1 is true
enabled="1"

# Path to the assembly to load and execute
# NOTE: The entrypoint must be of format `static void Doorstop.Entrypoint.Start()`
target_assembly="BepInEx/core/BepInEx.Preloader.dll"

# Overrides the default boot.config file path
boot_config_override=""

# If enabled, DOORSTOP_DISABLE env var value is ignored
# USE THIS ONLY WHEN ASKED TO OR YOU KNOW WHAT THIS MEANS
ignore_disable_switch="0"

# Mono Options

# Overrides default Mono DLL search path
# Sometimes it is needed to instruct Mono to seek its assemblies from a different path
# (e.g. mscorlib is stripped in original game)
# This option causes Mono to seek mscorlib and core libraries from a different folder before Managed
# Original Managed folder is added as a secondary folder in the search path
# To specify multiple paths, separate them with colons (:)
dll_search_path_override=""

# If 1, Mono debugger server will be enabled
debug_enable="0"

# When debug_enabled is 1, specifies the address to use for the debugger server
debug_address="127.0.0.1:10000"

# If 1 and debug_enabled is 1, Mono debugger server will suspend the game execution until a debugger is attached
debug_suspend="0"

# CoreCLR options (IL2CPP)

# Path to coreclr shared library WITHOUT THE EXTENSION that contains the CoreCLR runtime
coreclr_path=""

# Path to the directory containing the managed core libraries for CoreCLR (mscorlib, System, etc.)
corlib_dir=""

################################################################################
# Everything past this point is the actual script

# Special case: program is launched via Steam
# In that case rerun the script via their bootstrapper to ensure Steam overlay works
steam_arg_helper() {
    if [ "$executable_name" != "" ] && [ "$1" != "${1%"$executable_name"}" ]; then
        return 0
    elif [ "$executable_name" = "" ] && [ "$1" != "${1%.x86_64}" ]; then
        return 0
    elif [ "$executable_name" = "" ] && [ "$1" != "${1%.x86}" ]; then
        return 0
    else
        return 1
    fi
}
for a in "$@"; do
    if [ "$a" = "SteamLaunch" ]; then
        rotated=0; max=$#
        while [ $rotated -lt $max ]; do
            if steam_arg_helper "$1"; then
                to_rotate=$(($# - rotated))
                set -- "$@" "$0"
                while [ $((to_rotate-=1)) -ge 0 ]; do
                    set -- "$@" "$1"
                    shift
                done
                exec "$@"
            else
                set -- "$@" "$1"
                shift
                rotated=$((rotated+1))
            fi
        done
        exit 1
    fi
done

# Handle first param being executable name
if [ -x "$1" ] ; then
    executable_name="$1"
    shift
fi

if [ -z "${executable_name}" ] || [ ! -x "${executable_name}" ]; then
    echo "Please set executable_name to a valid name in a text editor or as the first command line parameter"
    exit 1
fi

# Use POSIX-compatible way to get the directory of the executable
a="/$0"; a=${a%/*}; a=${a#/}; a=${a:-.}; BASEDIR=$(cd "$a" || exit; pwd -P)

arch=""
executable_path=""
lib_extension=""

abs_path() {
    # Resolve relative path to absolute from BASEDIR
    if [ "$1" = "${1#/}" ]; then
        set -- "${BASEDIR}/${1}"
    fi
    echo "$(cd "$(dirname "$1")" && pwd)/$(basename "$1")"
}

# Set executable path and the extension to use for the libdoorstop shared object
os_type="$(uname -s)"
case ${os_type} in
    Linux*)
        executable_path="$(abs_path "$executable_name")"
        lib_extension="so"
    ;;
    Darwin*)
        real_executable_name="$(abs_path "$executable_name")"

        # If we're not even an actual executable, check .app Info for actual executable
        case $real_executable_name in
            *.app/Contents/MacOS/*)
                executable_path="${executable_name}"
            ;;
            *)
                # Add .app to the end if not given
                if [ "$real_executable_name" = "${real_executable_name%.app}" ]; then
                    real_executable_name="${real_executable_name}.app"
                fi
                inner_executable_name=$(defaults read "${real_executable_name}/Contents/Info" CFBundleExecutable)
                executable_path="${real_executable_name}/Contents/MacOS/${inner_executable_name}"
            ;;
        esac
        lib_extension="dylib"
    ;;
    *)
        # alright whos running games on freebsd
        echo "Unknown operating system ($(uname -s))"
        echo "Make an issue at https://github.com/NeighTools/UnityDoorstop"
        exit 1
    ;;
esac

_readlink() {
    # relative links with readlink (without -f) do not preserve the path info
    ab_path="$(abs_path "$1")"
    link="$(readlink "${ab_path}")"
    case $link in
        /*);;
        *) link="$(dirname "$ab_path")/$link";;
    esac
    echo "$link"
}

resolve_executable_path () {
    e_path="$(abs_path "$1")"

    while [ -L "${e_path}" ]; do
        e_path=$(_readlink "${e_path}");
    done
    echo "${e_path}"
}

# Get absolute path of executable
executable_path=$(resolve_executable_path "${executable_path}")

# Figure out the arch of the executable with file
file_out="$(LD_PRELOAD="" file -b "${executable_path}")"
case "${file_out}" in
    *64-bit*)
        arch="x64"
    ;;
    *32-bit*)
        arch="x86"
    ;;
    *)
        echo "The executable \"${executable_path}\" is not compiled for x86 or x64 (might be ARM?)"
        echo "If you think this is a mistake (or would like to encourage support for other architectures)"
        echo "Please make an issue at https://github.com/NeighTools/UnityDoorstop"
        echo "Got: ${file_out}"
        exit 1
    ;;
esac

# Helper to convert common boolean strings into just 0 and 1
doorstop_bool() {
    case "$1" in
        TRUE|true|t|T|1|Y|y|yes)
            echo "1"
        ;;
        FALSE|false|f|F|0|N|n|no)
            echo "0"
        ;;
    esac
}

# Read from command line
i=0; max=$#
while [ $i -lt $max ]; do
    case "$1" in
        --doorstop-enabled)
            enabled="$(doorstop_bool "$2")"
            shift
            i=$((i+1))
        ;;
        --doorstop-target-assembly)
            target_assembly="$2"
            shift
            i=$((i+1))
        ;;
        --doorstop-boot-config-override)
            boot_config_override="$2"
            shift
            i=$((i+1))
        ;;
        --doorstop-mono-dll-search-path-override)
            dll_search_path_override="$2"
            shift
            i=$((i+1))
        ;;
        --doorstop-mono-debug-enabled)
            debug_enable="$(doorstop_bool "$2")"
            shift
            i=$((i+1))
        ;;
        --doorstop-mono-debug-suspend)
            debug_suspend="$(doorstop_bool "$2")"
            shift
            i=$((i+1))
        ;;
        --doorstop-mono-debug-address)
            debug_address="$2"
            shift
            i=$((i+1))
        ;;
        *)
            set -- "$@" "$1"
        ;;
    esac
    shift
    i=$((i+1))
done

target_assembly="$(abs_path "$target_assembly")"

# Move variables to environment
export DOORSTOP_ENABLED="$enabled"
export DOORSTOP_TARGET_ASSEMBLY="$target_assembly"
export DOORSTOP_BOOT_CONFIG_OVERRIDE="$boot_config_override"
export DOORSTOP_IGNORE_DISABLED_ENV="$ignore_disable_switch"
export DOORSTOP_MONO_DLL_SEARCH_PATH_OVERRIDE="$dll_search_path_override"
export DOORSTOP_MONO_DEBUG_ENABLED="$debug_enable"
export DOORSTOP_MONO_DEBUG_ADDRESS="$debug_address"
export DOORSTOP_MONO_DEBUG_SUSPEND="$debug_suspend"

# Final setup
doorstop_directory="${BASEDIR}/doorstop_libs"
doorstop_name="libdoorstop_${arch}.${lib_extension}"

export LD_LIBRARY_PATH="${doorstop_directory}:${LD_LIBRARY_PATH}"
if [ -z "$LD_PRELOAD" ]; then
    export LD_PRELOAD="${doorstop_name}"
else
    export LD_PRELOAD="${doorstop_name}:${LD_PRELOAD}"
fi

export DYLD_LIBRARY_PATH="${doorstop_directory}:${DYLD_LIBRARY_PATH}"
if [ -z "$DYLD_INSERT_LIBRARIES" ]; then
    export DYLD_INSERT_LIBRARIES="${doorstop_name}"
else
    export DYLD_INSERT_LIBRARIES="${doorstop_name}:${DYLD_INSERT_LIBRARIES}"
fi

exec "$executable_path" "$@"
