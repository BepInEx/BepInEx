using BepInEx;
using BepInEx.Common;
using System;
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

        private string screenshotDir = Utility.CombinePaths(Utility.ExecutingDirectory, "UserData", "cap");

        public ScreenshotManager()
        {
            if (!Directory.Exists(screenshotDir))
                Directory.CreateDirectory(screenshotDir);
        }

        void LateUpdate()
        {
            if (UnityEngine.Event.current.Equals(Event.KeyboardEvent("f9")))
            {
                string filename = Path.Combine(screenshotDir, $"Koikatsu -{DateTime.Now.ToString("yyyy-mm-dd-HH-mm-ss")}.png");
                TakeScreenshot(filename);
            }
            else if (UnityEngine.Event.current.Equals(Event.KeyboardEvent("f11")))
            {
                string filename = Path.Combine(screenshotDir, $"Koikatsu Char-{DateTime.Now.ToString("yyyy-mm-dd-HH-mm-ss")}.png");
                TakeCharScreenshot(filename);
            }
        }

        void TakeScreenshot(string filename)
        {
            UnityEngine.Application.CaptureScreenshot(filename);
            Console.WriteLine($"Screenshot saved to {filename}");
        }

        void TakeCharScreenshot(string filename)
        {
            Camera.main.backgroundColor = Color.clear;
            var tex = RenderCamera(Camera.main);
            File.WriteAllBytes(filename, tex.EncodeToPNG());
            Destroy(tex);
            Console.WriteLine($"Character screenshot saved to {filename}");
        }

        Texture2D RenderCamera(Camera cam)
        {
            var go = new GameObject();
            Camera renderCam = go.AddComponent<Camera>();
            renderCam.CopyFrom(Camera.main);

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
    }
}
