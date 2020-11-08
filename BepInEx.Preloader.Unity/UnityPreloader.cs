using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Core.Logging;
using BepInEx.Logging;
using BepInEx.Preloader.Core;
using BepInEx.Preloader.Core.Logging;
using BepInEx.Preloader.RuntimeFixes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace BepInEx.Preloader.Unity
{
	/// <summary>
	///     The main entrypoint of BepInEx, and initializes all patchers and the chainloader.
	/// </summary>
	internal static class UnityPreloader
	{
		/// <summary>
		///     The log writer that is specific to the preloader.
		/// </summary>
		private static PreloaderConsoleListener PreloaderLog { get; set; }

		private static ManualLogSource Log => PreloaderLogger.Log;

		public static string ManagedPath { get; private set; } = Utility.CombinePaths(Paths.GameRootPath, $"{Paths.ProcessName}_Data", "Managed");

		public static bool IsPostUnity2017 { get; } = File.Exists(Path.Combine(ManagedPath, "UnityEngine.CoreModule.dll"));

		public static void Run(string managedDirectory)
		{
			try
			{
				InitializeHarmony();

				ConsoleManager.Initialize(false);
				AllocateConsole();

				if (managedDirectory != null)
					ManagedPath = managedDirectory;

				
				Utility.TryDo(() =>
				{
					if (ConfigApplyRuntimePatches.Value)
						UnityPatches.Apply();
				}, out var runtimePatchException);

				Logger.Sources.Add(TraceLogSource.CreateSource());
				Logger.Sources.Add(new HarmonyLogSource());

				Logger.Listeners.Add(new ConsoleLogListener());
				PreloaderLog = new PreloaderConsoleListener();
				Logger.Listeners.Add(PreloaderLog);

				ChainloaderLogHelper.PrintLogInfo(Log);

				Log.LogInfo($"Running under Unity v{GetUnityVersion()}");
				Log.LogInfo($"CLR runtime version: {Environment.Version}");
				Log.LogInfo($"Supports SRE: {Utility.CLRSupportsDynamicAssemblies}");

				Log.LogDebug($"Game executable path: {Paths.ExecutablePath}");
				Log.LogDebug($"Unity Managed directory: {ManagedPath}");
				Log.LogDebug($"BepInEx root path: {Paths.BepInExRootPath}");

				if (runtimePatchException != null)
					Log.LogWarning($"Failed to apply runtime patches for Mono. See more info in the output log. Error message: {runtimePatchException.Message}");

				Log.LogMessage("Preloader started");

				TypeLoader.SearchDirectories.Add(ManagedPath);

				using (var assemblyPatcher = new AssemblyPatcher())
				{
					assemblyPatcher.PatcherPlugins.Add(new PatcherPlugin
					{
						TargetDLLs = () => new[] { ConfigEntrypointAssembly.Value },
						Patcher = PatchEntrypoint,
						TypeName = "BepInEx.Chainloader"
					});

					assemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath);

					Log.LogInfo($"{assemblyPatcher.PatcherPlugins.Count} patcher plugin{(assemblyPatcher.PatcherPlugins.Count == 1 ? "" : "s")} loaded");

					assemblyPatcher.LoadAssemblyDirectory(ManagedPath);

					Log.LogInfo($"{assemblyPatcher.PatcherPlugins.Count} assemblies discovered");

					assemblyPatcher.PatchAndLoad();
				}


				Log.LogMessage("Preloader finished");

				Logger.Listeners.Remove(PreloaderLog);

				PreloaderLog.Dispose();

				Logger.Listeners.Add(new StdOutLogListener());
			}
			catch (Exception ex)
			{
				try
				{
					Log.LogFatal("Could not run preloader!");
					Log.LogFatal(ex);

					if (!ConsoleManager.ConsoleActive)
					{
						//if we've already attached the console, then the log will already be written to the console
						AllocateConsole();
						Console.Write(PreloaderLog);
					}
				}
				catch { }

				string log = string.Empty;

				try
				{
					// We could use platform-dependent newlines, however the developers use Windows so this will be easier to read :)

					log = string.Join("\r\n", PreloaderConsoleListener.LogEvents.Select(x => x.ToString()).ToArray());
					log += "\r\n";

					PreloaderLog?.Dispose();
					PreloaderLog = null;
				}
				catch { }

				File.WriteAllText(
					Path.Combine(Paths.GameRootPath, $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log"),
					log + ex);
			}
		}

		/// <summary>
		///     Inserts BepInEx's own chainloader entrypoint into UnityEngine.
		/// </summary>
		/// <param name="assembly">The assembly that will be attempted to be patched.</param>
		public static void PatchEntrypoint(ref AssemblyDefinition assembly)
		{
			if (assembly.MainModule.AssemblyReferences.Any(x => x.Name.Contains("BepInEx")))
				throw new Exception("BepInEx has been detected to be patched! Please unpatch before using a patchless variant!");

			string entrypointType = ConfigEntrypointType.Value;
			string entrypointMethod = ConfigEntrypointMethod.Value;

			bool isCctor = entrypointMethod.IsNullOrWhiteSpace() || entrypointMethod == ".cctor";


			var entryType = assembly.MainModule.Types.FirstOrDefault(x => x.Name == entrypointType);

			if (entryType == null)
				throw new Exception("The entrypoint type is invalid! Please check your config/BepInEx.cfg file");

			string chainloaderAssemblyPath = Path.Combine(Paths.BepInExAssemblyDirectory, "BepInEx.Unity.dll");

			var readerParameters = new ReaderParameters
			{
				AssemblyResolver = TypeLoader.CecilResolver
			};

			using (var chainloaderAssemblyDefinition = AssemblyDefinition.ReadAssembly(chainloaderAssemblyPath, readerParameters))
			{
				var chainloaderType = chainloaderAssemblyDefinition.MainModule.Types.First(x => x.Name == "UnityChainloader");

				var originalStartMethod = chainloaderType.EnumerateAllMethods().First(x => x.Name == "StaticStart");

				var startMethod = assembly.MainModule.ImportReference(originalStartMethod);

				var methods = new List<MethodDefinition>();

				if (isCctor)
				{
					var cctor = entryType.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic);

					if (cctor == null)
					{
						cctor = new MethodDefinition(".cctor",
							MethodAttributes.Static | MethodAttributes.Private | MethodAttributes.HideBySig
							| MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
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
						il.Create(OpCodes.Ldnull)); // gameExePath (always null, we initialize the Paths class in Entrypoint

					il.InsertBefore(ins,
						il.Create(OpCodes.Call, startMethod)); // UnityChainloader.StaticStart(string gameExePath)
				}
			}
		}

		/// <summary>
		///     Allocates a console window for use by BepInEx safely.
		/// </summary>
		public static void AllocateConsole()
		{
			if (!ConsoleManager.ConfigConsoleEnabled.Value)
				return;

			try
			{
				ConsoleManager.CreateConsole();
			}
			catch (Exception ex)
			{
				Log.LogError("Failed to allocate console!");
				Log.LogError(ex);
			}
		}

		public static string GetUnityVersion()
		{
			if (PlatformHelper.Is(Platform.Windows))
				return FileVersionInfo.GetVersionInfo(Paths.ExecutablePath).FileVersion;

			return $"Unknown ({(IsPostUnity2017 ? "post" : "pre")}-2017)";
		}

		private static void InitializeHarmony()
		{
			switch (ConfigHarmonyBackend.Value)
			{
				case MonoModBackend.auto:
					break;
				case MonoModBackend.dynamicmethod:
				case MonoModBackend.methodbuilder:
				case MonoModBackend.cecil:
					Environment.SetEnvironmentVariable("MONOMOD_DMD_TYPE", ConfigHarmonyBackend.Value.ToString());
					break;
				default:
					throw new ArgumentOutOfRangeException(nameof(ConfigHarmonyBackend), ConfigHarmonyBackend.Value, "Unknown backend");
			}
		}

		private enum MonoModBackend
		{
			// Enum names are important!
			[Description("Auto")] auto = 0,
			[Description("DynamicMethod")] dynamicmethod,
			[Description("MethodBuilder")] methodbuilder,
			[Description("Cecil")] cecil
		}

		#region Config

		private static readonly ConfigEntry<string> ConfigEntrypointAssembly = ConfigFile.CoreConfig.Bind(
			"Preloader.Entrypoint", "Assembly",
			IsPostUnity2017 ? "UnityEngine.CoreModule.dll" : "UnityEngine.dll",
			"The local filename of the assembly to target.");

		private static readonly ConfigEntry<string> ConfigEntrypointType = ConfigFile.CoreConfig.Bind(
			"Preloader.Entrypoint", "Type",
			"Application",
			"The name of the type in the entrypoint assembly to search for the entrypoint method.");

		private static readonly ConfigEntry<string> ConfigEntrypointMethod = ConfigFile.CoreConfig.Bind(
			"Preloader.Entrypoint", "Method",
			".cctor",
			"The name of the method in the specified entrypoint assembly and type to hook and load Chainloader from.");

		internal static readonly ConfigEntry<bool> ConfigApplyRuntimePatches = ConfigFile.CoreConfig.Bind(
			"Preloader", "ApplyRuntimePatches",
			true,
			"Enables or disables runtime patches.\nThis should always be true, unless you cannot start the game due to a Harmony related issue (such as running .NET Standard runtime) or you know what you're doing.");

		private static readonly ConfigEntry<MonoModBackend> ConfigHarmonyBackend = ConfigFile.CoreConfig.Bind(
			"Preloader",
			"HarmonyBackend",
			MonoModBackend.auto,
			"Specifies which MonoMod backend to use for Harmony patches. Auto uses the best available backend.\nThis setting should only be used for development purposes (e.g. debugging in dnSpy). Other code might override this setting.");

		#endregion
	}
}