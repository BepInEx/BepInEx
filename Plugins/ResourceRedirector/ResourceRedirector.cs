using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine.SceneManagement;

namespace ResourceRedirector
{
    public class ResourceRedirector : BaseUnityPlugin
    {
        public override string Name => "Resource Redirector";

        protected override void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            
        }


    }
}
