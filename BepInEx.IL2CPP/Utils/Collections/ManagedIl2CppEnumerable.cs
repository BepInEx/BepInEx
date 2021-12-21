using System;
using System.Collections;

namespace BepInEx.IL2CPP.Utils.Collections;

public class ManagedIl2CppEnumerable : IEnumerable
{
    private readonly Il2CppSystem.Collections.IEnumerable enumerable;

    public ManagedIl2CppEnumerable(Il2CppSystem.Collections.IEnumerable enumerable)
    {
        this.enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
    }

    public IEnumerator GetEnumerator() => new ManagedIl2CppEnumerator(enumerable.GetEnumerator());
}
