using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace BepInEx.Patcher.Internal
{
    public class SliderPlugin : IPatchPlugin
    {
        public void Patch(AssemblyDefinition assembly)
        {
            TypeDefinition customBase = assembly.MainModule.Types.First(x => x.Name == "CustomBase");

            var methods = customBase.Methods;
            
            var convertTextFromRate = methods.First(x => x.Name == "ConvertTextFromRate");

            var IL = convertTextFromRate.Body.GetILProcessor();
            IL.Replace(convertTextFromRate.Body.Instructions[0], IL.Create(OpCodes.Ldc_I4, -0));
            IL.Replace(convertTextFromRate.Body.Instructions[2], IL.Create(OpCodes.Ldc_I4, 200));
            
            var convertRateFromText = methods.First(x => x.Name == "ConvertRateFromText");

            IL = convertRateFromText.Body.GetILProcessor();
            IL.Replace(convertRateFromText.Body.Instructions[11], IL.Create(OpCodes.Ldc_I4, -0));
            IL.Replace(convertRateFromText.Body.Instructions[13], IL.Create(OpCodes.Ldc_I4, 200));
        }
    }
}
