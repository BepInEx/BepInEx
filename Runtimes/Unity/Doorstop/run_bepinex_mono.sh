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
executable_name=""

# All of the below can be overriden with command line args

# General Config Options

# Enable Doorstop?
# 0 is false, 1 is true
enabled="1"

# Path to the assembly to load and execute
# NOTE: The entrypoint must be of format `static void Doorstop.Entrypoint.Start()`
target_assembly="BepInEx/core/BepInEx.Unity.Mono.Preloader.dll"

# Overrides the default boot.config file path
boot_config_override=

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
dll_search_path_override="BepInEx/core"

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
set -e

# Special case: program is launched via Steam on Linux
# In that case rerun the script via their bootstrapper to delay adding Doorstop to LD_PRELOAD
# This is required until https://github.com/NeighTools/UnityDoorstop/issues/88 is resolved
for a in "$@"; do
    if [ "$a" = "SteamLaunch" ]; then
        rotated=0; max=$#
        while [ $rotated -lt $max ]; do
            # Test if argument is prefixed with the value of $PWD
            if [ "$1" != "${1#"${PWD%/}/"}" ]; then
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
        echo "Could not determine game executable launched by Steam" 1>&2
        exit 1
    fi
done

# Handle first param being executable name
if [ -x "$1" ] ; then
    executable_name="$1"
    shift
fi

if [ -z "${executable_name}" ] || [ ! -x "${executable_name}" ]; then
    echo "Please set executable_name to a valid name in a text editor or as the first command line parameter" 1>&2
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

# Set executable path and the extension to use for the libdoorstop shared object as well as check whether we're running on Apple Silicon
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

        # CPUs for Apple Silicon are in the format "Apple M.."
        cpu_type="$(sysctl -n machdep.cpu.brand_string)"
        case "${cpu_type}" in
            Apple*)
                is_apple_silicon=1
            ;;
        esac
    ;;
    *)
        # alright whos running games on freebsd
        echo "Unknown operating system ($(uname -s))" 1>&2
        echo "Make an issue at https://github.com/NeighTools/UnityDoorstop" 1>&2
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
    *PE32*)
        echo "The executable is a Windows executable file. You must use Wine/Proton and BepInEx for Windows with this executable." 1>&2
        echo "Uninstall BepInEx for *nix and install BepInEx for Windows instead." 1>&2
        echo "More info: https://docs.bepinex.dev/articles/advanced/steam_interop.html#protonwine" 1>&2
        exit 1
    ;;
    *shell\ script*)
        # Fallback for games that launch a shell script from Steam
        # default to x64, change as needed
        arch="x64"
    ;;
    *64-bit*)
        arch="x64"
    ;;
    *32-bit*)
        arch="x86"
    ;;
    *)
        echo "The executable \"${executable_path}\" is not compiled for x86 or x64 (might be ARM?)" 1>&2
        echo "If you think this is a mistake (or would like to encourage support for other architectures)" 1>&2
        echo "Please make an issue at https://github.com/NeighTools/UnityDoorstop" 1>&2
        echo "Got: ${file_out}" 1>&2
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
        --doorstop_enabled) # For backwards compatibility. Renamed to --doorstop-enabled
            enabled="$(doorstop_bool "$2")"
            shift
            i=$((i+1))
        ;;
        --doorstop_target_assembly) # For backwards compatibility. Renamed to --doorstop-target-assembly
            target_assembly="$2"
            shift
            i=$((i+1))
        ;;
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
        --doorstop-clr-runtime-coreclr-path)
            coreclr_path="$2"
            shift
            i=$((i+1))
        ;;
        --doorstop-clr-corlib-dir)
            corlib_dir="$2"
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
export DOORSTOP_CLR_RUNTIME_CORECLR_PATH="$coreclr_path.$lib_extension"
export DOORSTOP_CLR_CORLIB_DIR="$corlib_dir"

# Final setup
doorstop_directory="${BASEDIR}/"
doorstop_name="libdoorstop.${lib_extension}"

export LD_LIBRARY_PATH="${doorstop_directory}:${corlib_dir}:${LD_LIBRARY_PATH}"
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

if [ -n "${is_apple_silicon}" ]; then
    export ARCHPREFERENCE="arm64,x86_64"

    # We need to use arch for Apple Silicon to allow the executable to be run natively as otherwise if
    # the executable is universal, supporting both x86_64 and arm64, MacOs will still run it as x86_64
    # if the parent process is running as x86.
    # arch also strips the DYLD_INSERT_LIBRARIES env var so we have to pass that in manually
    exec arch -e DYLD_INSERT_LIBRARIES="${DYLD_INSERT_LIBRARIES}" "$executable_path" "$@"
else
    exec "$executable_path" "$@"
fi
