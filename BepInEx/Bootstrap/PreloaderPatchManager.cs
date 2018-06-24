using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BepInEx.Bootstrap
{
    public static class PreloaderPatchManager
    {
        /// <summary>
        /// The dictionary of currently loaded patchers. The key is the patcher delegate that will be used to patch, and the value is a list of filenames of assemblies that the patcher is targeting.
        /// </summary>
        public static Dictionary<AssemblyPatcherDelegate, IEnumerable<string>> PatcherDictionary { get; } =
            new Dictionary<AssemblyPatcherDelegate, IEnumerable<string>>();

        /// <summary>
        /// The list of initializers that were loaded from the patcher contract.
        /// </summary>
        public static List<Action> Initializers { get; } = new List<Action>();

        /// <summary>
        /// The list of finalizers that were loaded from the patcher contract.
        /// </summary>
        public static List<Action> Finalizers { get; } = new List<Action>();

        /// <summary>
        /// Adds the patcher to the patcher dictionary.
        /// </summary>
        /// <param name="dllNames">The list of DLL filenames to be patched.</param>
        /// <param name="patcher">The method that will perform the patching.</param>
        internal static void AddPatcher(IEnumerable<string> dllNames, AssemblyPatcherDelegate patcher)
        {
            PreloaderPatchManager.PatcherDictionary[patcher] = dllNames;
        }

        public static void Run()
        {
            AddPatcher(new[] {"UnityEngine.dll"}, PatchEntrypoint);

            if (Directory.Exists(Preloader.PatcherPluginPath))
            {
                SortedDictionary<string, KeyValuePair<AssemblyPatcherDelegate, IEnumerable<string>>> sortedPatchers =
                        new SortedDictionary<string, KeyValuePair<AssemblyPatcherDelegate, IEnumerable<string>>>();

                foreach (string assemblyPath in Directory.GetFiles(Preloader.PatcherPluginPath, "*.dll"))
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(assemblyPath);

                        foreach (var kv in GetPatcherMethods(assembly))
                            sortedPatchers.Add(assembly.GetName().Name, kv);
                    }
                    catch (BadImageFormatException) { } //unmanaged DLL
                    catch (ReflectionTypeLoadException) { } //invalid references
                }

                foreach (var kv in sortedPatchers)
                    AddPatcher(kv.Value.Value, kv.Value.Key);
            }

            AssemblyPatcher.PatchAll(Preloader.ManagedPath, PatcherDictionary, Initializers, Finalizers);
        }

        /// <summary>
        /// Scans the assembly for classes that use the patcher contract, and returns a dictionary of the patch methods.
        /// </summary>
        /// <param name="assembly">The assembly to scan.</param>
        /// <returns>A dictionary of delegates which will be used to patch the targeted assemblies.</returns>
        public static Dictionary<AssemblyPatcherDelegate, IEnumerable<string>> GetPatcherMethods(Assembly assembly)
        {
            var patcherMethods = new Dictionary<AssemblyPatcherDelegate, IEnumerable<string>>();

            foreach (var type in assembly.GetExportedTypes())
            {
                try
                {
                    if (type.IsInterface)
                        continue;

                    PropertyInfo targetsProperty = type.GetProperty("TargetDLLs",
                                                                    BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase,
                                                                    null,
                                                                    typeof(IEnumerable<string>),
                                                                    Type.EmptyTypes,
                                                                    null);

                    //first try get the ref patcher method
                    MethodInfo patcher = type.GetMethod("Patch",
                                                        BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase,
                                                        null,
                                                        CallingConventions.Any,
                                                        new[] {typeof(AssemblyDefinition).MakeByRefType()},
                                                        null);

                    if (patcher == null) //otherwise try getting the non-ref patcher method
                    {
                        patcher = type.GetMethod("Patch",
                                                 BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase,
                                                 null,
                                                 CallingConventions.Any,
                                                 new[] {typeof(AssemblyDefinition)},
                                                 null);
                    }

                    if (targetsProperty == null || !targetsProperty.CanRead || patcher == null)
                        continue;

                    AssemblyPatcherDelegate patchDelegate = (ref AssemblyDefinition ass) =>
                    {
                        //we do the array fuckery here to get the ref result out
                        object[] args = {ass};

                        patcher.Invoke(null, args);

                        ass = (AssemblyDefinition) args[0];
                    };

                    IEnumerable<string> targets = (IEnumerable<string>) targetsProperty.GetValue(null, null);

                    patcherMethods[patchDelegate] = targets;

                    MethodInfo initMethod = type.GetMethod("Initialize",
                                                           BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase,
                                                           null,
                                                           CallingConventions.Any,
                                                           Type.EmptyTypes,
                                                           null);

                    if (initMethod != null)
                    {
                        Initializers.Add(() => initMethod.Invoke(null, null));
                    }

                    MethodInfo finalizeMethod = type.GetMethod("Finish",
                                                               BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase,
                                                               null,
                                                               CallingConventions.Any,
                                                               Type.EmptyTypes,
                                                               null);

                    if (finalizeMethod != null)
                    {
                        Finalizers.Add(() => finalizeMethod.Invoke(null, null));
                    }
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Warning, $"Could not load patcher methods from {assembly.GetName().Name}");
                    Logger.Log(LogLevel.Warning, $"{ex}");
                }
            }

            Logger.Log(LogLevel.Info,
                       $"Loaded {patcherMethods.Select(x => x.Key).Distinct().Count()} patcher methods from {assembly.GetName().Name}");

            return patcherMethods;
        }

        /// <summary>
        /// Inserts BepInEx's own chainloader entrypoint into UnityEngine.
        /// </summary>
        /// <param name="assembly">The assembly that will be attempted to be patched.</param>
        public static void PatchEntrypoint(ref AssemblyDefinition assembly)
        {
            if (assembly.Name.Name == "UnityEngine")
            {
#if CECIL_10
                using (AssemblyDefinition injected = AssemblyDefinition.ReadAssembly(Preloader.BepInExAssemblyPath))
#elif CECIL_9
                AssemblyDefinition injected = AssemblyDefinition.ReadAssembly(BepInExAssemblyPath);
#endif
                {
                    var originalInjectMethod = injected.MainModule.Types.First(x => x.Name == "Chainloader").Methods
                                                       .First(x => x.Name == "Initialize");

                    var injectMethod = assembly.MainModule.ImportReference(originalInjectMethod);

                    var sceneManager = assembly.MainModule.Types.First(x => x.Name == "Application");

                    var voidType = assembly.MainModule.ImportReference(typeof(void));
                    var cctor = new MethodDefinition(".cctor",
                                                     Mono.Cecil.MethodAttributes.Static | Mono.Cecil.MethodAttributes.Private
                                                                                        | Mono.Cecil.MethodAttributes.HideBySig
                                                                                        | Mono.Cecil.MethodAttributes.SpecialName
                                                                                        | Mono.Cecil.MethodAttributes.RTSpecialName,
                                                     voidType);

                    var ilp = cctor.Body.GetILProcessor();
                    ilp.Append(ilp.Create(OpCodes.Call, injectMethod));
                    ilp.Append(ilp.Create(OpCodes.Ret));

                    sceneManager.Methods.Add(cctor);
                }
            }
        }
    }
}