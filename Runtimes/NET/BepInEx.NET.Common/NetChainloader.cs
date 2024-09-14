using BepInEx.Preloader.Core.Logging;

namespace BepInEx.NET.Common;

/// <summary>
///     The .NET runtime specific chainloader
/// </summary>
internal class NetChainloader : Chainloader
{
    internal override void InitializeLoggers()
    {
        base.InitializeLoggers();

        ChainloaderLogHelper.RewritePreloaderLogs();
    }
}
