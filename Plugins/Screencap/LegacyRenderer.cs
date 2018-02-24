using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Screencap
{
    public static class LegacyRenderer
    {
        public static byte[] RenderCamera(int ResolutionX, int ResolutionY, int DownscalingRate, int AntiAliasing)
        {
            var go = new GameObject();
            Camera renderCam = go.AddComponent<Camera>();
            renderCam.CopyFrom(Camera.main);
            CopyComponents(Camera.main.gameObject, renderCam.gameObject);

            renderCam.targetTexture = new RenderTexture(ResolutionX * DownscalingRate, ResolutionY * DownscalingRate, 32); //((int)cam.pixelRect.width, (int)cam.pixelRect.height, 32);
            renderCam.aspect = renderCam.targetTexture.width / (float)renderCam.targetTexture.height;
            renderCam.targetTexture.antiAliasing = AntiAliasing;

            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = renderCam.targetTexture;

            renderCam.clearFlags = CameraClearFlags.Skybox;

            renderCam.Render();
            Texture2D image = new Texture2D(ResolutionX * DownscalingRate, ResolutionY * DownscalingRate);
            image.ReadPixels(new Rect(0, 0, ResolutionX * DownscalingRate, ResolutionY * DownscalingRate), 0, 0);

            TextureScale.Bilinear(image, ResolutionX, ResolutionY);

            image.Apply();
            RenderTexture.active = currentRT;
            GameObject.Destroy(renderCam.targetTexture);
            GameObject.Destroy(renderCam);

            byte[] result = image.EncodeToPNG();

            GameObject.Destroy(image);

            return result;
        }

        static void CopyComponents(GameObject original, GameObject target)
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
        static Component CopyComponent(Component original, GameObject destination)
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
