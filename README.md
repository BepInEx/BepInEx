<p align="center">
    <img src="https://avatars2.githubusercontent.com/u/39589027?s=256">
</p>

# BepInEx

![Github All Releases](https://img.shields.io/github/downloads/bepinex/bepinex/total.svg)
![GitHub release](https://img.shields.io/github/release/bepinex/bepinex.svg)
[![BepInEx Discord](https://user-images.githubusercontent.com/7288322/34429117-c74dbd12-ecb8-11e7-896d-46369cd0de5b.png)](https://discord.gg/MpFEDAg)

Bepis Injector Extensible

---

BepInEx is a plugin / modding framework for Unity Mono, IL2CPP and .NET framework games (XNA, FNA, MonoGame, etc.)

(Currently only Unity Mono has stable releases)

#### Platform compatibility chart

|              | Windows | OSX  | Linux | ARM |
|--------------|---------|------|-------|-----|
| Unity Mono   | ✔️      | ✔️  | ✔️    | N/A |
| Unity IL2CPP | ✔️      | ❌   | ✔     | ❌  |
| .NET / XNA   | ✔️      | Mono | Mono  | N/A |

A more comprehensive comparison list of features and compatibility is available at https://bepis.io/unity.html

## Resources

**[Latest releases](https://github.com/BepInEx/BepInEx/releases)**

**[Bleeding Edge builds](https://builds.bepinex.dev/projects/bepinex_be)**

**[How to install (latest releases)](https://docs.bepinex.dev/articles/user_guide/installation/index.html)**

**[How to install (Bleeding Edge, BepInEx 6)](https://docs.bepinex.dev/master/articles/user_guide/installation/index.html)**

**[User and developer guides](https://docs.bepinex.dev/master/)**

**[Discord server](https://discord.gg/MpFEDAg)**

### Available plugin loaders

| Name              | Link to project                                                                           |
|-------------------|-------------------------------------------------------------------------------------------|
| BSIPA             | [BepInEx.BSIPA.Loader](https://github.com/BepInEx/BepInEx.BSIPA.Loader)                   |
| IPA               | [IPALoaderX](https://github.com/BepInEx/IPALoaderX)                                       |
| MelonLoader       | [BepInEx.MelonLoader.Loader](https://github.com/BepInEx/BepInEx.MelonLoader.Loader)       |
| MonoMod           | [BepInEx.MonoMod.Loader](https://github.com/BepInEx/BepInEx.MonoMod.Loader)               |
| MuseDashModLoader | [BepInEx.MDML.Loader](https://github.com/BepInEx/BepInEx.MDML.Loader)                     |
| Partiality        | [BepInEx-Partiality-Wrapper](https://github.com/sinai-dev/BepInEx-Partiality-Wrapper)     |
| Sybaris           | [BepInEx.SybarisLoader.Patcher](https://github.com/BepInEx/BepInEx.SybarisLoader.Patcher) |
| UnityInjector     | [BepInEx.UnityInjector.Loader](https://github.com/BepInEx/BepInEx.UnityInjectorLoader)    |
| Unity Mod Manager | [Yan.UMMLoader](https://github.com/hacknet-bar/Yan.UMMLoader)                             |
| uMod              | [BepInEx.uMod.Loader](https://github.com/BepInEx/BepInEx.uMod.Loader)                     |

## Used libraries

- [NeighTools/UnityDoorstop](https://github.com/NeighTools/UnityDoorstop) - v4.5.0
- [BepInEx/HarmonyX](https://github.com/BepInEx/HarmonyX) - v2.10.2
- [0x0ade/MonoMod](https://github.com/0x0ade/MonoMod) - v22.7.31.1
- [jbevain/cecil](https://github.com/jbevain/cecil) - v0.10.4

#### IL2CPP libraries

- [SamboyCoding/Cpp2IL](https://github.com/SamboyCoding/Cpp2IL) - v2022.0.7.2
- [BepInEx/Il2CppInterop](https://github.com/BepInEx/Il2CppInterop) - v1.4.5
- [BepInEx/dotnet-runtime](https://github.com/BepInEx/dotnet-runtime) - v6.0.7

## License

The BepInEx project is licensed under the LGPL-2.1 license.
