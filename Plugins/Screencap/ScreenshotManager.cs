using alphaShot;
using BepInEx;
using BepInEx.Common;
using Illusion.Game;
using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Screencap
{
    public class ScreenshotManager : BaseUnityPlugin
    {
        public override string ID => "com.bepis.bepinex.screenshotmanager";
        public override string Name => "Screenshot Manager";
        public override Version Version => new Version("2.0");

        Event ScreenKeyEvent = Event.KeyboardEvent("f9");
        Event CharacterKeyEvent = Event.KeyboardEvent("f11");
        Event CharacterSettingsKeyEvent = Event.KeyboardEvent("#f11");



        private string screenshotDir = Utility.CombinePaths(Utility.ExecutingDirectory, "UserData", "cap");

        #region Config properties

        private int ResolutionX
        {
            get => int.Parse(BepInEx.Config.GetEntry("screenshotrenderer-resolution-x", "1024"));
            set => BepInEx.Config.SetEntry("screenshotrenderer-resolution-x", value.ToString());
        }

        private int ResolutionY
        {
            get => int.Parse(BepInEx.Config.GetEntry("screenshotrenderer-resolution-y", "1024"));
            set => BepInEx.Config.SetEntry("screenshotrenderer-resolution-y", value.ToString());
        }
        
        private int AntiAliasing
        {
            get => int.Parse(BepInEx.Config.GetEntry("screenshotrenderer-antialiasing", "4"));
            set => BepInEx.Config.SetEntry("screenshotrenderer-antialiasing", value.ToString());
        }

        private int DownscalingRate
        {
            get => int.Parse(BepInEx.Config.GetEntry("screenshotrenderer-downscalerate", "1"));
            set => BepInEx.Config.SetEntry("screenshotrenderer-downscalerate", value.ToString());
        }

        private int RenderMethod
        {
            get => int.Parse(BepInEx.Config.GetEntry("screenshotrenderer-rendermethod", "1"));
            set => BepInEx.Config.SetEntry("screenshotrenderer-rendermethod", value.ToString());
        }

        #endregion


        void Awake()
        {
            if (!Directory.Exists(screenshotDir))
                Directory.CreateDirectory(screenshotDir);
        }

        void Update()
        {
            if (UnityEngine.Event.current.Equals(CharacterSettingsKeyEvent))
            {
                showingUI = !showingUI;
            }
            else if (UnityEngine.Event.current.Equals(ScreenKeyEvent))
            {
                string filename = Path.Combine(screenshotDir, $"Koikatsu -{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}.png");
                StartCoroutine(TakeScreenshot(filename));
            }
            else if (UnityEngine.Event.current.Equals(CharacterKeyEvent))
            {
                string filename = Path.Combine(screenshotDir, $"Koikatsu Char-{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}.png");
                TakeCharScreenshot(filename);
            }
        }


        IEnumerator TakeScreenshot(string filename)
        {
            Application.CaptureScreenshot(filename);
            Illusion.Game.Utils.Sound.Play(SystemSE.photo);

            while (!File.Exists(filename))
                yield return new WaitForSeconds(0.01f);

            BepInLogger.Log($"Screenshot saved to {filename}", true);
        }

        void TakeCharScreenshot(string filename)
        {
            Camera.main.backgroundColor = Color.clear;

            switch (RenderMethod)
            {
                case 0: //legacy
                default:
                    File.WriteAllBytes(filename, LegacyRenderer.RenderCamera(ResolutionX, ResolutionY, DownscalingRate, AntiAliasing));
                    break;
                //case 1: //alphashot
                //    File.WriteAllBytes(filename, AlphaShot.Capture(ResolutionX, ResolutionY, DownscalingRate, AntiAliasing));
                //    break;
            }

            Illusion.Game.Utils.Sound.Play(SystemSE.photo);
            BepInLogger.Log($"Character screenshot saved to {filename}", true);
        }


        #region UI
        private Rect UI = new Rect(20, 20, 160, 250);
        private bool showingUI = false;

        void OnGUI()
        {
            if (showingUI)
                UI = GUI.Window(Name.GetHashCode() + 0, UI, WindowFunction, "Rendering settings");
        }

        void WindowFunction(int windowID)
        {
            GUI.Label(new Rect(0, 20, 160, 20), "Output resolution", new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                normal = new GUIStyleState
                {
                    textColor = Color.white
                }
            });

            GUI.Label(new Rect(0, 40, 160, 20), "x", new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                normal = new GUIStyleState
                {
                    textColor = Color.white
                }
            });

            string resX = GUI.TextField(new Rect(10, 40, 60, 20), ResolutionX.ToString());

            string resY = GUI.TextField(new Rect(90, 40, 60, 20), ResolutionY.ToString());

            bool screenSize = GUI.Button(new Rect(10, 65, 140, 20), "Set to screen size");


            GUI.Label(new Rect(0, 90, 160, 20), "Downscaling rate", new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                normal = new GUIStyleState
                {
                    textColor = Color.white
                }
            });


            int downscale = (int)Math.Round(GUI.HorizontalSlider(new Rect(10, 113, 120, 20), DownscalingRate, 1, 4));

            GUI.Label(new Rect(0, 110, 150, 20), $"{downscale}x", new GUIStyle
            {
                alignment = TextAnchor.UpperRight,
                normal = new GUIStyleState
                {
                    textColor = Color.white
                }
            });


            GUI.Label(new Rect(0, 130, 160, 20), "Antialiasing", new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                normal = new GUIStyleState
                {
                    textColor = Color.white
                }
            });


            int antia = (int)Math.Round(GUI.HorizontalSlider(new Rect(10, 153, 120, 20), AntiAliasing, 1, 16));

            GUI.Label(new Rect(0, 150, 154, 20), $"{antia}x", new GUIStyle
            {
                alignment = TextAnchor.UpperRight,
                normal = new GUIStyleState
                {
                    textColor = Color.white
                }
            });


            GUI.Label(new Rect(0, 170, 160, 20), "Render method", new GUIStyle
            {
                alignment = TextAnchor.MiddleCenter,
                normal = new GUIStyleState
                {
                    textColor = Color.white
                }
            });

            int renderMethod = GUI.SelectionGrid(new Rect(10, 190, 140, 50), RenderMethod, new[] { "Legacy", "AlphaShot" }, 1);



            if (GUI.changed)
            {
                BepInEx.Config.SaveOnConfigSet = false;

                if (int.TryParse(resX, out int x))
                    ResolutionX = Mathf.Clamp(x, 2, 4096);

                if (int.TryParse(resY, out int y))
                    ResolutionY = Mathf.Clamp(y, 2, 4096);

                if (screenSize)
                {
                    ResolutionX = Screen.width;
                    ResolutionY = Screen.height;
                }

                DownscalingRate = downscale;
                AntiAliasing = antia;
                RenderMethod = renderMethod;

                BepInEx.Config.SaveOnConfigSet = true;
                BepInEx.Config.SaveConfig();
            }

            GUI.DragWindow();
        }
        #endregion
    }
}
