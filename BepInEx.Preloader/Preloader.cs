using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using BepInEx.Preloader.Patching;
using BepInEx.Preloader.RuntimeFixes;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityInjector.ConsoleUtil;
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

		public static void Run()
		{
			try
			{
				AllocateConsole();

				UnityPatches.Apply();

				Logger.Sources.Add(TraceLogSource.CreateSource());

				PreloaderLog = new PreloaderConsoleListener(Utility.SafeParseBool(Config.GetEntry("preloader-logconsole", "false", "BepInEx")));

				Logger.Listeners.Add(PreloaderLog);


				string consoleTile = $"BepInEx {typeof(Paths).Assembly.GetName().Version} - {Process.GetCurrentProcess().ProcessName}";

				ConsoleWindow.Title = consoleTile;
				Logger.LogMessage(consoleTile);

				//See BuildInfoAttribute for more information about this section.
				object[] attributes = typeof(BuildInfoAttribute).Assembly.GetCustomAttributes(typeof(BuildInfoAttribute), false);

				if (attributes.Length > 0)
				{
					var attribute = (BuildInfoAttribute)attributes[0];

					Logger.LogMessage(attribute.Info);
				}

#if UNITY_2018
				Logger.LogMessage("Compiled in Unity v2018 mode");
#else
				Logger.LogMessage("Compiled in Legacy Unity mode");
#endif

				Logger.LogInfo($"Running under Unity v{Process.GetCurrentProcess().MainModule.FileVersionInfo.FileVersion}");

				Logger.LogMessage("Preloader started");

				string entrypointAssembly = Config.GetEntry("entrypoint-assembly", "UnityEngine.dll", "Preloader");

				AssemblyPatcher.AddPatcher(new PatcherPlugin
					{ TargetDLLs = new[] { entrypointAssembly },
						Patcher = PatchEntrypoint,
						Name = "BepInEx.Chainloader"
					});

				AssemblyPatcher.AddPatchersFromDirectory(Paths.PatcherPluginPath, GetPatcherMethods);

				Logger.LogInfo($"{AssemblyPatcher.PatcherPlugins.Count} patcher plugin(s) loaded");

				AssemblyPatcher.PatchAndLoad(Paths.ManagedPath);

				AssemblyPatcher.DisposePatchers();

				Logger.LogMessage("Preloader finished");

				Logger.Listeners.Remove(PreloaderLog);
				Logger.Listeners.Add(new ConsoleLogListener());

				PreloaderLog.Dispose();
			}
			catch (Exception ex)
			{
				try
				{
					Logger.LogFatal("Could not run preloader!");
					Logger.LogFatal(ex);

					PreloaderLog?.Dispose();

					if (!ConsoleWindow.IsAttached)
					{
						//if we've already attached the console, then the log will already be written to the console
						AllocateConsole();
						Console.Write(PreloaderLog);
					}

					PreloaderLog = null;
				}
				finally
				{
					File.WriteAllText(
						Path.Combine(Paths.GameRootPath, $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log"),
						PreloaderLog + "\r\n" + ex);

					PreloaderLog?.Dispose();
					PreloaderLog = null;
				}
			}
		}

		/// <summary>
		///     Scans the assembly for classes that use the patcher contract, and returns a list of valid patchers.
		/// </summary>
		/// <param name="assembly">The assembly to scan.</param>
		/// <returns>A list of assembly patchers that were found in the assembly.</returns>
		public static List<PatcherPlugin> GetPatcherMethods(Assembly assembly)
		{
			var patcherMethods = new List<PatcherPlugin>();
			var flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase;

			foreach (var type in assembly.GetExportedTypes())
				try
				{
					if (type.IsInterface)
						continue;

					var targetsProperty = type.GetProperty("TargetDLLs",
						flags,
						null,
						typeof(IEnumerable<string>),
						Type.EmptyTypes,
						null);

					//first try get the ref patcher method
					var patcher = type.GetMethod("Patch",
						flags,
						null,
						CallingConventions.Any,
						new[] { typeof(AssemblyDefinition).MakeByRefType() },
						null);

					if (patcher == null) //otherwise try getting the non-ref patcher method
						patcher = type.GetMethod("Patch",
							flags,
							null,
							CallingConventions.Any,
							new[] { typeof(AssemblyDefinition) },
							null);

					if (targetsProperty == null || !targetsProperty.CanRead || patcher == null)
						continue;

					var assemblyPatcher = new PatcherPlugin();

					assemblyPatcher.Name = $"{assembly.GetName().Name}/{type.FullName}";
					assemblyPatcher.Patcher = (ref AssemblyDefinition ass) =>
					{
						//we do the array fuckery here to get the ref result out
						object[] args = { ass };

						patcher.Invoke(null, args);

						ass = (AssemblyDefinition)args[0];
					};

					assemblyPatcher.TargetDLLs = (IEnumerable<string>)targetsProperty.GetValue(null, null);

					var initMethod = type.GetMethod("Initialize",
						flags,
						null,
						CallingConventions.Any,
						Type.EmptyTypes,
						null);

					if (initMethod != null)
						assemblyPatcher.Initializer = () => initMethod.Invoke(null, null);

					var finalizeMethod = type.GetMethod("Finish",
						flags,
						null,
						CallingConventions.Any,
						Type.EmptyTypes,
						null);

					if (finalizeMethod != null)
						assemblyPatcher.Finalizer = () => finalizeMethod.Invoke(null, null);

					patcherMethods.Add(assemblyPatcher);
				}
				catch (Exception ex)
				{
					Logger.LogWarning($"Could not load patcher methods from {assembly.GetName().Name}");
					Logger.LogWarning(ex);
				}

			Logger.Log(patcherMethods.Count > 0 ? LogLevel.Info : LogLevel.Debug,
				$"Loaded {patcherMethods.Count} patcher methods from {assembly.GetName().Name}");

			return patcherMethods;
		}

		/// <summary>
		///     Inserts BepInEx's own chainloader entrypoint into UnityEngine.
		/// </summary>
		/// <param name="assembly">The assembly that will be attempted to be patched.</param>
		public static void PatchEntrypoint(ref AssemblyDefinition assembly)
		{
			if (assembly.MainModule.AssemblyReferences.Any(x => x.Name.Contains("BepInEx")))
				throw new Exception("BepInEx has been detected to be patched! Please unpatch before using a patchless variant!");

			string entrypointType = Config.GetEntry("entrypoint-type", "Application", "Preloader");
			string entrypointMethod = Config.GetEntry("entrypoint-method", ".cctor", "Preloader");

			bool isCctor = entrypointMethod.IsNullOrWhiteSpace() || entrypointMethod == ".cctor";


			var entryType = assembly.MainModule.Types.FirstOrDefault(x => x.Name == entrypointType);

			if (entryType == null)
				throw new Exception("The entrypoint type is invalid! Please check your config.ini");

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

					il.InsertBefore(ins, il.Create(OpCodes.Ldstr, Paths.ExecutablePath)); //containerExePath
					il.InsertBefore(ins,
						il.Create(OpCodes
							.Ldc_I4_0)); //startConsole (always false, we already load the console in Preloader)
					il.InsertBefore(ins,
						il.Create(OpCodes.Call,
							initMethod)); //Chainloader.Initialize(string containerExePath, bool startConsole = true)
					il.InsertBefore(ins, il.Create(OpCodes.Call, startMethod));
				}
			}
		}

		/// <summary>
		///     Allocates a console window for use by BepInEx safely.
		/// </summary>
		public static void AllocateConsole()
		{
			bool console = Utility.SafeParseBool(Config.GetEntry("console", "false", "BepInEx"));
			bool shiftjis = Utility.SafeParseBool(Config.GetEntry("console-shiftjis", "false", "BepInEx"));

			if (!console)
				return;

			try
			{
				ConsoleWindow.Attach();

				var encoding = (uint)Encoding.UTF8.CodePage;

				if (shiftjis)
					encoding = 932;

				ConsoleEncoding.ConsoleCodePage = encoding;
				Console.OutputEncoding = ConsoleEncoding.GetEncoding(encoding);
			}
			catch (Exception ex)
			{
				Logger.LogError("Failed to allocate console!");
				Logger.LogError(ex);
			}
		}
	}
}