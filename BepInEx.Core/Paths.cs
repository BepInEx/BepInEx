using System.IO;
using System.Reflection;
using BepInEx.Core;
using MonoMod.Utils;

namespace BepInEx
{
	/// <summary>
	///     Paths used by BepInEx
	/// </summary>
	public static class Paths
	{
		public static void SetExecutablePath(string executablePath, string bepinRootPath = null)
		{
			ExecutablePath = executablePath;
			ProcessName = Path.GetFileNameWithoutExtension(executablePath);

			GameRootPath = PlatformHelper.Is(Platform.MacOS)
				? Utility.ParentDirectory(executablePath, 4)
				: Path.GetDirectoryName(executablePath);

			BepInExRootPath = bepinRootPath ?? Path.Combine(GameRootPath, "BepInEx");
			ConfigPath = Path.Combine(BepInExRootPath, "config");
			BepInExConfigPath = Path.Combine(ConfigPath, "BepInEx.cfg");
			PluginPath = Path.Combine(BepInExRootPath, "plugins");
			PatcherPluginPath = Path.Combine(BepInExRootPath, "patchers");
			BepInExAssemblyDirectory = Path.Combine(BepInExRootPath, "core");
			BepInExAssemblyPath = Path.Combine(BepInExAssemblyDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.dll");
			CachePath = Path.Combine(BepInExRootPath, "cache");
		}

		internal static void SetPluginPath(string pluginPath)
		{
			PluginPath = Utility.CombinePaths(BepInExRootPath, pluginPath);
		}

		public static SemVersion BepInExVersion { get; } = SemVersion.Parse(typeof(Paths).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);

		/// <summary>
		///     The directory that the core BepInEx DLLs reside in.
		/// </summary>
		public static string BepInExAssemblyDirectory { get; private set; }

		/// <summary>
		///     The path to the core BepInEx DLL.
		/// </summary>
		public static string BepInExAssemblyPath { get; private set; }

		/// <summary>
		///     The path to the main BepInEx folder.
		/// </summary>
		public static string BepInExRootPath { get; private set; }

		/// <summary>
		///     The path of the currently executing program BepInEx is encapsulated in.
		/// </summary>
		public static string ExecutablePath { get; private set; }

		/// <summary>
		///     The directory that the currently executing process resides in.
		///		<para>On OSX however, this is the parent directory of the game.app folder.</para>
		/// </summary>
		public static string GameRootPath { get; private set; }

		/// <summary>
		///		The path to the config directory.
		/// </summary>
		public static string ConfigPath { get; private set; }

		/// <summary>
		///		The path to the global BepInEx configuration file.
		/// </summary>
		public static string BepInExConfigPath { get; private set; }

		/// <summary>
        ///		The path to temporary cache files.
        /// </summary>
		public static string CachePath { get; private set; }

		/// <summary>
		///     The path to the patcher plugin folder which resides in the BepInEx folder.
		/// </summary>
		public static string PatcherPluginPath { get; private set; }

		/// <summary>
		///     The path to the plugin folder which resides in the BepInEx folder.
		/// <para>
		///		This is ONLY guaranteed to be set correctly when Chainloader has been initialized.
		/// </para>
		/// </summary>
		public static string PluginPath { get; private set; }

		/// <summary>
		///     The name of the currently executing process.
		/// </summary>
		public static string ProcessName { get; private set; }
	}
}