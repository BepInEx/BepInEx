using BepInEx.Configuration;
using BepInEx.Unity.IL2CPP.Hook.Dobby;
using BepInEx.Unity.IL2CPP.Hook.Funchook;
using MonoMod.Core;

namespace BepInEx.Unity.IL2CPP.Hook;

internal class Il2CppDetourFactory : IDetourFactory
{
    internal enum DetourProvider
    {
        Default,
        Dobby,
        Funchook
    }
    
    private static readonly ConfigEntry<DetourProvider> DetourProviderType = ConfigFile.CoreConfig.Bind(
         "Detours", "DetourProviderType",
         DetourProvider.Default,
         "The native provider to use for managed detours"
        );

    ICoreDetour IDetourFactory.CreateDetour(CreateDetourRequest request)
    {
        // TODO: Do the proper detouring implementation whenever MonoMod adds supports for having the
        // target call the OrigEntrypoint instead of the detoured original
        return DetourFactory.Current.CreateDetour(request);
    }

    ICoreNativeDetour IDetourFactory.CreateNativeDetour(CreateNativeDetourRequest request)
    {
        var detour = DetourProviderType.Value switch
        {
            DetourProvider.Dobby    => new DobbyDetour(request.Source, request.Target),
            DetourProvider.Funchook => new FunchookDetour(request.Source, request.Target),
            _                       => CreateDefault(request.Source, request.Target)
        };

        return detour;
    }

    private static ICoreNativeDetour CreateDefault(nint original, nint target) =>
        // TODO: check and provide an OS accurate provider
        new DobbyDetour(original, target);
}
