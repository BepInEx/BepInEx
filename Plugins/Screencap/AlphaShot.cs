using System.Collections.Generic;
using UnityEngine;

namespace alphaShot
{
    //code from essu and Kenzato
    public static class AlphaShot
    {
        public static byte[] Capture(int ResolutionX, int ResolutionY, int DownscalingRate, int AntiAliasing)
        {
            //File.WriteAllLines(GameObject.FindObjectsOfType<SkinnedMeshRenderer>().Select(x => x.material.shader.name).Distinct().OrderBy(x => x).ToArray();
            var c = Camera.main;

            var t1 = PerformCapture(true, ResolutionX * DownscalingRate, ResolutionY * DownscalingRate, AntiAliasing);
            var t2 = PerformCapture(false, ResolutionX * DownscalingRate, ResolutionY * DownscalingRate, AntiAliasing);


            var p1 = t1.GetPixels();
            var p2 = t2.GetPixels();

            for (int i = 0; i < p1.Length; i++)
            {
                var pp1 = p1[i];
                var pp2 = p2[i];

                var pp1a = pp1.a;
                var pp2a = pp2.a;

                pp1.a = 1.0f;
                pp2.a = 1.0f;
                p1[i] = Color.Lerp(pp1, pp2, pp2a);
                p1[i].a = Mathf.Clamp01(pp2a * 4);
            }
            t1.SetPixels(p1);
            t1.Apply();

            TextureScale.Bilinear(t1, ResolutionX, ResolutionY);

            byte[] output = t1.EncodeToPNG();

            GameObject.Destroy(t1);
            GameObject.Destroy(t2);

            return output;
        }

        public static Texture2D PerformCapture(bool captureAlphaOnly, int ResolutionX, int ResolutionY, int AntiAliasing)
        {
            var go = new GameObject();
            Camera RenderCam = go.AddComponent<Camera>();
            RenderCam.CopyFrom(Camera.main);
            CopyComponents(Camera.main.gameObject, RenderCam.gameObject);
            
            var backRenderTexture = RenderCam.targetTexture;
            var backRect = RenderCam.rect;

            var oldBackground = RenderCam.backgroundColor;
            var oldFlags = RenderCam.clearFlags;

            var width = ResolutionX;
            var height = ResolutionY;
            var tex = new Texture2D(width, height, TextureFormat.ARGB32, false);

            var rt = RenderTexture.GetTemporary(width, height, 32, RenderTextureFormat.Default, RenderTextureReadWrite.Default, AntiAliasing);

            var lst = new List<SkinnedMeshRenderer>();
            if (captureAlphaOnly)
            {
                Graphics.SetRenderTarget(rt);
                GL.Clear(true, true, Color.clear);
                Graphics.SetRenderTarget(null);
            }
            else
            {
                foreach (var smr in GameObject.FindObjectsOfType<SkinnedMeshRenderer>())
                    if (smr.enabled)
                    {
                        //if (smr.material.renderQueue > 2500 || smr.material.renderQueue == -1 && smr.material.shader.renderQueue > 2500)
                        if (smr.material.shader.name == "Shader Forge/PBRsp_alpha" ||
                            smr.material.shader.name == "Shader Forge/PBRsp_alpha_culloff" ||
                            smr.material.shader.name == "Shader Forge/PBRsp_texture_alpha" ||
                            smr.material.shader.name == "Shader Forge/PBRsp_texture_alpha_culloff" ||
                            smr.material.shader.name == "Unlit/Transparent")
                        {
                            lst.Add(smr);
                            smr.enabled = false;
                        }
                    }
            }

            RenderCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            RenderCam.clearFlags = captureAlphaOnly ? CameraClearFlags.Depth : CameraClearFlags.Color;
            RenderCam.targetTexture = rt;
            RenderCam.rect = new Rect(0, 0, 1f, 1f);
            RenderCam.Render();
            RenderCam.targetTexture = backRenderTexture;
            RenderCam.rect = backRect;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            RenderCam.backgroundColor = oldBackground;
            RenderCam.clearFlags = oldFlags;

            RenderTexture.ReleaseTemporary(rt);

            foreach (var smr in lst)
                smr.enabled = true;
            return tex;
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