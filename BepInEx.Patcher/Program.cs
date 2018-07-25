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
            Console.WriteLine();
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
            bool hasFailure = false;
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
                    hasFailure = true;
                }
            }

            Console.WriteLine();

            if (!hasFound)
                Console.WriteLine("Didn't find any games to patch! Exiting.");
            else
                Console.WriteLine($"Patched {patchCount} assemblies!");

            if (hasFailure)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadKey();
            }
            else
                System.Threading.Thread.Sleep(3000);
        }

        static bool PatchUnityExe(string managedDir, string unityOutputDLL, out string message)
        {
            message = null;

            try
            {
                string injectedDLL = Path.GetFullPath($"{managedDir}\\BepInEx.Bootstrap.dll");
                File.WriteAllBytes(injectedDLL, EmbeddedResource.Get("BepInEx.Patcher.BepInEx.Bootstrap.dll"));

                var defaultResolver = new DefaultAssemblyResolver();
                defaultResolver.AddSearchDirectory(managedDir);
                var rp = new ReaderParameters
                {
                    AssemblyResolver = defaultResolver
                };

                string unityBackupDLL = Path.GetFullPath($"{managedDir}\\UnityEngine.dll.bak");
                
                //determine which assembly to use as a base
                AssemblyDefinition unity = AssemblyDefinition.ReadAssembly(unityOutputDLL, rp);

                if (!VerifyAssembly(unity, out message))
                {
                    //try and fall back to .bak if exists
                    if (File.Exists(unityBackupDLL))
                    {
                        unity.Dispose();
                        unity = AssemblyDefinition.ReadAssembly(unityBackupDLL, rp);

                        if (!VerifyAssembly(unity, out message))
                        {
                            //can't use anything
                            unity.Dispose();
                            message += "\r\nThe backup is not usable.";
                            return false;
                        }
                    }
                    else
                    {
                        //can't use anything
                        unity.Dispose();
                        message += "\r\nNo backup exists.";
                        return false;
                    }
                }
                else
                {
                    //make a backup of the assembly
                    File.Copy(unityOutputDLL, unityBackupDLL, true);
                    unity.Dispose();
                    unity = AssemblyDefinition.ReadAssembly(unityBackupDLL, rp);
                }
                
                //patch
                using (unity)
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
            var originalInjectMethod = injected.MainModule.Types.First(x => x.Name == "Entrypoint")
                .Methods.First(x => x.Name == "Init");

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

        static bool VerifyAssembly(AssemblyDefinition unity, out string message)
        {
            bool canPatch = true;
            message = "";

            //check if already patched
            if (unity.MainModule.AssemblyReferences.Any(x => x.Name.Contains("BepInEx")))
            {
                canPatch = false;

                message += "This assembly has already been patched by BepInEx.\r\n";
            }

	        message = message.Trim();
            return canPatch;
        }
    }
}