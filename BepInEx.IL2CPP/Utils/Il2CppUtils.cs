using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace BepInEx.IL2CPP.Utils;

internal static class Il2CppUtils
{
    // TODO: Check if we can safely initialize this in Chainloader instead
    private static GameObject managerGo;

    public static Il2CppObjectBase AddComponent(Type t)
    {
        if (managerGo == null)
            managerGo = new GameObject { hideFlags = HideFlags.HideAndDontSave };

        if (!ClassInjector.IsTypeRegisteredInIl2Cpp(t))
            ClassInjector.RegisterTypeInIl2Cpp(t);

        return managerGo.AddComponent(Il2CppType.From(t));
    }
}
