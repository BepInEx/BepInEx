using System.Collections;
using BepInEx.Unity.IL2CPP.Utils.Collections;
using UnityEngine;

namespace BepInEx.Unity.IL2CPP.Utils;

public static class MonoBehaviourExtensions
{
    public static Coroutine StartCoroutine(this MonoBehaviour self, IEnumerator coroutine) =>
        self.StartCoroutine(coroutine.WrapToIl2Cpp());
}
