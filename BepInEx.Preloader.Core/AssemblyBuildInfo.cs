using System;
using System.Linq;
using Mono.Cecil;

namespace BepInEx.Preloader.Core
{
    /// <summary>
    ///     Represents the build information of an assembly including runtime information and CPU architecture
    /// </summary>
    public class AssemblyBuildInfo
    {
        /// <summary>
        ///     A .NET type (framework, standard or core)
        /// </summary>
        public enum FrameworkType
        {
            /// <summary>
            ///     The framework type couldn't be determined
            /// </summary>
            Unknown,
            /// <summary>
            ///     .NET Framework
            /// </summary>
            NetFramework,
            /// <summary>
            ///     .NET Standard
            /// </summary>
            NetStandard,
            /// <summary>
            ///     .NET Core
            /// </summary>
            NetCore
        }

        /// <summary>
        ///     The version of the .NET framework the assembly was built for
        /// </summary>
        public Version NetFrameworkVersion { get; private set; }

        /// <summary>
        ///     Whether the assembly was built with AnyCPU
        /// </summary>
        public bool IsAnyCpu { get; private set; }

        /// <summary>
        ///     True if the assembly targets 64 bit, false if it targets 32 bit
        /// </summary>
        public bool Is64Bit { get; private set; }

        /// <summary>
        ///     The type of .NET framework the assembly was built for
        /// </summary>
        public FrameworkType AssemblyFrameworkType { get; private set; }

        private void SetNet4Version(AssemblyDefinition assemblyDefinition)
        {
            NetFrameworkVersion = new Version(0, 0);
            AssemblyFrameworkType = FrameworkType.Unknown;

            var targetFrameworkAttribute = assemblyDefinition.CustomAttributes.FirstOrDefault(x => 
                x.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute");

            if (targetFrameworkAttribute == null)
                return;

            if (targetFrameworkAttribute.ConstructorArguments.Count < 1)
                return;

            if (targetFrameworkAttribute.ConstructorArguments[0].Type.Name != "String")
                return;

            string versionInfo = (string)targetFrameworkAttribute.ConstructorArguments[0].Value;

            var values = versionInfo.Split(',');

            

            foreach (var value in values)
            {
                if (value.StartsWith(".NET"))
                {
                    AssemblyFrameworkType = value switch
                    {
                        ".NETFramework" => FrameworkType.NetFramework,
                        ".NETCoreApp"   => FrameworkType.NetCore,
                        ".NETStandard"  => FrameworkType.NetStandard,
                        _               => FrameworkType.Unknown
                    };
                }
                else if (value.StartsWith("Version=v"))
                {
                    try
                    {
                        NetFrameworkVersion = new Version(value.Substring("Version=v".Length));
                    }
                    catch { }
                }
            }
        }

        /// <summary>
        ///     Get an <see cref="AssemblyBuildInfo"/> from an <see cref="AssemblyDefinition"/>
        /// </summary>
        /// <param name="assemblyDefinition">The assembly definition to get the build information for</param>
        /// <returns>The assembly build information</returns>
        /// <exception cref="Exception">Thrown when unable to determine the assembly archtecture</exception>
        public static AssemblyBuildInfo DetermineInfo(AssemblyDefinition assemblyDefinition)
        {
            var buildInfo = new AssemblyBuildInfo();

            // framework version

            var runtime = assemblyDefinition.MainModule.Runtime;

            if (runtime == TargetRuntime.Net_1_0)
            {
                buildInfo.NetFrameworkVersion = new Version(1, 0);
                buildInfo.AssemblyFrameworkType = FrameworkType.NetFramework;
            }
            else if (runtime == TargetRuntime.Net_1_1)
            {
                buildInfo.NetFrameworkVersion = new Version(1, 1);
                buildInfo.AssemblyFrameworkType = FrameworkType.NetFramework;
            }
            else if (runtime == TargetRuntime.Net_2_0)
            {
                // Assume 3.5 here. The code to determine versions between 2.0 and 3.5 is not worth the amount of non-unity games that use it, if any

                buildInfo.NetFrameworkVersion = new Version(3, 5);
                buildInfo.AssemblyFrameworkType = FrameworkType.NetFramework;
            }
            else
            {
                buildInfo.SetNet4Version(assemblyDefinition);
            }

            // bitness

            /*
                AnyCPU 64-bit preferred
                MainModule.Architecture: I386
                MainModule.Attributes: ILOnly

                AnyCPU 32-bit preferred
                MainModule.Architecture: I386
                MainModule.Attributes: ILOnly, Required32Bit, Preferred32Bit

                x86
                MainModule.Architecture: I386
                MainModule.Attributes: ILOnly, Required32Bit

                x64
                MainModule.Architecture: AMD64
                MainModule.Attributes: ILOnly
            */

            var architecture = assemblyDefinition.MainModule.Architecture;
            var attributes = assemblyDefinition.MainModule.Attributes;

            if (architecture == TargetArchitecture.AMD64)
            {
                buildInfo.Is64Bit = true;
                buildInfo.IsAnyCpu = false;
            }
            else if (architecture == TargetArchitecture.I386 && HasFlag(attributes, ModuleAttributes.Preferred32Bit | ModuleAttributes.Required32Bit))
            {
                buildInfo.Is64Bit = false;
                buildInfo.IsAnyCpu = true;
            }
            else if (architecture == TargetArchitecture.I386 && HasFlag(attributes, ModuleAttributes.Required32Bit))
            {
                buildInfo.Is64Bit = false;
                buildInfo.IsAnyCpu = false;
            }
            else if (architecture == TargetArchitecture.I386)
            {
                buildInfo.Is64Bit = true;
                buildInfo.IsAnyCpu = true;
            }
            else
            {
                throw new Exception("Unable to determine assembly architecture");
            }

            return buildInfo;
        }

        private static bool HasFlag(ModuleAttributes value, ModuleAttributes flag)
        {
            return (value & flag) == flag;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            string frameworkType = AssemblyFrameworkType switch {
                FrameworkType.NetFramework => "Framework",
                FrameworkType.NetStandard  => "Standard",
                FrameworkType.NetCore      => "Core",
                FrameworkType.Unknown      => "Unknown",
                _                          => throw new ArgumentOutOfRangeException()
            };

            if (IsAnyCpu)
            {
                return $".NET {frameworkType} {NetFrameworkVersion}, AnyCPU ({(Is64Bit ? "64" : "32")}-bit preferred)";
            }

            return $".NET {frameworkType} {NetFrameworkVersion}, {(Is64Bit ? "x64" : "x86")}";
        }
    }
}
