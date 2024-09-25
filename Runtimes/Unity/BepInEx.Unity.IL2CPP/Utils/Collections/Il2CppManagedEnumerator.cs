﻿using System.Collections;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppSystem;
using ArgumentNullException = System.ArgumentNullException;
using Il2CppIEnumerator = Il2CppSystem.Collections.IEnumerator;
using IntPtr = System.IntPtr;
using NotSupportedException = System.NotSupportedException;
using Type = System.Type;

namespace BepInEx.Unity.IL2CPP.Utils.Collections;

/// <summary>
///     An IL2CPP enumerator that wraps a managed one 
/// </summary>
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

    /// <summary>
    ///     Creates an <see cref="Il2CppManagedEnumerator"/> using a <see cref="IntPtr"/>
    /// </summary>
    /// <param name="ptr">The pointer of the object</param>
    public Il2CppManagedEnumerator(IntPtr ptr) : base(ptr) { }

    /// <summary>
    ///     Creates an <see cref="Il2CppManagedEnumerator"/> from an <see cref="IEnumerator"/>
    /// </summary>
    /// <param name="enumerator">The wrapped enumerator</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="enumerator"/> is null</exception>
    public Il2CppManagedEnumerator(IEnumerator enumerator)
        : base(ClassInjector.DerivedConstructorPointer<Il2CppManagedEnumerator>())
    {
        this.enumerator = enumerator ?? throw new ArgumentNullException(nameof(enumerator));
        ClassInjector.DerivedConstructorBody(this);
    }

    /// <inheritdoc cref="IEnumerator.Current"/>
    public Object Current => enumerator.Current switch
    {
        Il2CppIEnumerator i => i.Cast<Object>(),
        IEnumerator e       => new Il2CppManagedEnumerator(e),
        Object oo           => oo,
        { } obj             => ManagedToIl2CppObject(obj),
        null                => null
    };

    /// <inheritdoc cref="IEnumerator.MoveNext"/>
    public bool MoveNext() => enumerator.MoveNext();

    /// <inheritdoc cref="IEnumerator.Reset"/>
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
                typeof(Il2CppInterop.Runtime.IL2CPP).GetMethod(nameof(Il2CppInterop.Runtime.IL2CPP.il2cpp_value_box)));
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
            return new Object(Il2CppInterop.Runtime.IL2CPP.ManagedStringToIl2Cpp(s));
        if (t.IsPrimitive)
            return GetValueBoxer(t)(obj);
        throw new NotSupportedException($"Type {t} cannot be converted directly to an Il2Cpp object");
    }
}
