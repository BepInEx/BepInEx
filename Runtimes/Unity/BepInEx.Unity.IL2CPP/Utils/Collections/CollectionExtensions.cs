using Il2CppSystem.Collections;

namespace BepInEx.Unity.IL2CPP.Utils.Collections;

public static class CollectionExtensions
{
    public static IEnumerator WrapToIl2Cpp(this System.Collections.IEnumerator self) =>
        new Il2CppManagedEnumerator(self).Cast<IEnumerator>();

    public static System.Collections.IEnumerator WrapToManaged(this IEnumerator self) =>
        new ManagedIl2CppEnumerator(self);

    public static IEnumerable WrapToIl2Cpp(this System.Collections.IEnumerable self) =>
        new Il2CppManagedEnumerable(self).Cast<IEnumerable>();

    public static System.Collections.IEnumerable WrapToManaged(this IEnumerable self) =>
        new ManagedIl2CppEnumerable(self);
}
