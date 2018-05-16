using System;

namespace BepInEx
{
    /// <summary>
    /// This attribute denotes that a class is a plugin, and specifies the required metadata.
    /// </summary>
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
        public Version Version { get; protected set; }
        
        /// <param name="GUID">The unique identifier of the plugin. Should not change between plugin versions.</param>
        /// <param name="Name">The user friendly name of the plugin. Is able to be changed between versions.</param>
        /// <param name="Version">The specfic version of the plugin.</param>
        public BepInPlugin(string GUID, string Name, string Version)
        {
            this.GUID = GUID;
            this.Name = Name;
            this.Version = new Version(Version);
        }
    }

    /// <summary>
    /// This attribute specifies any dependencies that this plugin has on other plugins.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class BepInDependency : Attribute
    {
        public enum DependencyFlags
        {
            /// <summary>
            /// The plugin has a hard dependency on the referenced plugin, and will not run without it.
            /// </summary>
            HardDependency = 1,

            /// <summary>
            /// This plugin has a soft dependency on the referenced plugin, and is able to run without it.
            /// </summary>
            SoftDependency = 2,
        }

        /// <summary>
        /// The GUID of the referenced plugin.
        /// </summary>
        public string DependencyGUID { get; protected set; }

        /// <summary>
        /// The flags associated with this dependency definition.
        /// </summary>
        public DependencyFlags Flags { get; protected set; }
        
        /// <param name="DependencyGUID">The GUID of the referenced plugin.</param>
        /// <param name="Flags">The flags associated with this dependency definition.</param>
        public BepInDependency(string DependencyGUID, DependencyFlags Flags = DependencyFlags.HardDependency)
        {
            this.DependencyGUID = DependencyGUID;
            this.Flags = Flags;
        }
    }

    /// <summary>
    /// This attribute specifies which processes this plugin should be run for. Not specifying this attribute will load the plugin under every process.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
    public class BepInProcess : Attribute
    {
        /// <summary>
        /// The name of the process that this plugin will run under.
        /// </summary>
        public string ProcessName { get; protected set; }
        
        /// <param name="ProcessName">The name of the process that this plugin will run under.</param>
        public BepInProcess(string ProcessName)
        {
            this.ProcessName = ProcessName;
        }
    }
}
