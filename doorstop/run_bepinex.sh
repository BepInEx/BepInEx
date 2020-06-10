#!/bin/sh
# BepInEx running script
#
# This script is used to run a Unity game with BepInEx enabled.
#
# Usage: Configure the script below and simply run this script when you want to run your game modded.

if [ -z "$1" ]; then echo "Please open run.sh in a text editor and configure executable name. Comment or remove this line when you're done." && exit 1; fi

# -------- SETTINGS --------
# ---- EDIT AS NEEDED ------

# EDIT THIS: The name of the executable to run
# LINUX: This is the name of the Unity game executable 
# MACOS: This is the name of the game app WITHOUT the .app
executable_name="";


# The rest is automatically handled by BepInEx

# Whether or not to enable Doorstop. Valid values: TRUE or FALSE
export DOORSTOP_ENABLE=TRUE;

# What .NET assembly to execute. Valid value is a path to a .NET DLL that mono can execute.
export DOORSTOP_INVOKE_DLL_PATH=${PWD}/BepInEx/core/BepInEx.Preloader.dll;


# ----- DO NOT EDIT FROM THIS LINE FORWARD  ------
# ----- (unless you know what you're doing) ------

# Backup current LD_PRELOAD because it can break `file` when running from Steam
LD_PRELOAD_BAK=$LD_PRELOAD;
export LD_PRELOAD="";

doorstop_libs=${PWD}/doorstop_libs;
arch="";
executable_path="";
lib_postfix="";

os_type=`uname -s`;
case $os_type in
    Linux*)     executable_path=${PWD}/${executable_name};
                lib_postfix="so";;
    Darwin*)    executable_path=${PWD}/${executable_name}.app/Contents/MacOS/${executable_name};
                lib_postfix="dylib";;
    *)          echo "Cannot identify OS (got $(uname -s))!";
                echo "Please create an issue at https://github.com/BepInEx/BepInEx/issues."; 
                exit 1;;
esac

# Special case: if there is an arg, use that as executable path
# Linux: arg is path to the executable
# MacOS: arg is path to the .app folder which we need to resolve to the exectuable
if [ -n "$1" ]; then
    case $os_type in
        Linux*)     executable_path=$1;;
        Darwin*)    executable_name=`basename "$1" .app`;
                    executable_path=$1/Contents/MacOS/$executable_name;;
    esac
fi

executable_type=`file -b "${executable_path}"`;
case $executable_type in
    *64-bit*)           arch="x64";;
    *32-bit*|*i386*)    arch="x86";;
    *)          echo "Cannot identify executable type (got ${executable_type})!"; 
                echo "Please create an issue at https://github.com/BepInEx/BepInEx/issues."; 
                exit 1;;
esac

doorstop_libname=libdoorstop_${arch}.${lib_postfix};
export LD_LIBRARY_PATH=${doorstop_libs}:${LD_LIBRARY_PATH};
export LD_PRELOAD=${doorstop_libs}/$doorstop_libname:$LD_PRELOAD_BAK;
export DYLD_LIBRARY_PATH=${doorstop_libs};
export DYLD_INSERT_LIBRARIES=${doorstop_libs}/$doorstop_libname;

"${executable_path}"