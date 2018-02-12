using BepInEx.Patcher.Internal;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            string assemblyDLL = Path.GetFullPath("Assembly-CSharp.dll");
            if (!File.Exists(assemblyDLL))
                Error("\"Assembly-CSharp.dll\" not found.");

            string assemblyOriginalDLL = Path.GetFullPath("Assembly-CSharp.dll.bak");
            if (!File.Exists(assemblyOriginalDLL))
                File.Copy(assemblyDLL, assemblyOriginalDLL);


            string unityOutputDLL = Path.GetFullPath("UnityEngine.dll");
            if (!File.Exists(unityOutputDLL))
                Error("\"UnityEngine.dll\" not found.");

            string unityOriginalDLL = Path.GetFullPath("UnityEngine.dll.bak");
            if (!File.Exists(unityOriginalDLL))
                File.Copy(unityOutputDLL, unityOriginalDLL);


            string injectedDLL = Path.GetFullPath("BepInEx.dll");
            if (!File.Exists(unityOutputDLL))
                Error("\"BepInEx.dll\" not found.");

            string referenceDir = Directory.GetCurrentDirectory();


            var defaultResolver = new DefaultAssemblyResolver();
            defaultResolver.AddSearchDirectory(referenceDir);

            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(assemblyOriginalDLL, new ReaderParameters {
                AssemblyResolver = defaultResolver
            });
            AssemblyDefinition unity = AssemblyDefinition.ReadAssembly(unityOriginalDLL, new ReaderParameters
            {
                AssemblyResolver = defaultResolver
            });
            AssemblyDefinition injected = AssemblyDefinition.ReadAssembly(injectedDLL, new ReaderParameters
            {
                AssemblyResolver = defaultResolver
            });



            //IPatchPlugin exitScene = new ExitScenePlugin();
            //exitScene.Patch(assembly);

            //IPatchPlugin slider = new SliderPlugin();
            //slider.Patch(assembly);

            //IPatchPlugin title = new TitleScenePlugin();
            //title.Patch(assembly);


            InjectAssembly(assembly, unity, injected);


            assembly.Write(assemblyDLL);
            unity.Write(unityOutputDLL);
        }

        static void InjectAssembly(AssemblyDefinition assembly, AssemblyDefinition unity, AssemblyDefinition injected)
        {
            var originalInjectMethod = injected.MainModule.Types.First(x => x.Name == "Chainloader").Methods.First(x => x.Name == "Initialize");

            var injectMethod = unity.MainModule.Import(originalInjectMethod);

            var sceneManager = unity.MainModule.Types.First(x => x.Name == "SceneManager");

            foreach (var loadScene in sceneManager.Methods.Where(x => x.Name == "LoadScene"))
            {
                ILProcessor IL = loadScene.Body.GetILProcessor();

                IL.InsertBefore(loadScene.Body.Instructions[0], IL.Create(OpCodes.Call, injectMethod));
            }
        }
    }
}
