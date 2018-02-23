using BepInEx;
using System;
using UnityEngine;

namespace DeveloperConsole
{
    public class DeveloperConsole : BaseUnityPlugin
    {
        public override string ID => "developerconsole";
        public override string Name => "Developer Console";
        public override Version Version => new Version("1.0.1");

        private Rect UI = new Rect(20, 20, 400, 200);
        bool showingUI = false;
        string TotalLog = "";

        int showCounter = 0;
        string TotalShowingLog = "";

        public DeveloperConsole()
        {
            BepInLogger.EntryLogged += (log, show) =>
            {
                string current = $"{TotalLog}\r\n{log}";
                if (current.Length > 2000)
                {
                    current = current.Remove(0, 1000);
                }
                TotalLog = current;

                if (show)
                {
                    if (showCounter == 0)
                        TotalShowingLog = "";

                    showCounter = 400;
                    TotalShowingLog = $"{TotalShowingLog}\r\n{log}";
                }
            };
        }



        void Update()
        {
            if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.F12))
            {
                showingUI = !showingUI;
            }
        }

        void OnGUI()
        {
            ShowLog();

            if (showingUI)
                UI = GUI.Window(Name.GetHashCode() + 0, UI, WindowFunction, "Developer Console");
        }

        

        void ShowLog()
        {
            if (showCounter != 0)
            {
                showCounter--;

                GUI.Label(new Rect(40, 0, 600, 160), TotalShowingLog, new GUIStyle
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = 26,
                    normal = new GUIStyleState
                    {
                        textColor = Color.white
                    }
                });
            }
        }

        void WindowFunction(int windowID)
        {
            GUI.Label(new Rect(10, 40, 380, 160), TotalLog, new GUIStyle
            {
                alignment = TextAnchor.LowerLeft,
                wordWrap = true,
                normal = new GUIStyleState
                {
                    textColor = Color.white
                }
            });

            if (GUI.Button(new Rect(295, 20, 100, 20), "Dump scene"))
            {
                SceneDumper.DumpScene();
            }

            GUI.DragWindow();
        }
    }
}
