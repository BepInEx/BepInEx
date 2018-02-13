using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BepInEx
{
    public abstract class BaseUnityPlugin : MonoBehaviour
    {
        public abstract string Name { get; }

        protected virtual void OnEnable()
        {
            SceneManager.sceneLoaded += LevelFinishedLoading;
        }

        protected virtual void OnDisable()
        {
            SceneManager.sceneLoaded -= LevelFinishedLoading;
        }

        protected virtual void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {

        }
    }
}
