using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx.Common;
using Mono.Cecil;

namespace BepInEx.Bootstrap
{
    public delegate void AssemblyPatcherDelegate(ref AssemblyDefinition assembly);

    public static class AssemblyPatcher
    {
        private static bool DumpingEnabled => bool.TryParse(Config.GetEntry("preloader-dumpassemblies", "false"), out bool result) ? result : false;

        public static void PatchAll(string directory, Dictionary<AssemblyPatcherDelegate, IEnumerable<string>> patcherMethodDictionary)
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
            AssemblyDefinition[] sortedAssemblies = Utility.TopologicalSort(assemblies, x => assemblyDependencyDict[x]).ToArray();

	        List<string> sortedAssemblyFilenames = sortedAssemblies.Select(x => assemblyFilenames[x]).ToList();

            //call the patchers on the assemblies
	        foreach (var patcherMethod in patcherMethodDictionary)
	        {
		        foreach (string assemblyFilename in patcherMethod.Value)
		        {
			        int index = sortedAssemblyFilenames.FindIndex(x => x == assemblyFilename);

			        if (index < 0)
				        continue;

					Patch(ref sortedAssemblies[index], patcherMethod.Key);
		        }
	        }


			for (int i = 0; i < sortedAssemblies.Length; i++)
			{
                string filename = Path.GetFileName(assemblyFilenames[sortedAssemblies[i]]);

                if (DumpingEnabled)
                {
                    using (MemoryStream mem = new MemoryStream())
                    {
                        string dirPath = Path.Combine(Preloader.PluginPath, "DumpedAssemblies");

                        if (!Directory.Exists(dirPath))
                            Directory.CreateDirectory(dirPath);
                            
	                    sortedAssemblies[i].Write(mem);
                        File.WriteAllBytes(Path.Combine(dirPath, filename), mem.ToArray());
                    }
                }

				Load(sortedAssemblies[i]);
#if CECIL_10
				sortedAssemblies[i].Dispose();
#endif
            }
        }

        public static void Patch(ref AssemblyDefinition assembly, AssemblyPatcherDelegate patcherMethod)
        {
	        patcherMethod.Invoke(ref assembly);
        }

	    public static void Load(AssemblyDefinition assembly)
	    {
		    using (MemoryStream assemblyStream = new MemoryStream())
		    {
			    assembly.Write(assemblyStream);
			    Assembly.Load(assemblyStream.ToArray());
		    }
	    }
    }
}
