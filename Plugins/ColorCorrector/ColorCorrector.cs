using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ColorCorrector
{
    public class ColorCorrector : BaseUnityPlugin
    {
        public override string Name => "Color Corrector";

        protected override void LevelFinishedLoading(Scene scene, LoadSceneMode mode)
        {
            var c = Camera.main.gameObject.GetComponent("AmplifyColorEffect");
            Destroy(c);
        }
    }
}
