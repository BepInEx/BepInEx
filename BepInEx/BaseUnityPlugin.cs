using System;
using UnityEngine;

namespace BepInEx
{
    /// <summary>
    /// The base plugin type, that is loaded into the game.
    /// </summary>
    public abstract class BaseUnityPlugin : MonoBehaviour
    {
        /// <summary>
        /// The unique identifier of the plugin. Should not change between plugin versions.
        /// </summary>
        public abstract string ID { get; }

        /// <summary>
        /// The user friendly name of the plugin. Is able to be changed between versions.
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// The specfic version of the plugin.
        /// </summary>
        public abstract Version Version { get; }
    }
}
