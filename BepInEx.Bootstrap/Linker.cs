using System.Diagnostics;

namespace BepInEx.Bootstrap
{
	public static class Linker
	{
		public static void StartBepInEx()
		{
			Chainloader.Initialize(Process.GetCurrentProcess().MainModule.FileName);
			Chainloader.Start();
		}
	}
}