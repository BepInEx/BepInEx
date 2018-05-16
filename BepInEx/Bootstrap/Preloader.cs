using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace BepInEx.Bootstrap
{
    internal static class Preloader
    {
        public static string ExecutablePath { get; private set; }

        internal static string CurrentExecutingAssemblyPath => Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "").Replace('/', '\\');

        internal static string GameName => Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().ProcessName);

        internal static string GameRootPath => Path.GetDirectoryName(ExecutablePath);

        internal static string ManagedPath => Path.Combine(GameRootPath, Path.Combine($"{GameName}_Data", "Managed"));


        public static void Main(string[] args)
        {
            ExecutablePath = args[0];
            
            try
            {
                AssemblyPatcher.AssemblyLoad += PatchEntrypoint;

                AssemblyPatcher.PatchAll(ManagedPath);
            }
            catch (Exception ex)
            {
                //File.WriteAllText("B:\\test.txt", ex.ToString());
            }
        }

        static void PatchEntrypoint(AssemblyDefinition assembly)
        {
            if (assembly.Name.Name == "UnityEngine")
            {
                using (AssemblyDefinition injected = AssemblyDefinition.ReadAssembly(CurrentExecutingAssemblyPath))
                {
                    var originalInjectMethod = injected.MainModule.Types.First(x => x.Name == "Chainloader")
                        .Methods.First(x => x.Name == "Initialize");

                    var injectMethod = assembly.MainModule.ImportReference(originalInjectMethod);

                    var sceneManager = assembly.MainModule.Types.First(x => x.Name == "Application");

                    var voidType = assembly.MainModule.ImportReference(typeof(void));
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
    }
}
