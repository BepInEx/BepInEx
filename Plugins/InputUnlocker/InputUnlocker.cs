using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InputUnlocker
{
    class InputUnlocker : BaseUnityPlugin
    {
        public override string Name => "Input Length Unlocker";

        protected override void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            foreach (UnityEngine.UI.InputField gameObject in GameObject.FindObjectsOfType<UnityEngine.UI.InputField>())
            {
                gameObject.characterLimit = 99;
            }
        }
    }
}
