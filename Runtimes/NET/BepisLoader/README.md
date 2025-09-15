# BepisLoader
[![Thunderstore Badge](https://modding.resonite.net/assets/available-on-thunderstore.svg)](https://thunderstore.io/c/resonite/)

A mod loader which allows using BepInEx with [Resonite](https://resonite.com/).

## Installation (Manual)

1. Download the latest release ZIP file (e.g., `ResoniteModding-BepisLoader-1.3.1.zip`) from [Thunderstore](https://thunderstore.io/c/resonite/p/ResoniteModding/BepisLoader/).
2. Extract the contents of the `BepInExPack` folder from the ZIP into your Resonite installation directory:
   - **Windows Default:** `C:\Program Files (x86)\Steam\steamapps\common\Resonite\`
   - **Linux Default:** `~/.steam/steam/steamapps/common/Resonite/`
3. **Linux Users Only:** The included `LinuxBootstrap.sh` file needs to be used instead of the default one:
   - The package includes a modified `LinuxBootstrap.sh` that launches `BepisLoader.dll` instead of `Renderite.Host.dll`
   - **Important:** Resonite updates could replace this file, breaking the mod loader. If this happens, you'll need to manually replace `LinuxBootstrap.sh` with the one from the BepisLoader package
4. Start the game normally.
5. If you want to verify that the mod loader is working, check the `BepInEx\LogOutput.log` file after launching the game.

### Uninstallation

1. Delete the following files from your Resonite installation directory:
   - **Windows Entry Points:**
     - `hostfxr.dll` (our Windows entry point)
     - `hostfxr.pdb`
   - **Common Files:**
     - `hookfxr.ini`
     - All `BepisLoader*` files
   - **Linux Only:**
     - Restore the original `LinuxBootstrap.sh` (or verify game files through Steam)
2. Delete the `BepInEx` folder.

## Package Contents

The BepisLoader package contains:
- **Windows Entry Point:** `hostfxr.dll` and `hookfxr.ini` for hooking into the .NET runtime
- **Linux Entry Point:** Modified `LinuxBootstrap.sh` that launches BepisLoader
- **BepisLoader:** Core loader files (`BepisLoader.dll`, etc.)
- **BepInEx:** The BepInEx framework and all required dependencies

## References

BepisLoader makes use of these repositories and packages them inside its releases:

- [BepInEx .NET 9 Fork](https://github.com/ResoniteModding/BepInEx) - The core BepInEx framework
- [BepInEx Resonite Shim](https://github.com/ResoniteModding/BepInExResoniteShim) - Resonite-specific compatibility layer
- [hookfxr](https://github.com/ResoniteModding/hookfxr) - .NET runtime hooking for Windows
