using BepInEx.Logging;

namespace BepInEx.Preloader.Core;

internal static class PreloaderLogger
{
    public static ManualLogSource Log { get; } = Logger.CreateLogSource("Preloader");
}
