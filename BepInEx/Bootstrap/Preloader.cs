using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Common;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityInjector.ConsoleUtil;
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

        public static PreloaderLogWriter PreloaderLog { get; private set; }

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

        private static bool TryGetConfigBool(string key, string defaultValue)
        {
            try
            {
                string result = Config.GetEntry(key, defaultValue);

                return bool.Parse(result);
            }
            catch
            {
                return false;
            }
        }

        internal static void AllocateConsole()
        {
            bool console = TryGetConfigBool("console", "false");
            bool shiftjis = TryGetConfigBool("console-shiftjis", "false");

            if (console)
            {
                try
                {
                    ConsoleWindow.Attach();

                    uint encoding = (uint) Encoding.UTF8.CodePage;

                    if (shiftjis)
                        encoding = 932;

                    ConsoleEncoding.ConsoleCodePage = encoding;
                    Console.OutputEncoding = ConsoleEncoding.GetEncoding(encoding);
                }
                catch (Exception ex)
                {
                    Logger.Log(LogLevel.Error, "Failed to allocate console!");
                    Logger.Log(LogLevel.Error, ex);
                }
            }
        }

        public static void Main(string[] args)
        {
            try
            {
                AppDomain.CurrentDomain.AssemblyResolve += LocalResolve;
                ExecutablePath = args[0];

                AllocateConsole();

                PreloaderLog = new PreloaderLogWriter(TryGetConfigBool("preloader-logconsole", "false"));
                PreloaderLog.Enabled = true;

                string consoleTile = $"BepInEx {Assembly.GetExecutingAssembly().GetName().Version} - {Process.GetCurrentProcess().ProcessName}";
                ConsoleWindow.Title = consoleTile;
                

                Logger.SetLogger(PreloaderLog);
                
                PreloaderLog.WriteLine(consoleTile);
                Logger.Log(LogLevel.Message, "Preloader started");


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
                Logger.Log(LogLevel.Fatal, "Could not run preloader!");
                Logger.Log(LogLevel.Fatal, ex);

                PreloaderLog.Enabled = false;

                try
                {
                    UnityInjector.ConsoleUtil.ConsoleWindow.Attach();
                    Console.Write(PreloaderLog);
                }
                finally
                {
                    File.WriteAllText(Path.Combine(GameRootPath, $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log"),
                        PreloaderLog.ToString());

                    PreloaderLog.Dispose();
                }
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

					//first try get the ref patcher method
		            MethodInfo patcher = type.GetMethod(
			            "Patch",
			            BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase,
			            null,
			            CallingConventions.Any,
			            new[] {typeof(AssemblyDefinition).MakeByRefType()},
			            null);

		            if (patcher == null) //otherwise try getting the non-ref patcher method
		            {
			            patcher = type.GetMethod(
				            "Patch",
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
	                    object[] args = { ass };

	                    patcher.Invoke(null, args);

	                    ass = (AssemblyDefinition)args[0];
                    };

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
                    Logger.Log(LogLevel.Warning, $"Could not load patcher methods from {assembly.GetName().Name}");
                    Logger.Log(LogLevel.Warning, $"{ex}");
                }
            }

            Logger.Log(LogLevel.Info, $"Loaded {patcherMethods.SelectMany(x => x.Value).Distinct().Count()} patcher methods from {assembly.GetName().Name}");

            return patcherMethods;
        }

        internal static void PatchEntrypoint(ref AssemblyDefinition assembly)
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
            AssemblyName assemblyName = new AssemblyName(args.Name);

            var foundAssembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == assemblyName.Name);

            if (foundAssembly != null)
                return foundAssembly;

            if (Utility.TryResolveDllAssembly(assemblyName, CurrentExecutingAssemblyDirectoryPath, out foundAssembly) ||
                Utility.TryResolveDllAssembly(assemblyName, PatcherPluginPath, out foundAssembly) ||
                Utility.TryResolveDllAssembly(assemblyName, PluginPath, out foundAssembly))
                return foundAssembly;

            return null;
        }
    }
}
