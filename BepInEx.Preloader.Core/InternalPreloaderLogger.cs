using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Logging;

namespace BepInEx.Preloader.Core
{
	public static class PreloaderLogger
	{
		public static ManualLogSource Log { get; } = Logger.CreateLogSource("Preloader");
	}
}
