using BepInEx;
using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace InputUnlocker
{
    class InputUnlocker : BaseUnityPlugin
    {
        public override string ID => "inputunlocker";
        public override string Name => "Input Length Unlocker";
        public override Version Version => new Version("1.0");

        void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            foreach (UnityEngine.UI.InputField gameObject in GameObject.FindObjectsOfType<UnityEngine.UI.InputField>())
            {
                gameObject.characterLimit = 999;
            }
        }

        #region MonoBehaviour
        void OnEnable()
        {
            SceneManager.sceneLoaded += LevelFinishedLoading;
        }

        void OnDisable()
        {
            SceneManager.sceneLoaded -= LevelFinishedLoading;
        }
        #endregion
    }
}
