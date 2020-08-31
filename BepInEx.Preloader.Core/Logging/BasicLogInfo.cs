using System.Diagnostics;
using BepInEx.Logging;

namespace BepInEx.Preloader.Core.Logging
{
	public static class BasicLogInfo
	{
		public static void PrintLogInfo(ManualLogSource log)
		{
			string consoleTile = $"BepInEx {typeof(Paths).Assembly.GetName().Version} - {Process.GetCurrentProcess().ProcessName}";
			log.LogMessage(consoleTile);

			if (ConsoleManager.ConsoleActive)
				ConsoleManager.SetConsoleTitle(consoleTile);

			//See BuildInfoAttribute for more information about this section.
			object[] attributes = typeof(BuildInfoAttribute).Assembly.GetCustomAttributes(typeof(BuildInfoAttribute), false);

			if (attributes.Length > 0)
			{
				var attribute = (BuildInfoAttribute)attributes[0];
				log.LogMessage(attribute.Info);
			}
		}
	}
}
