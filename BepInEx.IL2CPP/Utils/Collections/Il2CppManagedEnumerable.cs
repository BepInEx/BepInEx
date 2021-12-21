using System.Collections;
using Il2CppSystem;
using UnhollowerRuntimeLib;
using ArgumentNullException = System.ArgumentNullException;
using IEnumerator = Il2CppSystem.Collections.IEnumerator;
using IntPtr = System.IntPtr;

namespace BepInEx.IL2CPP.Utils.Collections;

public class Il2CppManagedEnumerable : Object
{
    private readonly IEnumerable enumerable;

    static Il2CppManagedEnumerable()
    {
        ClassInjector.RegisterTypeInIl2Cpp<Il2CppManagedEnumerable>(new RegisterTypeOptions
        {
            Interfaces = new[] { typeof(Il2CppSystem.Collections.IEnumerable) }
        });
    }

    public Il2CppManagedEnumerable(IntPtr ptr) : base(ptr) { }

    public Il2CppManagedEnumerable(IEnumerable enumerable)
        : base(ClassInjector.DerivedConstructorPointer<Il2CppManagedEnumerable>())
    {
        this.enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
        ClassInjector.DerivedConstructorBody(this);
    }

    public IEnumerator GetEnumerator() =>
        new Il2CppManagedEnumerator(enumerable.GetEnumerator()).Cast<IEnumerator>();
}
