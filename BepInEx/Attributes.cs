using System;

namespace BepInEx
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class BepInPlugin : Attribute
    {
        /// <summary>
        /// The unique identifier of the plugin. Should not change between plugin versions.
        /// </summary>
        public string GUID { get; protected set; }

        
        /// <summary>
        /// The user friendly name of the plugin. Is able to be changed between versions.
        /// </summary>
        public string Name { get; protected set; }

        
        /// <summary>
        /// The specfic version of the plugin.
        /// </summary>
        public string Version { get; protected set; }

        public BepInPlugin(string GUID, string Name, string Version)
        {
            this.GUID = GUID;
            this.Name = Name;
            this.Version = Version;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class BepInDependency : Attribute
    {
        public enum DependencyFlags
        {
            HardDependency = 1,
            SoftDependency = 2
        }

        public string refGUID { get; protected set; }

        public DependencyFlags Flags { get; protected set; }

        public BepInDependency(string refGUID, DependencyFlags Flags = DependencyFlags.HardDependency)
        {
            this.refGUID = refGUID;
            this.Flags = Flags;
        }
    }
}
