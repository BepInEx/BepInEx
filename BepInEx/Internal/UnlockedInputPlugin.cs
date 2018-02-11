using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BepInEx.Internal
{
    class UnlockedInputPlugin : IUnityPlugin
    {
        public void OnStart()
        {

        }

        public void OnLevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            foreach (UnityEngine.UI.InputField gameObject in GameObject.FindObjectsOfType<UnityEngine.UI.InputField>())
            {
                gameObject.characterLimit = 99;
            }
        }

        public void OnFixedUpdate()
        {

        }

        public void OnLateUpdate()
        {

        }

        public void OnUpdate()
        {

        }
    }
}
