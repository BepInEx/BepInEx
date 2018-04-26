using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;
using System.Linq;

namespace BepInEx.Patcher
{
    class Program
    {
        static void Error(string message)
        {
            Console.WriteLine($"Error: {message}");
            Console.WriteLine("Press any key to continue...");
            Console.ReadKey();
            Environment.Exit(1);
        }

        static void Main(string[] args)
        {
            foreach (string exePath in Directory.GetFiles(Directory.GetCurrentDirectory()))
            {

                string managedDir = Environment.CurrentDirectory + $@"\{Path.GetFileNameWithoutExtension(exePath)}_Data\Managed";
                string unityOutputDLL = Path.GetFullPath($"{managedDir}\\UnityEngine.dll");

                if (!Directory.Exists(managedDir) || !File.Exists(unityOutputDLL))
                    continue;

                string unityOriginalDLL = Path.GetFullPath($"{managedDir}\\UnityEngine.dll.bak");
                if (!File.Exists(unityOriginalDLL))
                    File.Copy(unityOutputDLL, unityOriginalDLL);

                string harmony = Path.GetFullPath($"{managedDir}\\0Harmony.dll");
                File.WriteAllBytes(harmony, Resources._0Harmony);

                string injectedDLL = Path.GetFullPath($"{managedDir}\\BepInEx.dll");
                File.WriteAllBytes(injectedDLL, Resources.BepInEx);

                var defaultResolver = new DefaultAssemblyResolver();
                defaultResolver.AddSearchDirectory(managedDir);
                var rp = new ReaderParameters
                {
                    AssemblyResolver = defaultResolver
                };

                AssemblyDefinition unity = AssemblyDefinition.ReadAssembly(unityOriginalDLL, rp);
                AssemblyDefinition injected = AssemblyDefinition.ReadAssembly(injectedDLL, rp);

                InjectAssembly(unity, injected);
            
                unity.Write(unityOutputDLL);
            }
        }

        static void InjectAssembly(AssemblyDefinition unity, AssemblyDefinition injected)
        {
            //Entry point
            var originalInjectMethod = injected.MainModule.Types.First(x => x.Name == "Chainloader")
                .Methods.First(x => x.Name == "Initialize");

            var injectMethod = unity.MainModule.ImportReference(originalInjectMethod);

            var sceneManager = unity.MainModule.Types.First(x => x.Name == "Application");

            var voidType = unity.MainModule.ImportReference(typeof(void));
            var cctor = new MethodDefinition(".cctor",
                MethodAttributes.Static
                | MethodAttributes.Private
                | MethodAttributes.HideBySig
                | MethodAttributes.SpecialName
                | MethodAttributes.RTSpecialName,
                voidType);

            var ilp = cctor.Body.GetILProcessor();
            ilp.Append(ilp.Create(OpCodes.Call, injectMethod));
            ilp.Append(ilp.Create(OpCodes.Ret));

            sceneManager.Methods.Add(cctor);
        }
    }
}
