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


            string tmOutputDLL = Path.GetFullPath("TextMeshPro-1.0.55.56.0b12.dll");
            if (!File.Exists(tmOutputDLL))
                Error("\"TextMeshPro-1.0.55.56.0b12.dll\" not found.");

            string tmOriginalDLL = Path.GetFullPath("TextMeshPro-1.0.55.56.0b12.dll.bak");
            if (!File.Exists(tmOriginalDLL))
                File.Copy(tmOutputDLL, tmOriginalDLL);


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
            AssemblyDefinition tm = AssemblyDefinition.ReadAssembly(tmOriginalDLL, new ReaderParameters
            {
                AssemblyResolver = defaultResolver
            });
            AssemblyDefinition injected = AssemblyDefinition.ReadAssembly(injectedDLL, new ReaderParameters
            {
                AssemblyResolver = defaultResolver
            });



            //IPatchPlugin exitScene = new ExitScenePlugin();
            //exitScene.Patch(assembly);


            InjectAssembly(assembly, unity, tm, injected);


            assembly.Write(assemblyDLL);
            unity.Write(unityOutputDLL);
            tm.Write(tmOutputDLL);
        }

        static void InjectAssembly(AssemblyDefinition assembly, AssemblyDefinition unity, AssemblyDefinition tm, AssemblyDefinition injected)
        {
            //Entry point
            var originalInjectMethod = injected.MainModule.Types.First(x => x.Name == "Chainloader").Methods.First(x => x.Name == "Initialize");

            var injectMethod = unity.MainModule.Import(originalInjectMethod);

            var sceneManager = unity.MainModule.Types.First(x => x.Name == "SceneManager");

            ILProcessor IL;

            foreach (var loadScene in sceneManager.Methods.Where(x => x.Name == "LoadScene"))
            {
                IL = loadScene.Body.GetILProcessor();

                IL.InsertBefore(loadScene.Body.Instructions[0], IL.Create(OpCodes.Call, injectMethod));
            }

            //Text loading
            originalInjectMethod = injected.MainModule.Types.First(x => x.Name == "Chainloader").Methods.First(x => x.Name == "TextLoadedHook");
            injectMethod = tm.MainModule.Import(originalInjectMethod);

            TypeDefinition tmpText = tm.MainModule.Types.First(x => x.Name == "TMP_Text");
            var setText = tmpText.Methods.First(x => x.Name == "set_text");

            IL = setText.Body.GetILProcessor();
            
            IL.InsertAfter(setText.Body.Instructions[11], IL.Create(OpCodes.Call, injectMethod));
            //IL.InsertAfter(setText.Body.Instructions[3], IL.Create(OpCodes.Call, injectMethod));
        }
    }
}
