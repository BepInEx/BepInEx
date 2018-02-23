using BepInEx;
using BepInEx.Common;
using Illusion.Game;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Screencap
{
    public class ScreenshotManager : BaseUnityPlugin
    {
        public override string Name => "Screenshot Manager";
        Event ScreenKeyEvent;
        Event CharacterKeyEvent;

        private string screenshotDir = Utility.CombinePaths(Utility.ExecutingDirectory, "UserData", "cap");

        public ScreenshotManager()
        {
            ScreenKeyEvent = Event.KeyboardEvent("f9");
            CharacterKeyEvent = Event.KeyboardEvent("f11");
            if (!Directory.Exists(screenshotDir))
                Directory.CreateDirectory(screenshotDir);
        }

        void LateUpdate()
        {
            if (UnityEngine.Event.current.Equals(ScreenKeyEvent))
            {
                string filename = Path.Combine(screenshotDir, $"Koikatsu-{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}.png");
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
            var tex = RenderCamera(Camera.main);
            File.WriteAllBytes(filename, tex.EncodeToPNG());
            Destroy(tex);
            Illusion.Game.Utils.Sound.Play(SystemSE.photo);
            BepInLogger.Log($"Character screenshot saved to {filename}", true);
        }

        Texture2D RenderCamera(Camera cam)
        {
            var go = new GameObject();
            Camera renderCam = go.AddComponent<Camera>();
            renderCam.CopyFrom(Camera.main);
            CopyComponents(Camera.main.gameObject, renderCam.gameObject);

            renderCam.targetTexture = new RenderTexture(2048, 2048, 32); //((int)cam.pixelRect.width, (int)cam.pixelRect.height, 32);
            renderCam.aspect = renderCam.targetTexture.width / (float)renderCam.targetTexture.height;
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = renderCam.targetTexture;

            renderCam.Render();
            Texture2D image = new Texture2D(renderCam.targetTexture.width, renderCam.targetTexture.height);
            image.ReadPixels(new Rect(0, 0, renderCam.targetTexture.width, renderCam.targetTexture.height), 0, 0);
            image.Apply();
            RenderTexture.active = currentRT;
            Destroy(renderCam.targetTexture);
            Destroy(renderCam);
            return image;
        }

        void CopyComponents(GameObject original, GameObject target)
        {
            foreach (Component component in original.GetComponents<Component>())
            {
                var newComponent = CopyComponent(component, target);

                if (component is MonoBehaviour)
                {
                    var behavior = (MonoBehaviour)component;

                    (newComponent as MonoBehaviour).enabled = behavior.enabled;
                }
            }
        }

        //https://answers.unity.com/questions/458207/copy-a-component-at-runtime.html
        Component CopyComponent(Component original, GameObject destination)
        {
            System.Type type = original.GetType();
            Component copy = destination.AddComponent(type);
            // Copied fields can be restricted with BindingFlags
            System.Reflection.FieldInfo[] fields = type.GetFields();
            foreach (System.Reflection.FieldInfo field in fields)
            {
                field.SetValue(copy, field.GetValue(original));
            }
            return copy;
        }
    }
}
