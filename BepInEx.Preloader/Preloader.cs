using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Preloader.Patching;
using BepInEx.Preloader.RuntimeFixes;
using HarmonyLib;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MonoMod.Utils;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace BepInEx.Preloader
{
	/// <summary>
	///     The main entrypoint of BepInEx, and initializes all patchers and the chainloader.
	/// </summary>
	internal static class Preloader
	{
		/// <summary>
		///     The log writer that is specific to the preloader.
		/// </summary>
		private static PreloaderConsoleListener PreloaderLog { get; set; }

		public static bool IsPostUnity2017 { get; } = File.Exists(Path.Combine(Paths.ManagedPath, "UnityEngine.CoreModule.dll"));

		public static void Run()
		{
			try
			{
				ConsoleManager.Initialize(false);
				AllocateConsole();

				Utility.TryDo(() =>
				{
					if (ConfigApplyRuntimePatches.Value)
						UnityPatches.Apply();
				}, out var runtimePatchException);

				Logger.InitializeInternalLoggers();
				Logger.Sources.Add(TraceLogSource.CreateSource());
				
				PreloaderLog = new PreloaderConsoleListener();
				Logger.Listeners.Add(PreloaderLog);

				string consoleTile = $"BepInEx {typeof(Paths).Assembly.GetName().Version} - {Paths.ProcessName}";

				if (ConsoleManager.ConsoleActive)
					ConsoleManager.SetConsoleTitle(consoleTile);

				Logger.LogMessage(consoleTile);

				//See BuildInfoAttribute for more information about this section.
				object[] attributes = typeof(BuildInfoAttribute).Assembly.GetCustomAttributes(typeof(BuildInfoAttribute), false);

				if (attributes.Length > 0)
				{
					var attribute = (BuildInfoAttribute)attributes[0];
					Logger.LogMessage(attribute.Info);
				}

				Logger.LogInfo($"Running under Unity v{GetUnityVersion()}");
				Logger.LogInfo($"CLR runtime version: {Environment.Version}");
				Logger.LogInfo($"Supports SRE: {Utility.CLRSupportsDynamicAssemblies}");

				if (runtimePatchException != null)
					Logger.LogWarning($"Failed to apply runtime patches for Mono. See more info in the output log. Error message: {runtimePatchException.Message}");

				Logger.LogMessage("Preloader started");

				AssemblyPatcher.AddPatcher(new PatcherPlugin
				{
					TargetDLLs = () => new[] { ConfigEntrypointAssembly.Value },
					Patcher = PatchEntrypoint,
					TypeName = "BepInEx.Chainloader"
				});

				AssemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath);

				Logger.LogInfo($"{AssemblyPatcher.PatcherPlugins.Count} patcher plugin(s) loaded");

				AssemblyPatcher.PatchAndLoad(Paths.ManagedPath);
				AssemblyPatcher.DisposePatchers();

				Logger.LogMessage("Preloader finished");

				Logger.Listeners.Remove(PreloaderLog);

				PreloaderLog.Dispose();
			}
			catch (Exception ex)
			{
				try
				{
					Logger.LogFatal("Could not run preloader!");
					Logger.LogFatal(ex);

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

			using (var injected = AssemblyDefinition.ReadAssembly(Paths.BepInExAssemblyPath))
			{
				var originalInitMethod = injected.MainModule.Types.First(x => x.Name == "Chainloader").Methods
												 .First(x => x.Name == "Initialize");

				var originalStartMethod = injected.MainModule.Types.First(x => x.Name == "Chainloader").Methods
												  .First(x => x.Name == "Start");

				var initMethod = assembly.MainModule.ImportReference(originalInitMethod);
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
						il.Create(OpCodes.Ldc_I4_0)); //startConsole (always false, we already load the console in Preloader)

					il.InsertBefore(ins,
						il.Create(OpCodes.Call, assembly.MainModule.ImportReference(
							AccessTools.PropertyGetter(typeof(PreloaderConsoleListener), nameof(PreloaderConsoleListener.LogEvents))))); // preloaderLogEvents (load from Preloader.PreloaderLog.LogEvents)

                    il.InsertBefore(ins,
						il.Create(OpCodes.Call, initMethod)); // Chainloader.Initialize(string gamePath, string managedPath = null, bool startConsole = true)

					il.InsertBefore(ins,
						il.Create(OpCodes.Call, startMethod));
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
				Logger.Listeners.Add(new ConsoleLogListener());
			}
			catch (Exception ex)
			{
				Logger.LogError("Failed to allocate console!");
				Logger.LogError(ex);
			}
		}

		public static string GetUnityVersion()
		{
			if (Utility.CurrentOs == Platform.Windows)
				return FileVersionInfo.GetVersionInfo(Paths.ExecutablePath).FileVersion;

			return $"Unknown ({(IsPostUnity2017 ? "post" : "pre")}-2017)";
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

		private static readonly ConfigEntry<bool> ConfigPreloaderCOutLogging = ConfigFile.CoreConfig.Bind(
			"Logging", "PreloaderConsoleOutRedirection",
			true,
			"Redirects text from Console.Out during preloader patch loading to the BepInEx logging system.");

		#endregion
	}
}