using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BepInEx.Patcher.Internal
{
    public class ExitScenePlugin : IPatchPlugin
    {
        public void Patch(AssemblyDefinition assembly)
        {
            TypeDefinition exitScene = assembly.MainModule.Types.First(x => x.Name == "ExitScene");
            var startExit = exitScene.Methods.First(x => x.Name == "Start");


            var IL = startExit.Body.GetILProcessor();
            IL.Replace(startExit.Body.Instructions[26], IL.Create(OpCodes.Ldstr, "Do you want to exit the character maker?"));
        }
    }
}
