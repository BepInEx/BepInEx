using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Logging;
using BepInEx.Preloader.Core;

namespace BepInEx.NetLauncher
{
	internal static class NetPreloaderRunner
	{
		internal static void PreloaderMain(string[] args)
		{
			PlatformUtils.SetPlatform();

			Logger.Listeners.Add(new ConsoleLogListener());

			ConsoleManager.Initialize(true);

			NetPreloader.Start(args);
		}

		internal static void OuterMain(string[] args, string filename)
		{
			try
			{
				Paths.SetExecutablePath(filename);

				AppDomain.CurrentDomain.AssemblyResolve += LocalResolve;

				PreloaderMain(args);
			}
			catch (Exception ex)
			{
				PreloaderLogger.Log.LogFatal("Unhandled exception");
				PreloaderLogger.Log.LogFatal(ex);
				Program.ReadExit();
			}
		}

		private static Assembly LocalResolve(object sender, ResolveEventArgs args)
		{
			var assemblyName = new AssemblyName(args.Name);

			var foundAssembly = AppDomain.CurrentDomain.GetAssemblies()
										 .FirstOrDefault(x => x.GetName().Name == assemblyName.Name);

			if (foundAssembly != null)
				return foundAssembly;

			if (LocalUtility.TryResolveDllAssembly(assemblyName, Paths.BepInExAssemblyDirectory, out foundAssembly)
				|| LocalUtility.TryResolveDllAssembly(assemblyName, Paths.PatcherPluginPath, out foundAssembly)
				|| LocalUtility.TryResolveDllAssembly(assemblyName, Paths.PluginPath, out foundAssembly))
				return foundAssembly;

			return null;
		}
	}

	class Program
	{
		internal static void ReadExit()
		{
			Console.WriteLine("Press enter to exit...");
			Console.ReadLine();
			Environment.Exit(-1);
		}

		static void Main(string[] args)
		{
			try
			{
				Console.WriteLine("test");

				string filename;

#if DEBUG
				filename = Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName));

#else
				filename = Process.GetCurrentProcess().MainModule.FileName;
#endif

				ResolveDirectories.Add(Path.Combine(Path.GetDirectoryName(filename), "BepInEx", "Core"));

				AppDomain.CurrentDomain.AssemblyResolve += RemoteResolve;

				NetPreloaderRunner.OuterMain(args, filename);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Unhandled exception");
				Console.WriteLine(ex);
				ReadExit();
			}
		}

		public static List<string> ResolveDirectories { get; set; } = new List<string>
		{
			"C:\\Windows\\Microsoft.NET\\assembly\\GAC_32\\Microsoft.Xna.Framework.Game\\v4.0_4.0.0.0__842cf8be1de50553\\"
		};

		private static Assembly RemoteResolve(object sender, ResolveEventArgs reference)
		{
			var assemblyName = new AssemblyName(reference.Name);

			foreach (var directory in ResolveDirectories)
			{
				var potentialDirectories = new List<string> { directory };

				potentialDirectories.AddRange(Directory.GetDirectories(directory, "*", SearchOption.AllDirectories));

				var potentialFiles = potentialDirectories.Select(x => Path.Combine(x, $"{assemblyName.Name}.dll"))
														 .Concat(potentialDirectories.Select(x => Path.Combine(x, $"{assemblyName.Name}.exe")));

				foreach (string path in potentialFiles)
				{
					if (!File.Exists(path))
						continue;

					var assembly = Assembly.LoadFrom(path);

					if (assembly.GetName().Name == assemblyName.Name)
						return assembly;
				}
			}

			return null;
		}
	}
}