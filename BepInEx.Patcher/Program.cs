using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace BepInEx.Patcher
{
    class Program
    {
        static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Failed");
            Console.ResetColor();
            Console.WriteLine(message);
        }

        static void WriteSuccess()
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Success");
            Console.ResetColor();
        }

        static void Main(string[] args)
        {
            Console.WriteLine($"BepInEx Patcher v{Assembly.GetExecutingAssembly().GetName().Version}");

            if (args.Length >= 1) //short circuit for specific dll patch
                Environment.Exit(PatchUnityExe(Path.GetDirectoryName(args[0]), args[0], out string message) ? 0 : 9999);

            bool hasFound = false;
            int patchCount = 0;

            foreach (string exePath in Directory.GetFiles(Directory.GetCurrentDirectory()))
            {
                string gameName = Path.GetFileNameWithoutExtension(exePath);

                string managedDir = Environment.CurrentDirectory + $@"\{gameName}_Data\Managed";
                string unityOutputDLL = Path.GetFullPath($"{managedDir}\\UnityEngine.dll");

                if (!Directory.Exists(managedDir) || !File.Exists(unityOutputDLL))
                    continue;

                hasFound = true;

                Console.Write($"Patching {gameName}... ");

                if (PatchUnityExe(managedDir, unityOutputDLL, out string message))
                {
                    WriteSuccess();
                    patchCount++;
                }
                else
                {
                    WriteError(message);
                }
            }

            Console.WriteLine();

            if (!hasFound)
                Console.WriteLine("Didn't find any games to patch! Exiting.");
            else
                Console.WriteLine($"Patched {patchCount} assemblies!");

            System.Threading.Thread.Sleep(2000);
        }

        static bool PatchUnityExe(string managedDir, string unityOutputDLL, out string message)
        {
            message = null;

            try
            {
                string unityOriginalDLL = Path.GetFullPath($"{managedDir}\\UnityEngine.dll.bak");
                if (!File.Exists(unityOriginalDLL))
                    File.Copy(unityOutputDLL, unityOriginalDLL);

                string harmony = Path.GetFullPath($"{managedDir}\\0Harmony.dll");
                File.WriteAllBytes(harmony, EmbeddedResource.Get("BepInEx.Patcher.0Harmony.dll"));

                string injectedDLL = Path.GetFullPath($"{managedDir}\\BepInEx.dll");
                File.WriteAllBytes(injectedDLL, EmbeddedResource.Get("BepInEx.Patcher.BepInEx.dll"));

                var defaultResolver = new DefaultAssemblyResolver();
                defaultResolver.AddSearchDirectory(managedDir);
                var rp = new ReaderParameters
                {
                    AssemblyResolver = defaultResolver
                };

                using (AssemblyDefinition unity = AssemblyDefinition.ReadAssembly(unityOriginalDLL, rp))
                using (AssemblyDefinition injected = AssemblyDefinition.ReadAssembly(injectedDLL, rp))
                {
                    InjectAssembly(unity, injected);
            
                    unity.Write(unityOutputDLL);
                }
            }
            catch (Exception e)
            {
                message = e.ToString();
                return false;
            }

            return true;
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
