using BepInEx.Common;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

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
            string unityOutputDLL = Path.GetFullPath(@"KoikatuTrial_Data\Managed\UnityEngine.dll");
            if (!File.Exists(unityOutputDLL))
                Error("\"UnityEngine.dll\" not found.");

            string unityOriginalDLL = Path.GetFullPath(@"KoikatuTrial_Data\Managed\UnityEngine.dll.bak");
            if (!File.Exists(unityOriginalDLL))
                File.Copy(unityOutputDLL, unityOriginalDLL);
            

            string injectedDLL = Path.GetFullPath(@"KoikatuTrial_Data\Managed\BepInEx.dll");
            if (!File.Exists(unityOutputDLL))
                Error("\"BepInEx.dll\" not found.");

            string referenceDir = Path.GetFullPath(@"KoikatuTrial_Data\Managed");


            var defaultResolver = new DefaultAssemblyResolver();
            defaultResolver.AddSearchDirectory(referenceDir);
            
            AssemblyDefinition unity = AssemblyDefinition.ReadAssembly(unityOriginalDLL, new ReaderParameters
            {
                AssemblyResolver = defaultResolver
            });
            AssemblyDefinition injected = AssemblyDefinition.ReadAssembly(injectedDLL, new ReaderParameters
            {
                AssemblyResolver = defaultResolver
            });


            InjectAssembly(unity, injected);
            
            unity.Write(unityOutputDLL);
        }

        static void InjectAssembly(AssemblyDefinition unity, AssemblyDefinition injected)
        {
            //Entry point
            var originalInjectMethod = injected.MainModule.Types.First(x => x.Name == "Chainloader")
                .Methods.First(x => x.Name == "Initialize");

            var injectMethod = unity.MainModule.Import(originalInjectMethod);

            var sceneManager = unity.MainModule.Types.First(x => x.Name == "Application");

            var voidType = unity.MainModule.Import(typeof(void));
            var cctor = new MethodDefinition(".cctor",
                Mono.Cecil.MethodAttributes.Static
                | Mono.Cecil.MethodAttributes.Private
                | Mono.Cecil.MethodAttributes.HideBySig
                | Mono.Cecil.MethodAttributes.SpecialName
                | Mono.Cecil.MethodAttributes.RTSpecialName,
                voidType);

            var ilp = cctor.Body.GetILProcessor();
            ilp.Append(ilp.Create(OpCodes.Call, injectMethod));
            ilp.Append(ilp.Create(OpCodes.Ret));

            sceneManager.Methods.Add(cctor);
        }
    }
}
