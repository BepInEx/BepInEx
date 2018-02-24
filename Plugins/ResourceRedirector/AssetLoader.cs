using BepInEx.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ResourceRedirector
{
    public static class AssetLoader
    {
        public static AudioClip LoadAudioClip(string path, AudioType type)
        {
            using (WWW loadGachi = new WWW(Utility.ConvertToWWWFormat(path)))
            {
                AudioClip clip = loadGachi.GetAudioClipCompressed(false, type);

                //force single threaded loading instead of using a coroutine
                while (!clip.isReadyToPlay) { }

                return clip;
            }
        }

        public static Texture2D LoadTexture(string path)
        {
            byte[] data = File.ReadAllBytes(path);

            Texture2D tex = new Texture2D(2, 2);

            //DDS method
            //tex.LoadRawTextureData

            tex.LoadImage(data);

            return tex;
        }
    }
}
