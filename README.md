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
| Unity Mono   | ✔️       | ✔️    | ✔️     | N/A |
| Unity IL2CPP | ✔️       | ❌    | ❌ (Wine only)  | ❌   |
| .NET / XNA   | ✔️       | Mono | Mono  | N/A |

A more comprehensive comparison list of features and compatibility is available at https://bepis.io/unity.html


## Resources

**[Latest releases](https://github.com/BepInEx/BepInEx/releases)**

**[Bleeding Edge builds](https://builds.bepis.io/projects/bepinex_be)**

**[How to install](https://bepinex.github.io/bepinex_docs/master/articles/user_guide/installation/index.html)**

**[User and developer guides](https://bepinex.github.io/bepinex_docs/master/articles/index.html)**

**[Discord server](https://discord.gg/MpFEDAg)**

### Available plugin loaders

| Name              | Link to project                                                                           |
|-------------------|-------------------------------------------------------------------------------------------|
| BSIPA             | [BepInEx.BSIPA.Loader](https://github.com/BepInEx/BepInEx.BSIPA.Loader)                   |
| IPA               | [IPALoaderX](https://github.com/BepInEx/IPALoaderX)                                       |
| MelonLoader       | [BepInEx.MelonLoader.Loader](https://github.com/BepInEx/BepInEx.MelonLoader.Loader)       |
| MonoMod           | [BepInEx.MonoMod.Loader](https://github.com/BepInEx/BepInEx.MonoMod.Loader)               |
| Partiality        | [BepInEx-Partiality-Wrapper](https://github.com/sinai-dev/BepInEx-Partiality-Wrapper)     |
| Sybaris           | [BepInEx.SybarisLoader.Patcher](https://github.com/BepInEx/BepInEx.SybarisLoader.Patcher) |
| UnityInjector     | [BepInEx.UnityInjector.Loader](https://github.com/BepInEx/BepInEx.UnityInjectorLoader)    |
| Unity Mod Manager | [Yan.UMMLoader](https://github.com/hacknet-bar/Yan.UMMLoader)                             |

## Used libraries
- [NeighTools/UnityDoorstop](https://github.com/NeighTools/UnityDoorstop) - 3.1 ([1646a74](https://github.com/NeighTools/UnityDoorstop/commit/1646a74fd58c287533b67ac576ef974908d24346))
- [NeighTools/UnityDoorstop.Unix](https://github.com/NeighTools/UnityDoorstop.Unix) - 1.2.0.0 ([94c882f](https://github.com/NeighTools/UnityDoorstop.Unix/commit/94c882f9c42b53685571b2d160ccf6e2e9492434))
- [BepInEx/HarmonyX](https://github.com/BepInEx/HarmonyX) - 2.3.1 ([bbc07d](https://github.com/BepInEx/HarmonyX/commit/bbc07dd1a6537cb1397c490f93a5619ad1d1fe3e))
- [0x0ade/MonoMod](https://github.com/0x0ade/MonoMod) - v20.11.26.02 ([1775ec9](https://github.com/MonoMod/MonoMod/commit/1775ec98e76d3420b2365d6103b4f1b69761a197))
- [jbevain/cecil](https://github.com/jbevain/cecil) - 0.10.4 ([98ec890](https://github.com/jbevain/cecil/commit/98ec890d44643ad88d573e97be0e120435eda732))

#### IL2CPP libraries
- [Perfare/Il2CppDumper](https://github.com/Perfare/Il2CppDumper) - v6.4.12 fork ([585cc52](https://github.com/BepInEx/Il2CppDumper/commit/585cc5209955a776e0e583c56b85bcfb4f0833e6))
- [knah/Il2CppAssemblyUnhollower](https://github.com/knah/Il2CppAssemblyUnhollower) - v0.4.13.0 ([8957b7a](https://github.com/knah/Il2CppAssemblyUnhollower/commit/8957b7a6d7996467956f8354a0f6cdaf63c7adef))
- [mono/mono](https://github.com/mono/mono) - 6.12.0.93 fork ([7328415](https://github.com/BepInEx/mono/commit/7328415ac575399a71f32487e97bce9d5fe7f6ca))

## Credits
- [Usagirei](https://github.com/Usagirei) - Code for using the console and for assisting with technical support
- [essu](https://github.com/exdownloader) - Project logo, moral support and lots of misc. help
- [denikson](https://github.com/denikson) - [UnityDoorstop](https://github.com/NeighTools/UnityDoorstop) for the patchless loader
- [nn@](https://twitter.com/NnAone2cmg) - Japanese translation of the wiki
