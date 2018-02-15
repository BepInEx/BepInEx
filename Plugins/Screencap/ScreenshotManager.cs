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
            float oldaspect = cam.aspect;
            cam.targetTexture = new RenderTexture(1024, 1024, 32); //((int)cam.pixelRect.width, (int)cam.pixelRect.height, 32);
            cam.aspect = cam.targetTexture.width / (float)cam.targetTexture.height;
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = cam.targetTexture;
            
            cam.Render();
            Texture2D image = new Texture2D(cam.targetTexture.width, cam.targetTexture.height);
            image.ReadPixels(new Rect(0, 0, cam.targetTexture.width, cam.targetTexture.height), 0, 0);
            image.Apply();
            RenderTexture.active = currentRT;
            Destroy(cam.targetTexture);
            cam.targetTexture = null;
            cam.aspect = oldaspect;
            return image;
        }
    }
}
