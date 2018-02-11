using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.SceneManagement;

namespace BepInEx
{
    public interface IUnityPlugin
    {
        void OnStart();

        void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode);


        void OnUpdate();

        void OnLateUpdate();

        void OnFixedUpdate();
    }
}
