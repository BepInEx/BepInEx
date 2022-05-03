using MonoMod.RuntimeDetour;

namespace BepInEx.IL2CPP.Hook;

public interface INativeDetour : IDetour
{
    public nint OriginalMethodPtr { get; }
    public nint DetourMethodPtr { get; }
    public nint TrampolinePtr { get; }
}
