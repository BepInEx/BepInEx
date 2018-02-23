using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BepInEx
{
    public abstract class BaseUnityPlugin : MonoBehaviour
    {
        public abstract string ID { get; }

        public abstract string Name { get; }

        public abstract Version Version { get; }
    }
}
