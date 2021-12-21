using BepInEx.Logging;

namespace BepInEx.Preloader.Core;

public static class PreloaderLogger
{
    public static ManualLogSource Log { get; } = Logger.CreateLogSource("Preloader");
}
