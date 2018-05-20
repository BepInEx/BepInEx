using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using MethodAttributes = Mono.Cecil.MethodAttributes;

namespace BepInEx.Bootstrap
{
    public static class Preloader
    {
        #region Path Properties

        public static string ExecutablePath { get; private set; }

        public static string CurrentExecutingAssemblyPath => Assembly.GetExecutingAssembly().CodeBase.Replace("file:///", "").Replace('/', '\\');

        public static string CurrentExecutingAssemblyDirectoryPath => Path.GetDirectoryName(CurrentExecutingAssemblyPath);

        public static string GameName => Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().ProcessName);

        public static string GameRootPath => Path.GetDirectoryName(ExecutablePath);

        public static string ManagedPath => Path.Combine(GameRootPath, Path.Combine($"{GameName}_Data", "Managed"));

        #endregion


        public static Dictionary<string, IList<AssemblyPatcherDelegate>> PatcherDictionary = new Dictionary<string, IList<AssemblyPatcherDelegate>>(StringComparer.OrdinalIgnoreCase);

        public static void AddPatcher(string dllName, AssemblyPatcherDelegate patcher)
        {
            if (PatcherDictionary.TryGetValue(dllName, out IList<AssemblyPatcherDelegate> patcherList))
                patcherList.Add(patcher);
            else
            {
                patcherList = new List<AssemblyPatcherDelegate>();

                patcherList.Add(patcher);

                PatcherDictionary[dllName] = patcherList;
            }
        }

        public static void Main(string[] args)
        {
            ExecutablePath = args[0];

            AppDomain.CurrentDomain.AssemblyResolve += LocalResolve;
            
            try
            {
                //AssemblyPatcher.AssemblyLoad += PatchEntrypoint;
                
                AddPatcher("UnityEngine.dll", PatchEntrypoint);

                AssemblyPatcher.PatchAll(ManagedPath, PatcherDictionary);
            }
            catch (Exception ex)
            {
                //File.WriteAllText("B:\\test.txt", ex.ToString());
            }
        }

        internal static void PatchEntrypoint(AssemblyDefinition assembly)
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

        internal static Assembly LocalResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name == "0Harmony, Version=1.1.0.0, Culture=neutral, PublicKeyToken=null")
                return Assembly.LoadFile(Path.Combine(CurrentExecutingAssemblyDirectoryPath, "0Harmony.dll"));

            return null;
        }
    }
}
