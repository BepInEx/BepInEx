using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Common;
using BepInEx.Logger;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace BepInEx.Bootstrap
{
    public static class Preloader
    {
        #region Path Properties

        public static string ExecutablePath { get; private set; }

        public static string CurrentExecutingAssemblyPath => Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "").Replace('/', '\\');

        public static string CurrentExecutingAssemblyDirectoryPath => Path.GetDirectoryName(CurrentExecutingAssemblyPath);

        public static string GameName => Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().ProcessName);

        public static string GameRootPath => Path.GetDirectoryName(ExecutablePath);

        public static string ManagedPath => Utility.CombinePaths(GameRootPath, $"{GameName}_Data", "Managed");

        public static string PluginPath => Utility.CombinePaths(GameRootPath, "BepInEx");

        public static string PatcherPluginPath => Utility.CombinePaths(GameRootPath, "BepInEx", "patchers");

        #endregion

        public static PreloaderTextWriter PreloaderLog { get; private set; }

        public static Dictionary<string, IList<AssemblyPatcherDelegate>> PatcherDictionary = new Dictionary<string, IList<AssemblyPatcherDelegate>>(StringComparer.OrdinalIgnoreCase);


        public static void AddPatcher(string dllName, AssemblyPatcherDelegate patcher)
        {
            if (PatcherDictionary.TryGetValue(dllName, out IList<AssemblyPatcherDelegate> patcherList))
                patcherList.Add(patcher);
            else
            {
                patcherList = new List<AssemblyPatcherDelegate>();

                patcherList.Add(patcher);

                PatcherDictionary[dllName] = patcherList;
            }
        }

        public static void Main(string[] args)
        {
            try
            {
                PreloaderLog = new PreloaderTextWriter();

                PreloaderLog.WriteLine($"BepInEx {Assembly.GetExecutingAssembly().GetName().Version}");
                PreloaderLog.Log(LogLevel.Message, "Preloader started");


                ExecutablePath = args[0];

                AppDomain.CurrentDomain.AssemblyResolve += LocalResolve;



                AddPatcher("UnityEngine.dll", PatchEntrypoint);

                if (Directory.Exists(PatcherPluginPath))
                    foreach (string assemblyPath in Directory.GetFiles(PatcherPluginPath, "*.dll"))
                    {
                        try
                        {
                            var assembly = Assembly.LoadFrom(assemblyPath);

                            foreach (var kv in GetPatcherMethods(assembly))
                                foreach (var patcher in kv.Value)
                                    AddPatcher(kv.Key, patcher);
                        }
                        catch (BadImageFormatException) { } //unmanaged DLL
                        catch (ReflectionTypeLoadException) { } //invalid references
                    }

                AssemblyPatcher.PatchAll(ManagedPath, PatcherDictionary);
            }
            catch (Exception ex)
            {
                PreloaderLog.Log(LogLevel.Fatal, "Could not run preloader!");
                PreloaderLog.Log(LogLevel.Fatal, ex);

                PreloaderLog.Disable();

                try
                {
                    UnityInjector.ConsoleUtil.ConsoleWindow.Attach();
                    Console.Write(PreloaderLog);
                }
                finally
                {
                    File.WriteAllText(Path.Combine(GameRootPath, $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log"),
                        PreloaderLog.ToString());
                }
            }
            finally
            {
                PreloaderLog.Disable();
            }
        }

        internal static IDictionary<string, IList<AssemblyPatcherDelegate>> GetPatcherMethods(Assembly assembly)
        {
            var patcherMethods = new Dictionary<string, IList<AssemblyPatcherDelegate>>(StringComparer.OrdinalIgnoreCase);

            foreach (var type in assembly.GetExportedTypes())
            {
                try
                {
                    if (type.IsInterface)
                        continue;

                    PropertyInfo targetsProperty = type.GetProperty(
                        "TargetDLLs", 
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase,
                        null,
                        typeof(IEnumerable<string>),
                        Type.EmptyTypes,
                        null);

                    MethodInfo patcher = type.GetMethod(
                        "Patch", 
                        BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase,
                        null,
                        CallingConventions.Any,
                        new[] { typeof(AssemblyDefinition) },
                        null);

                    if (targetsProperty == null || !targetsProperty.CanRead || patcher == null)
                        continue;

                    AssemblyPatcherDelegate patchDelegate = (ass) => { patcher.Invoke(null, new object[] {ass}); };

                    IEnumerable<string> targets = (IEnumerable<string>)targetsProperty.GetValue(null, null);

                    foreach (string target in targets)
                    {
                        if (patcherMethods.TryGetValue(target, out IList<AssemblyPatcherDelegate> patchers))
                            patchers.Add(patchDelegate);
                        else
                        {
                            patchers = new List<AssemblyPatcherDelegate>{ patchDelegate };

                            patcherMethods[target] = patchers;
                        }
                    }
                }
                catch (Exception ex)
                {
                    PreloaderLog.Log(LogLevel.Warning, $"Could not load patcher methods from {assembly.GetName().Name}");
                }
            }

            PreloaderLog.Log(LogLevel.Info, $"Loaded {patcherMethods.SelectMany(x => x.Value).Distinct().Count()} patcher methods from {assembly.GetName().Name}");

            return patcherMethods;
        }

        internal static void PatchEntrypoint(AssemblyDefinition assembly)
        {
            if (assembly.Name.Name == "UnityEngine")
            {
#if CECIL_10
                using (AssemblyDefinition injected = AssemblyDefinition.ReadAssembly(CurrentExecutingAssemblyPath))
#elif CECIL_9
                AssemblyDefinition injected = AssemblyDefinition.ReadAssembly(CurrentExecutingAssemblyPath);
#endif
                {
                    var originalInjectMethod = injected.MainModule.Types.First(x => x.Name == "Chainloader")
                        .Methods.First(x => x.Name == "Initialize");

                    var injectMethod = assembly.MainModule.ImportReference(originalInjectMethod);

                    var sceneManager = assembly.MainModule.Types.First(x => x.Name == "Application");

                    var voidType = assembly.MainModule.ImportReference(typeof(void));
                    var cctor = new MethodDefinition(".cctor",
                        MethodAttributes.Static
                        | MethodAttributes.Private
                        | MethodAttributes.HideBySig
                        | MethodAttributes.SpecialName
                        | MethodAttributes.RTSpecialName,
                        voidType);

                    var ilp = cctor.Body.GetILProcessor();
                    ilp.Append(ilp.Create(OpCodes.Call, injectMethod));
                    ilp.Append(ilp.Create(OpCodes.Ret));

                    sceneManager.Methods.Add(cctor);
                }
            }
        }

        internal static Assembly LocalResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name == "0Harmony, Version=1.1.0.0, Culture=neutral, PublicKeyToken=null")
                return Assembly.LoadFile(Path.Combine(CurrentExecutingAssemblyDirectoryPath, "0Harmony.dll"));

            if (Utility.TryResolveDllAssembly(args.Name, CurrentExecutingAssemblyDirectoryPath, out var assembly) ||
                Utility.TryResolveDllAssembly(args.Name, PatcherPluginPath, out assembly) ||
                Utility.TryResolveDllAssembly(args.Name, PluginPath, out assembly))
                return assembly;

            return null;
        }
    }
}
