using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Common;
using Mono.Cecil;

namespace BepInEx.Bootstrap
{
    public delegate void AssemblyPatcherDelegate(AssemblyDefinition assembly);

    public static class AssemblyPatcher
    {
        public static void PatchAll(string directory, Dictionary<string, IList<AssemblyPatcherDelegate>> patcherMethodDictionary)
        {
            //load all the requested assemblies
            List<AssemblyDefinition> assemblies = new List<AssemblyDefinition>();
            Dictionary<AssemblyDefinition, string> assemblyFilenames = new Dictionary<AssemblyDefinition, string>();

            foreach (string assemblyPath in Directory.GetFiles(directory, "*.dll"))
            {
                var assembly = AssemblyDefinition.ReadAssembly(assemblyPath);
                
                //NOTE: this is special cased here because the dependency handling for System.dll is a bit wonky
                //System has an assembly reference to itself, and it also has a reference to Mono.Security causing a circular dependency
                //It's also generally dangerous to change system.dll since so many things rely on it, 
                // and it's already loaded into the appdomain since this loader references it, so we might as well skip it
                if (assembly.Name.Name == "System"
                    || assembly.Name.Name == "mscorlib") //mscorlib is already loaded into the appdomain so it can't be patched
                {
#if CECIL_10
                    assembly.Dispose();
#endif
                    continue;
                }

                assemblies.Add(assembly);
                assemblyFilenames[assembly] = Path.GetFileName(assemblyPath);
            }

            //generate a dictionary of each assembly's dependencies
            Dictionary<AssemblyDefinition, IList<AssemblyDefinition>> assemblyDependencyDict = new Dictionary<AssemblyDefinition, IList<AssemblyDefinition>>();
            
            foreach (AssemblyDefinition assembly in assemblies)
            {
                assemblyDependencyDict[assembly] = new List<AssemblyDefinition>();

                foreach (var dependencyRef in assembly.MainModule.AssemblyReferences)
                {
                    var dependencyAssembly = assemblies.FirstOrDefault(x => x.FullName == dependencyRef.FullName);

                    if (dependencyAssembly != null)
                        assemblyDependencyDict[assembly].Add(dependencyAssembly);
                }
            }

            //sort the assemblies so load the assemblies that are dependant upon first
            IEnumerable<AssemblyDefinition> sortedAssemblies = Utility.TopologicalSort(assemblies, x => assemblyDependencyDict[x]);

            //call the patchers on the assemblies
            foreach (var assembly in sortedAssemblies)
            {
#if CECIL_10
                using (assembly)
#endif
                {
                    //skip if we aren't patching it
                    if (!patcherMethodDictionary.TryGetValue(Path.GetFileName(assemblyFilenames[assembly]), out IList<AssemblyPatcherDelegate> patcherMethods))
                        continue;

                    Patch(assembly, patcherMethods);
                }
            }
        }

        public static void Patch(AssemblyDefinition assembly, IEnumerable<AssemblyPatcherDelegate> patcherMethods)
        {
            using (MemoryStream assemblyStream = new MemoryStream())
            {
                foreach (AssemblyPatcherDelegate method in patcherMethods)
                    method.Invoke(assembly);

                assembly.Write(assemblyStream);
                Assembly.Load(assemblyStream.ToArray());
            }
        }
    }
}
