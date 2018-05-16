using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Common;
using Mono.Cecil;

namespace BepInEx
{
    internal static class AssemblyPatcher
    {
        public delegate void AssemblyLoadEventHandler(AssemblyDefinition assembly);

        public static AssemblyLoadEventHandler AssemblyLoad;


        public static void PatchAll(string directory)
        {
            //load all the requested assemblies
            List<AssemblyDefinition> assemblies = new List<AssemblyDefinition>();

            foreach (string assemblyPath in Directory.GetFiles(directory, "*.dll"))
            {
                var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
                
                //NOTE: this is special cased here because the dependency handling for System.dll is a bit wonky
                //System has an assembly reference to itself, and it also has a reference to Mono.Security causing a circular dependency
                //It's also generally dangerous to change system.dll since so many things rely on it, 
                // and it's already loaded into the appdomain since this loader references it, so we might as well skip it
                if (assembly.Name.Name == "System"
                    || assembly.Name.Name == "System.Core"
                    || assembly.Name.Name == "mscorlib")
                {
                    assembly.Dispose();
                    continue;
                }

                assemblies.Add(assembly);
            }

            //generate a dictionary of each assembly's dependencies
            Dictionary<AssemblyDefinition, List<AssemblyDefinition>> assemblyDependencyDict = new Dictionary<AssemblyDefinition, List<AssemblyDefinition>>();
            
            foreach (AssemblyDefinition assembly in assemblies)
            {
                assemblyDependencyDict[assembly] = new List<AssemblyDefinition>();

                foreach (var dependencyRef in assembly.MainModule.AssemblyReferences)
                {
                    var dependencyAssembly = assemblies.FirstOrDefault(x => x.FullName == dependencyRef.FullName);

                    if (dependencyAssembly != null)
                    {
                        assemblyDependencyDict[assembly].Add(dependencyAssembly);
                    }
                }
            }

            //sort the assemblies so load the assemblies that are dependant upon first
            List<AssemblyDefinition> sortedAssemblies = Utility.TopologicalSort(assemblies, x => assemblyDependencyDict[x]);

            //special casing for UnityEngine, needs to be reordered to the front
            var unityEngine = sortedAssemblies.FirstOrDefault(x => x.Name.Name == "UnityEngine");
            if (unityEngine != null)
            {
                sortedAssemblies.Remove(unityEngine);
                sortedAssemblies.Insert(0, unityEngine);
            }

            //call the patchers on the assemblies
            foreach (var assembly in sortedAssemblies)
            {
                using (MemoryStream assemblyStream = new MemoryStream())
                {
                    AssemblyLoad?.Invoke(assembly);
                    
                    assembly.Write(assemblyStream);
                    Assembly.Load(assemblyStream.ToArray());
                }

                assembly.Dispose();
            }
        }
    }
}
