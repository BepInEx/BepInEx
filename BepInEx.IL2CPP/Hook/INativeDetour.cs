using MonoMod.RuntimeDetour;
using System;
using System.Collections.Generic;
using System.Text;

namespace BepInEx.IL2CPP.Hook;

public interface INativeDetour : IDetour
{
    public nint OriginalMethodPtr { get; }
    public nint DetourMethodPtr { get; }
    public nint TrampolinePtr { get; }
}
