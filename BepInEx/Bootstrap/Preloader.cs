using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityInjector.ConsoleUtil;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace BepInEx.Bootstrap
{
	/// <summary>
	///     The main entrypoint of BepInEx, and initializes all patchers and the chainloader.
	/// </summary>
	internal static class Preloader
	{
		/// <summary>
		///     The list of finalizers that were loaded from the patcher contract.
		/// </summary>
		public static List<Action> Finalizers { get; } = new List<Action>();

		/// <summary>
		///     The list of initializers that were loaded from the patcher contract.
		/// </summary>
		public static List<Action> Initializers { get; } = new List<Action>();

		/// <summary>
		///     The dictionary of currently loaded patchers. The key is the patcher delegate that will be used to patch, and the
		///     value is a list of filenames of assemblies that the patcher is targeting.
		/// </summary>
		public static Dictionary<AssemblyPatcherDelegate, IEnumerable<string>> PatcherDictionary { get; } =
			new Dictionary<AssemblyPatcherDelegate, IEnumerable<string>>();

		/// <summary>
		///     The log writer that is specific to the preloader.
		/// </summary>
		public static PreloaderLogWriter PreloaderLog { get; private set; }

		public static void Run()
		{
			try
			{
				AllocateConsole();

				PreloaderLog =
					new PreloaderLogWriter(Utility.SafeParseBool(Config.GetEntry("preloader-logconsole", "false", "BepInEx")));
				PreloaderLog.Enabled = true;

				string consoleTile =
					$"BepInEx {Assembly.GetExecutingAssembly().GetName().Version} - {Process.GetCurrentProcess().ProcessName}";
				ConsoleWindow.Title = consoleTile;

				Logger.SetLogger(PreloaderLog);

				PreloaderLog.WriteLine(consoleTile);

				#if DEBUG

				object[] attributes = typeof(DebugInfoAttribute).Assembly.GetCustomAttributes(typeof(DebugInfoAttribute), false);
				
				if (attributes.Length > 0)
				{
					var attribute = (DebugInfoAttribute)attributes[0];

					PreloaderLog.WriteLine(attribute.Info);
				}

				#endif

				Logger.Log(LogLevel.Message, "Preloader started");

				string entrypointAssembly = Config.GetEntry("entrypoint-assembly", "UnityEngine.dll", "Preloader");

				AddPatcher(new[] {entrypointAssembly}, PatchEntrypoint);

				if (Directory.Exists(Paths.PatcherPluginPath))
				{
					var sortedPatchers = new SortedDictionary<string, KeyValuePair<AssemblyPatcherDelegate, IEnumerable<string>>>();

					foreach (string assemblyPath in Directory.GetFiles(Paths.PatcherPluginPath, "*.dll"))
						try
						{
							var assembly = Assembly.LoadFrom(assemblyPath);

							foreach (KeyValuePair<string, KeyValuePair<AssemblyPatcherDelegate, IEnumerable<string>>> kv in GetPatcherMethods(assembly))
							    try
							    {
							        sortedPatchers.Add(kv.Key, kv.Value);
							    }
							    catch (ArgumentException)
							    {
                                    Logger.Log(LogLevel.Warning, $"Found duplicate of patcher {kv.Key}!");
							    }
						}
						catch (BadImageFormatException) { } //unmanaged DLL
						catch (ReflectionTypeLoadException) { } //invalid references

					foreach (KeyValuePair<string, KeyValuePair<AssemblyPatcherDelegate, IEnumerable<string>>> kv in sortedPatchers)
						AddPatcher(kv.Value.Value, kv.Value.Key);
				}

				AssemblyPatcher.PatchAll(Paths.ManagedPath, PatcherDictionary, Initializers, Finalizers);
			}
			catch (Exception ex)
			{
				Logger.Log(LogLevel.Fatal, "Could not run preloader!");
				Logger.Log(LogLevel.Fatal, ex);

				PreloaderLog.Enabled = false;

				try
				{
					if (!ConsoleWindow.IsAttatched)
					{
						//if we've already attached the console, then the log will already be written to the console
						AllocateConsole();
						Console.Write(PreloaderLog);
					}
				}
				finally
				{
					File.WriteAllText(Path.Combine(Paths.GameRootPath, $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log"),
						PreloaderLog.ToString());

					PreloaderLog.Dispose();
				}
			}
		}

		/// <summary>
		///     Scans the assembly for classes that use the patcher contract, and returns a dictionary of the patch methods.
		/// </summary>
		/// <param name="assembly">The assembly to scan.</param>
		/// <returns>A dictionary of delegates which will be used to patch the targeted assemblies.</returns>
		public static Dictionary<string, KeyValuePair<AssemblyPatcherDelegate, IEnumerable<string>>> GetPatcherMethods(Assembly assembly)
		{
			var patcherMethods = new Dictionary<string, KeyValuePair<AssemblyPatcherDelegate, IEnumerable<string>>>();

			foreach (var type in assembly.GetExportedTypes())
				try
				{
					if (type.IsInterface)
						continue;

					var targetsProperty = type.GetProperty("TargetDLLs",
						BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase,
						null,
						typeof(IEnumerable<string>),
						Type.EmptyTypes,
						null);

					//first try get the ref patcher method
					var patcher = type.GetMethod("Patch",
						BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase,
						null,
						CallingConventions.Any,
						new[] {typeof(AssemblyDefinition).MakeByRefType()},
						null);

					if (patcher == null) //otherwise try getting the non-ref patcher method
						patcher = type.GetMethod("Patch",
							BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase,
							null,
							CallingConventions.Any,
							new[] {typeof(AssemblyDefinition)},
							null);

					if (targetsProperty == null || !targetsProperty.CanRead || patcher == null)
						continue;

					AssemblyPatcherDelegate patchDelegate = (ref AssemblyDefinition ass) =>
					{
						//we do the array fuckery here to get the ref result out
						object[] args = {ass};

						patcher.Invoke(null, args);

						ass = (AssemblyDefinition) args[0];
					};

					var targets = (IEnumerable<string>) targetsProperty.GetValue(null, null);

					patcherMethods[$"{assembly.GetName().Name}{type.FullName}"] = new KeyValuePair<AssemblyPatcherDelegate, IEnumerable<string>>(patchDelegate, targets);

					var initMethod = type.GetMethod("Initialize",
						BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase,
						null,
						CallingConventions.Any,
						Type.EmptyTypes,
						null);

					if (initMethod != null)
						Initializers.Add(() => initMethod.Invoke(null, null));

					var finalizeMethod = type.GetMethod("Finish",
						BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase,
						null,
						CallingConventions.Any,
						Type.EmptyTypes,
						null);

					if (finalizeMethod != null)
						Finalizers.Add(() => finalizeMethod.Invoke(null, null));
				}
				catch (Exception ex)
				{
					Logger.Log(LogLevel.Warning, $"Could not load patcher methods from {assembly.GetName().Name}");
					Logger.Log(LogLevel.Warning, $"{ex}");
				}

			Logger.Log(LogLevel.Info,
				$"Loaded {patcherMethods.Select(x => x.Key).Distinct().Count()} patcher methods from {assembly.GetName().Name}");

			return patcherMethods;
		}

		/// <summary>
		///     Inserts BepInEx's own chainloader entrypoint into UnityEngine.
		/// </summary>
		/// <param name="assembly">The assembly that will be attempted to be patched.</param>
		public static void PatchEntrypoint(ref AssemblyDefinition assembly)
		{
			if (assembly.MainModule.AssemblyReferences.Any(x => x.Name.Contains("BepInEx")))
			{
				throw new Exception("BepInEx has been detected to be patched! Please unpatch before using a patchless variant!");
			}

			string entrypointType = Config.GetEntry("entrypoint-type", "Application", "Preloader");
			string entrypointMethod = Config.GetEntry("entrypoint-method", ".cctor", "Preloader");

			bool isCctor = entrypointMethod.IsNullOrWhiteSpace() || entrypointMethod == ".cctor";


			var entryType = assembly.MainModule.Types.FirstOrDefault(x => x.Name == entrypointType);

			if (entryType == null)
			{
				throw new Exception("The entrypoint type is invalid! Please check your config.ini");
			}
			
			using (var injected = AssemblyDefinition.ReadAssembly(Paths.BepInExAssemblyPath))
			{
				var originalInitMethod = injected.MainModule.Types.First(x => x.Name == "Chainloader").Methods
					.First(x => x.Name == "Initialize");

				var originalStartMethod = injected.MainModule.Types.First(x => x.Name == "Chainloader").Methods
					.First(x => x.Name == "Start");

				var initMethod = assembly.MainModule.ImportReference(originalInitMethod);
				var startMethod = assembly.MainModule.ImportReference(originalStartMethod);
				
				List<MethodDefinition> methods = new List<MethodDefinition>();

				if (isCctor)
				{
					MethodDefinition cctor = entryType.Methods.FirstOrDefault(m => m.IsConstructor && m.IsStatic);

					if (cctor == null)
					{
						cctor = new MethodDefinition(".cctor",
							MethodAttributes.Static | MethodAttributes.Private | MethodAttributes.HideBySig
							| MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
							assembly.MainModule.ImportReference(typeof(void)));

						entryType.Methods.Add(cctor);
						ILProcessor il = cctor.Body.GetILProcessor();
						il.Append(il.Create(OpCodes.Ret));
					}

					methods.Add(cctor);
				}
				else
				{
					methods.AddRange(entryType.Methods.Where(x => x.Name == entrypointMethod));
				}

				if (!methods.Any())
				{
					throw new Exception("The entrypoint method is invalid! Please check your config.ini");
				}

				foreach (var method in methods)
				{
					var il = method.Body.GetILProcessor();

					Instruction ins = il.Body.Instructions.First();
						
					il.InsertBefore(ins, il.Create(OpCodes.Ldstr, Paths.ExecutablePath)); //containerExePath
					il.InsertBefore(ins, il.Create(OpCodes.Ldc_I4_0)); //startConsole (always false, we already load the console in Preloader)
					il.InsertBefore(ins, il.Create(OpCodes.Call, initMethod)); //Chainloader.Initialize(string containerExePath, bool startConsole = true)
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

				var encoding = (uint) Encoding.UTF8.CodePage;

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

		/// <summary>
		///     Adds the patcher to the patcher dictionary.
		/// </summary>
		/// <param name="dllNames">The list of DLL filenames to be patched.</param>
		/// <param name="patcher">The method that will perform the patching.</param>
		public static void AddPatcher(IEnumerable<string> dllNames, AssemblyPatcherDelegate patcher)
		{
			PatcherDictionary[patcher] = dllNames;
		}
	}
}