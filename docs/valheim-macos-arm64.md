# Valheim on macOS Apple Silicon

Tested on April 6, 2026 on an Apple M4 Pro MacBook Pro with the Steam build of Valheim at:

`/Users/$USER/Library/Application Support/Steam/steamapps/common/Valheim/valheim.app`

## What was validated

- Valheim's macOS executable is a universal binary with `x86_64` and `arm64` slices.
- BepInEx `5.4.23.5` starts natively on macOS Apple Silicon with this branch.
- HarmonyX `2.16.1` plus MonoMod `25.x` is working for simple Harmony patches on this setup.
- The following real Valheim mods loaded successfully in native Apple Silicon testing:
  - `ValheimModding-Jotunn-2.28.0`
  - `Azumatt-AzuCraftyBoxes-1.8.13`

## The important blocker on newer Apple Silicon

The official UnityDoorstop `4.5.0` macOS release ships `libdoorstop.dylib` as `x86_64, arm64`.

On this M4 Pro test machine, `dyld` rejected that binary before managed code even started:

`missing compatible architecture (have 'x86_64,arm64', need 'arm64e')`

For this machine, the minimal working fix was to rebuild UnityDoorstop so the macOS universal dylib contains `x86_64, arm64e`.

## Install from this branch

Build and install into the default Steam Valheim app bundle:

```bash
scripts/valheim/install_macos_arm64.sh --with-smoke-tests
```

Install directly from a Gale or r2modman profile export:

```bash
scripts/valheim/install_gale_export.sh --export ~/Downloads/Servidor.r2z
```

Install into a custom Valheim path:

```bash
scripts/valheim/install_macos_arm64.sh "/path/to/valheim.app"
```

Copy an exported Windows or Gale plugin directory into the install at the same time:

```bash
scripts/valheim/install_macos_arm64.sh --mods-dir "/path/to/BepInEx/plugins"
```

Run the game:

```bash
cd "/Users/$USER/Library/Application Support/Steam/steamapps/common/Valheim/valheim.app/Contents/MacOS"
./run_bepinex.sh ./Valheim
```

## Smoke tests in this repo

Two test plugins are included under `TestPlugins/`:

- `ValheimArm64Smoke.NoHarmony`
- `ValheimArm64Smoke.Harmony`

The Harmony smoke test successfully patched and hit `FejdStartup.Awake` during native startup on the M4 Pro validation run.

## Remaining risks

- Mods with extra native libraries may still need macOS-specific binaries.
- Heavy IL manipulation and less common detour paths still need broader coverage than the smoke test and the two real mods above.
- If a mod pack was exported from Windows, only copy `BepInEx/plugins`, `BepInEx/config`, and `BepInEx/patchers`. Do not copy Windows root files like `winhttp.dll`.
- Gale `.r2z` exports do not include every mod DLL directly; this repo's installer resolves the package list from `export.r2x` and downloads the exact versions from Thunderstore.
