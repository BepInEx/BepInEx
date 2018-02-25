using BepInEx;
using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace InputUnlocker
{
    class InputUnlocker : BaseUnityPlugin
    {
        public override string ID => "com.bepis.bepinex.inputunlocker";
        public override string Name => "Input Length Unlocker";
        public override Version Version => new Version("1.0");

        void Awake()
        {
            foreach (InputField gameObject in GameObject.FindObjectsOfType<InputField>())
            {
                UnlockInput(gameObject);
            }
        }

        void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            foreach (GameObject obj in scene.GetRootGameObjects())
                foreach (InputField gameObject in obj.GetComponentsInChildren<InputField>(true))
                {
                    UnlockInput(gameObject);
                }
        }

        void UnlockInput(InputField input)
        {
            input.characterLimit = 999;
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
