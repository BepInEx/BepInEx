using BepInEx.Patcher.Internal;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BepInEx.Patcher
{
    class Program
    {
        const string originalDLL = @"M:\koikatu\KoikatuTrial_Data\Managed\Assembly-CSharp-original.dll";
        const string outputDLL = @"M:\koikatu\KoikatuTrial_Data\Managed\Assembly-CSharp.dll";
        
        const string unityOriginalDLL = @"M:\koikatu\KoikatuTrial_Data\Managed\UnityEngine-original.dll";
        const string unityOutputDLL = @"M:\koikatu\KoikatuTrial_Data\Managed\UnityEngine.dll";
        
        const string injectedDLL = @"M:\koikatu\KoikatuTrial_Data\Managed\BepInEx.dll";

        const string referenceDir = @"M:\koikatu\KoikatuTrial_Data\Managed\";

        static void Main(string[] args)
        {
            var defaultResolver = new DefaultAssemblyResolver();
            defaultResolver.AddSearchDirectory(referenceDir);

            AssemblyDefinition assembly = AssemblyDefinition.ReadAssembly(originalDLL, new ReaderParameters {
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



            IPatchPlugin exitScene = new ExitScenePlugin();
            exitScene.Patch(assembly);

            //IPatchPlugin slider = new SliderPlugin();
            //slider.Patch(assembly);

            //IPatchPlugin title = new TitleScenePlugin();
            //title.Patch(assembly);


            InjectAssembly(assembly, unity, injected);


            assembly.Write(outputDLL);
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
