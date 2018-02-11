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

            IPatchPlugin slider = new SliderPlugin();
            slider.Patch(assembly);

            IPatchPlugin title = new TitleScenePlugin();
            title.Patch(assembly);


            InjectAssembly(assembly, unity, injected);


            assembly.Write(outputDLL);
            unity.Write(unityOutputDLL);
        }

        static void InjectAssembly(AssemblyDefinition assembly, AssemblyDefinition unity, AssemblyDefinition injected)
        {
            ILProcessor IL;

            //Initialize
            var originalInjectMethod = injected.MainModule.Types.First(x => x.Name == "Target").Methods.First(x => x.Name == "Initialize");

            var injectMethod = unity.MainModule.Import(originalInjectMethod);

            var sceneManager = unity.MainModule.Types.First(x => x.Name == "SceneManager");

            foreach (var loadScene in sceneManager.Methods.Where(x => x.Name == "LoadScene"))
            {
                IL = loadScene.Body.GetILProcessor();

                IL.InsertBefore(loadScene.Body.Instructions[0], IL.Create(OpCodes.Call, injectMethod));
            }


            //CustomInitializer
            originalInjectMethod = injected.MainModule.Types.First(x => x.Name == "Target").Methods.First(x => x.Name == "InitializeCustomBase");

            injectMethod = assembly.MainModule.Import(originalInjectMethod);

            var customControl = assembly.MainModule.Types.First(x => x.Name == "CustomControl");

            var customInitialize = customControl.Methods.First(x => x.Name == "Initialize");

            IL = customInitialize.Body.GetILProcessor();

            //IL.Replace(customInitialize.Body.Instructions[169], IL.Create(OpCodes.Call, customControl.Properties[4].GetMethod));
            //IL.Append(IL.Create(OpCodes.Call, injectMethod));

            IL.Replace(customInitialize.Body.Instructions[169], IL.Create(OpCodes.Call, injectMethod));

            customInitialize.Body.Instructions[169].Offset = 0x01f0;

            IL.Append(IL.Create(OpCodes.Ret));

            //injected.MainModule.Import(assembly.MainModule.Types.First(x => x.Name == "CustomBase"));
        }
    }
}
