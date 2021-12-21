using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Il2CppSystem;
using UnhollowerBaseLib;
using UnhollowerRuntimeLib;
using ArgumentNullException = System.ArgumentNullException;
using Il2CppIEnumerator = Il2CppSystem.Collections.IEnumerator;
using IntPtr = System.IntPtr;
using NotSupportedException = System.NotSupportedException;
using Type = System.Type;

namespace BepInEx.IL2CPP.Utils.Collections;

public class Il2CppManagedEnumerator : Object
{
    private static readonly Dictionary<Type, System.Func<object, Object>> boxers = new();

    private readonly IEnumerator enumerator;

    static Il2CppManagedEnumerator()
    {
        ClassInjector.RegisterTypeInIl2Cpp<Il2CppManagedEnumerator>(new RegisterTypeOptions
        {
            Interfaces = new[] { typeof(Il2CppIEnumerator) }
        });
    }

    public Il2CppManagedEnumerator(IntPtr ptr) : base(ptr) { }

    public Il2CppManagedEnumerator(IEnumerator enumerator)
        : base(ClassInjector.DerivedConstructorPointer<Il2CppManagedEnumerator>())
    {
        this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        ClassInjector.DerivedConstructorBody(this);
    }

    public Object Current => enumerator.Current switch
    {
        Il2CppIEnumerator i => i.Cast<Object>(),
        IEnumerator e       => new Il2CppManagedEnumerator(e),
        Object oo           => oo,
        { } obj             => ManagedToIl2CppObject(obj),
        null                => null
    };

    public bool MoveNext() => enumerator.MoveNext();

    public void Reset() => enumerator.Reset();

    private static System.Func<object, Object> GetValueBoxer(Type t)
    {
        if (boxers.TryGetValue(t, out var conv))
            return conv;

        var dm = new DynamicMethod($"Il2CppUnbox_{t.FullDescription()}", typeof(Object),
                                   new[] { typeof(object) });
        var il = dm.GetILGenerator();
        var loc = il.DeclareLocal(t);
        var classField = typeof(Il2CppClassPointerStore<>).MakeGenericType(t)
                                                          .GetField(nameof(Il2CppClassPointerStore<int>
                                                                               .NativeClassPtr));
        il.Emit(OpCodes.Ldsfld, classField);
        il.Emit(OpCodes.Ldarg_0);
        il.Emit(OpCodes.Unbox_Any, t);
        il.Emit(OpCodes.Stloc, loc);
        il.Emit(OpCodes.Ldloca, loc);
        il.Emit(OpCodes.Call,
                typeof(UnhollowerBaseLib.IL2CPP).GetMethod(nameof(UnhollowerBaseLib.IL2CPP.il2cpp_value_box)));
        il.Emit(OpCodes.Newobj, typeof(Object).GetConstructor(new[] { typeof(IntPtr) }));
        il.Emit(OpCodes.Ret);

        var converter = dm.CreateDelegate(typeof(System.Func<object, Object>)) as System.Func<object, Object>;
        boxers[t] = converter;
        return converter;
    }

    private static Object ManagedToIl2CppObject(object obj)
    {
        var t = obj.GetType();
        if (obj is string s)
            return new Object(UnhollowerBaseLib.IL2CPP.ManagedStringToIl2Cpp(s));
        if (t.IsPrimitive)
            return GetValueBoxer(t)(obj);
        throw new NotSupportedException($"Type {t} cannot be converted directly to an Il2Cpp object");
    }
}
