using System.Diagnostics;
using System.Reflection;

namespace BepInEx.Bootstrap
{
	public static class Linker
	{
		public static void StartBepInEx()
		{
			var property = typeof(Paths)
				.GetProperty("ExecutablePath", BindingFlags.Static | BindingFlags.Public)
				?.GetSetMethod(true);

			property?.Invoke(null, new object[] {Process.GetCurrentProcess().MainModule.FileName});
			
			Chainloader.Initialize();
		}
	}
}