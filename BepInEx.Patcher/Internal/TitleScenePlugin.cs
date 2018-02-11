using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BepInEx.Patcher.Internal
{
    public class TitleScenePlugin : IPatchPlugin
    {
        public void Patch(AssemblyDefinition assembly)
        {
            TypeDefinition CustomTrialTitle = assembly.MainModule.Types.First(x => x.Name == "CustomTrialTitle");
            var enter = CustomTrialTitle.Methods.First(x => x.Name == "Enter");


            var IL = enter.Body.GetILProcessor();
            //IL.Replace(enter.Body.Instructions[32], IL.Create(OpCodes.Ldstr, "Title"));
            IL.Replace(enter.Body.Instructions[15], IL.Create(OpCodes.Ldstr, "Title"));

            var lambda = (MethodDefinition)enter.Body.Instructions[45].Operand;

            var onCustom = CustomTrialTitle.Methods.First(x => x.Name == "OnCustom");
            onCustom.Body.Instructions[1].Operand = "Title";

            IL = lambda.Body.GetILProcessor();

            var method = (GenericInstanceMethod)lambda.Body.Instructions[2].Operand;

            method.GenericArguments[0] = assembly.MainModule.Types.First(x => x.Name == "TitleScene");

            //IL.Remove(lambda.Body.Instructions[1]);
            //IL.Remove(lambda.Body.Instructions[0]);

            //IL.InsertBefore(lambda.Body.Instructions[0], IL.Create(OpCodes.Ldstr, "Title"));
        }
    }
}
