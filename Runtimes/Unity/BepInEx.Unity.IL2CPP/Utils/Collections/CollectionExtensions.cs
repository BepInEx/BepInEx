using Il2CppSystem.Collections;

namespace BepInEx.Unity.IL2CPP.Utils.Collections;

/// <summary>
///     Extensions class to work with collections in IL2CPP
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    ///     Get a <see cref="Il2CppManagedEnumerator"/> from am <see cref="System.Collections.IEnumerator"/>
    /// </summary>
    /// <param name="self">The enumerator to wrap to IL2CPP</param>
    /// <returns>The wrapped enumerator</returns>
    public static IEnumerator WrapToIl2Cpp(this System.Collections.IEnumerator self) =>
        new Il2CppManagedEnumerator(self).Cast<IEnumerator>();

    /// <summary>
    ///     Get a <see cref="ManagedIl2CppEnumerator"/> from am <see cref="IEnumerator"/>
    /// </summary>
    /// <param name="self">The IL2CPP enumerator to unwrap</param>
    /// <returns>The unwrapped enumerator</returns>
    public static System.Collections.IEnumerator WrapToManaged(this IEnumerator self) =>
        new ManagedIl2CppEnumerator(self);

    /// <summary>
    ///     Get a <see cref="Il2CppManagedEnumerable"/> from am <see cref="System.Collections.IEnumerable"/>
    /// </summary>
    /// <param name="self">The enumerable to wrap to IL2CPP</param>
    /// <returns>The wrapped enumerable</returns>
    public static IEnumerable WrapToIl2Cpp(this System.Collections.IEnumerable self) =>
        new Il2CppManagedEnumerable(self).Cast<IEnumerable>();

    /// <summary>
    ///     Get a <see cref="ManagedIl2CppEnumerable"/> from am <see cref="IEnumerable"/>
    /// </summary>
    /// <param name="self">The IL2CPP enumerable to unwrap</param>
    /// <returns>The unwrapped enumerable</returns>
    public static System.Collections.IEnumerable WrapToManaged(this IEnumerable self) =>
        new ManagedIl2CppEnumerable(self);
}
