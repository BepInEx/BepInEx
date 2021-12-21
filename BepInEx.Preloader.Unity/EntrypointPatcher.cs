using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Preloader.Core.Patching;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BepInEx.Preloader.Unity;

[PatcherPluginInfo("io.bepinex.entrypointpatcher", "BepInEx Entrypoint", "1.0")]
internal class EntrypointPatcher : BasePatcher
{
    private static readonly ConfigEntry<string> ConfigEntrypointAssembly = ConfigFile.CoreConfig.Bind(
     "Preloader.Entrypoint", "Assembly",
     UnityPreloader.IsPostUnity2017
         ? "UnityEngine.CoreModule.dll"
         : "UnityEngine.dll",
     "The local filename of the assembly to target.");

    private static readonly ConfigEntry<string> ConfigEntrypointType = ConfigFile.CoreConfig.Bind(
     "Preloader.Entrypoint", "Type",
     "Application",
     "The name of the type in the entrypoint assembly to search for the entrypoint method.");

    private static readonly ConfigEntry<string> ConfigEntrypointMethod = ConfigFile.CoreConfig.Bind(
     "Preloader.Entrypoint", "Method",
     ".cctor",
     "The name of the method in the specified entrypoint assembly and type to hook and load Chainloader from.");

    private bool HasLoaded { get; set; }

    [TargetAssembly(TargetAssemblyAttribute.AllAssemblies)]
    public bool PatchEntrypoint(ref AssemblyDefinition assembly, string filename)
    {
        if (HasLoaded || filename != ConfigEntrypointAssembly.Value)
            return false;

        if (assembly.MainModule.AssemblyReferences.Any(x => x.Name.Contains("BepInEx")))
            throw new
                Exception("BepInEx has been detected to be patched! Please unpatch before using a patchless variant!");

        var entrypointType = ConfigEntrypointType.Value;
        var entrypointMethod = ConfigEntrypointMethod.Value;

        Log.Log(LogLevel.Debug, $"Hooking chainloader into {entrypointType}::{entrypointMethod}");

        var isCctor = entrypointMethod.IsNullOrWhiteSpace() || entrypointMethod == ".cctor";


        var entryType = assembly.MainModule.Types.FirstOrDefault(x => x.Name == entrypointType);

        if (entryType == null)
            throw new Exception("The entrypoint type is invalid! Please check your config/BepInEx.cfg file");

        var chainloaderAssemblyPath = Path.Combine(Paths.BepInExAssemblyDirectory, "BepInEx.Unity.dll");

        var readerParameters = new ReaderParameters
        {
            AssemblyResolver = TypeLoader.CecilResolver
        };

        using (var chainloaderAssemblyDefinition =
               AssemblyDefinition.ReadAssembly(chainloaderAssemblyPath, readerParameters))
        {
            var chainloaderType =
                chainloaderAssemblyDefinition.MainModule.Types.First(x => x.Name == "UnityChainloader");

            var originalStartMethod = chainloaderType.EnumerateAllMethods().First(x => x.Name == "StaticStart");

            var startMethod = assembly.MainModule.ImportReference(originalStartMethod);

            var methods = new List<MethodDefinition>();

            if (isCctor)
            {
                var cctor = entryType.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic);

                if (cctor == null)
                {
                    cctor = new MethodDefinition(".cctor",
                                                 MethodAttributes.Static | MethodAttributes.Private
                                                                         | MethodAttributes.HideBySig |
                                                                           MethodAttributes.SpecialName
                                                                         | MethodAttributes.RTSpecialName,
                                                 assembly.MainModule.ImportReference(typeof(void)));

                    entryType.Methods.Add(cctor);
                    var il = cctor.Body.GetILProcessor();
                    il.Append(il.Create(OpCodes.Ret));
                }

                methods.Add(cctor);
            }
            else
            {
                methods.AddRange(entryType.Methods.Where(x => x.Name == entrypointMethod));
            }

            if (!methods.Any())
                throw new Exception("The entrypoint method is invalid! Please check your config.ini");

            foreach (var method in methods)
            {
                var il = method.Body.GetILProcessor();

                var ins = il.Body.Instructions.First();

                il.InsertBefore(ins,
                                il.Create(OpCodes
                                              .Ldnull)); // gameExePath (always null, we initialize the Paths class in Entrypoint)

                il.InsertBefore(ins,
                                il.Create(OpCodes.Call,
                                          startMethod)); // UnityChainloader.StaticStart(string gameExePath)
            }
        }

        HasLoaded = true;

        return true;
    }

    public override void Finalizer()
    {
        if (!HasLoaded)
            Log.Log(LogLevel.Fatal,
                    $"Failed to patch BepInEx chainloader into assembly '{ConfigEntrypointAssembly.Value}', either due to error or not being able to find it. Is it spelled correctly?");
    }
}
