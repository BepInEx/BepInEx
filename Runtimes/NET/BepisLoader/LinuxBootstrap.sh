#!/usr/bin/env sh

# This is a bootstrapper to run Resonite in what is quite the unorthodox
# configuration on Linux.
#
# This script is executed by the "ResoBoot" bootstrapper if ResoBoot
# detects it's running under Proton/Wine.
#
# The following shell code is responsible for installing an adequate runtime
# for Resonite that's independent from any system installation (or lack thereof)
# and for ensuring that Renderite is executed with Proton's Wine installation
# and not the system's.


### SCRIPT START ###

# Ensure that $DOTNET_ROOT points to the downloaded runtime rather
# than at the system's. Also ensure that the root and tools subfolder
# are temporarily added to the system's $PATH variable just to make
# sure things work correctly.

DOTNET_ROOT="$PWD/dotnet-runtime"
PATH="$PATH":"$DOTNET_ROOT":"$DOTNET_ROOT"/tools


# Miscellaneous variables

DOTNET_INSTALL_SCRIPT="$PWD/dotnet-install.sh"
DOTNET_EXECUTABLE="$DOTNET_ROOT/dotnet"
RENDERER_SCRIPT="Renderer/Renderite.Renderer.sh"

# terminal_execute()
# {
# 	# !! Modified by Cyro !! (Change: terminal emulator order)
# 	#
# 	# This code is released in public domain by Han Boetes <han@mijncomputer.nl>
# 	#
# 	# This script tries to exec a terminal emulator by trying some known terminal
# 	# emulators.
# 	#
# 	# We welcome patches that add distribution-specific mechanisms to find the
# 	# preferred terminal emulator. On Debian, there is the x-terminal-emulator
# 	# symlink for example.
# 	#
# 	# Invariants:
# 	# 1. $TERMINAL comes first.
# 	# 2. The most common terminal emulators from popular desktop environments are tried afterwards.
# 	# 4. More niche/less well-known terminal emulators are tried next.
# 	# 3. Distribution-specific mechanisms come last (since they're kind of clunky), e.g. x-terminal-emulator, uxterm, xterm, etc.

# 	for terminal in "$TERMINAL" konsole gnome-terminal mate-terminal xfce4-terminal guake terminix tilix lxterminal terminator termite terminology tilda hyper alacritty termit kitty Eterm rio roxterm st lilyterm wezterm qterminal x-terminal-emulator uxterm xterm aterm urxvt rxvt; do
# 		if command -v "$terminal" > /dev/null 2>&1; then
# 			"$terminal" -e "$* 2>/dev/null"
# 			break
# 		fi
# 	done

# 	# "$*" 2>/dev/null
# }

# Overload the 'dotnet' command so that it calls the version which was
# downloaded by the script rather than potentially call (or fail to call)
# the system's main dotnet runtime.

dotnet()
{
	"$DOTNET_EXECUTABLE" "$@"
}

main()
{
	# Make sure the dotnet installer is executable, grab the .NET 9.0 runtime and
	# just place it in the main Resonite folder.

	chmod +x "$DOTNET_INSTALL_SCRIPT"


	# Also make sure that the alternate Renderite script is executable as well
	# so that Resonite can run it.

	chmod +x "$RENDERER_SCRIPT"


	# Install .NET 9 into the current directory

	bash "$DOTNET_INSTALL_SCRIPT" --verbose --channel 9.0 --runtime dotnet --install-dir "$DOTNET_ROOT"


	# Not technically required, but mark dotnet itself as executable just in case.

	chmod +x "$DOTNET_EXECUTABLE"


	# Replace Windows runtime config with Linux runtime config for BepisLoader
	if [ -f "./BepisLoader.runtimeconfig.json" ]; then
		cat > "./BepisLoader.runtimeconfig.json" <<-'EOF'
		{
		  "runtimeOptions": {
		    "tfm": "net9.0",
		    "framework": {
		      "name": "Microsoft.NETCore.App",
		      "version": "9.0.0"
		    },
		    "configProperties": {
		      "System.Reflection.Metadata.MetadataUpdater.IsSupported": false,
		      "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": false
		    }
		  }
		}
		EOF
	fi


	# ~ Launch Resonite! :) ~

	dotnet BepisLoader.dll "$@"
}

main "$@"
exit
