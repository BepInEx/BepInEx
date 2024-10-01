using System;
using System.Collections;

namespace BepInEx.Unity.IL2CPP.Utils.Collections;

/// <summary>
///     A wrapped <see cref="IEnumerable"/> to the IL2CPP system
/// </summary>
public class ManagedIl2CppEnumerable : IEnumerable
{
    private readonly Il2CppSystem.Collections.IEnumerable enumerable;

    /// <summary>
    ///     Creates a <see cref="ManagedIl2CppEnumerable"/> from an <see cref="Il2CppSystem.Collections.IEnumerable"/>
    /// </summary>
    /// <param name="enumerable">The enumerable to wrap</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="enumerable"/> is null</exception>
    public ManagedIl2CppEnumerable(Il2CppSystem.Collections.IEnumerable enumerable)
    {
        this.enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
    }

    /// <inheritdoc />
    public IEnumerator GetEnumerator() => new ManagedIl2CppEnumerator(enumerable.GetEnumerator());
}
