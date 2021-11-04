using Il2CppSystem.Collections;

namespace BepInEx.IL2CPP.Utils.Collections
{
    public static class CollectionExtensions
    {
        public static IEnumerator WrapToIl2Cpp(this System.Collections.IEnumerator self)
        {
            return new Il2CppManagedEnumerator(self).Cast<IEnumerator>();
        }

        public static System.Collections.IEnumerator WrapToManaged(this IEnumerator self)
        {
            return new ManagedIl2CppEnumerator(self);
        }

        public static IEnumerable WrapToIl2Cpp(this System.Collections.IEnumerable self)
        {
            return new Il2CppManagedEnumerable(self).Cast<IEnumerable>();
        }

        public static System.Collections.IEnumerable WrapToManaged(this IEnumerable self)
        {
            return new ManagedIl2CppEnumerable(self);
        }
    }
}
