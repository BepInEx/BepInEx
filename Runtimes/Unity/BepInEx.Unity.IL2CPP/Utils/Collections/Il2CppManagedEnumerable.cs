using System.Collections;
using Il2CppInterop.Runtime.Injection;
using Il2CppSystem;
using ArgumentNullException = System.ArgumentNullException;
using IEnumerator = Il2CppSystem.Collections.IEnumerator;
using IntPtr = System.IntPtr;

namespace BepInEx.Unity.IL2CPP.Utils.Collections;

/// <summary>
///     An IL2CPP enumerable that wraps a managed one 
/// </summary>
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

    /// <summary>
    ///     Creates an <see cref="Il2CppManagedEnumerable"/> using a <see cref="IntPtr"/>
    /// </summary>
    /// <param name="ptr">The pointer of the object</param>
    public Il2CppManagedEnumerable(IntPtr ptr) : base(ptr) { } 

    /// <summary>
    ///     Creates an <see cref="Il2CppManagedEnumerable"/> from an <see cref="IEnumerable"/>
    /// </summary>
    /// <param name="enumerable">The wrapped enumerable</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="enumerable"/> is null</exception>
    public Il2CppManagedEnumerable(IEnumerable enumerable)
        : base(ClassInjector.DerivedConstructorPointer<Il2CppManagedEnumerable>())
    {
        this.enumerable = enumerable ?? throw new ArgumentNullException(nameof(enumerable));
        ClassInjector.DerivedConstructorBody(this);
    }

    /// <inheritdoc cref="IEnumerable.GetEnumerator"/>
    public IEnumerator GetEnumerator() =>
        new Il2CppManagedEnumerator(enumerable.GetEnumerator()).Cast<IEnumerator>();
}
