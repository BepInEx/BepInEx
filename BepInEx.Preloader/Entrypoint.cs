using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Preloader.RuntimeFixes;

namespace BepInEx.Preloader
{
	internal static class PreloaderRunner
	{
		public static void PreloaderPreMain(string[] args)
		{
			string bepinPath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(EnvVars.DOORSTOP_INVOKE_DLL_PATH)));

			Paths.SetExecutablePath(args[0], bepinPath, EnvVars.DOORSTOP_MANAGED_FOLDER_DIR);
			AppDomain.CurrentDomain.AssemblyResolve += LocalResolve;

			PreloaderMain();
		}

		private static void PreloaderMain()
		{
			if (Preloader.ConfigApplyRuntimePatches.Value)
				XTermFix.Apply();

			Preloader.Run();
		}

		private static Assembly LocalResolve(object sender, ResolveEventArgs args)
		{
			var assemblyName = new AssemblyName(args.Name);

			var foundAssembly = AppDomain.CurrentDomain.GetAssemblies()
										 .FirstOrDefault(x => x.GetName().Name == assemblyName.Name);

			if (foundAssembly != null)
				return foundAssembly;

			if (Utility.TryResolveDllAssembly(assemblyName, Paths.BepInExAssemblyDirectory, out foundAssembly)
				|| Utility.TryResolveDllAssembly(assemblyName, Paths.PatcherPluginPath, out foundAssembly)
				|| Utility.TryResolveDllAssembly(assemblyName, Paths.PluginPath, out foundAssembly))
				return foundAssembly;

			return null;
		}
	}

	internal static class Entrypoint
	{
		private static string preloaderPath;

		/// <summary>
		///     The main entrypoint of BepInEx, called from Doorstop.
		/// </summary>
		/// <param name="args">
		///     The arguments passed in from Doorstop. First argument is the path of the currently executing
		///     process.
		/// </param>
		public static void Main(string[] args)
		{
			// We set it to the current directory first as a fallback, but try to use the same location as the .exe file.
			string silentExceptionLog = $"preloader_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";

			try
			{
				EnvVars.LoadVars();

				silentExceptionLog = Path.Combine(Path.GetDirectoryName(args[0]), silentExceptionLog);

				// Get the path of this DLL via Doorstop env var because Assembly.Location mangles non-ASCII characters on some versions of Mono for unknown reasons
				preloaderPath = Path.GetDirectoryName(Path.GetFullPath(EnvVars.DOORSTOP_INVOKE_DLL_PATH));

				AppDomain.CurrentDomain.AssemblyResolve += ResolveCurrentDirectory;

				// In some versions of Unity 4, Mono tries to resolve BepInEx.dll prematurely because of the call to Paths.SetExecutablePath
				// To prevent that, we have to use reflection and a separate startup class so that we can install required assembly resolvers before the main code
				typeof(Entrypoint).Assembly.GetType($"BepInEx.Preloader.{nameof(PreloaderRunner)}")
								  ?.GetMethod(nameof(PreloaderRunner.PreloaderPreMain))
								  ?.Invoke(null, new object[] { args });

				AppDomain.CurrentDomain.AssemblyResolve -= ResolveCurrentDirectory;
			}
			catch (Exception ex)
			{
				File.WriteAllText(silentExceptionLog, ex.ToString());
			}
		}

		private static Assembly ResolveCurrentDirectory(object sender, ResolveEventArgs args)
		{
			var name = new AssemblyName(args.Name);

			try
			{
				return Assembly.LoadFile(Path.Combine(preloaderPath, $"{name.Name}.dll"));
			}
			catch (Exception)
			{
				return null;
			}
		}

	}
}