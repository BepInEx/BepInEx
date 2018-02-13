using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace BepInEx.Patcher
{
    public interface IPatchPlugin
    {
        void Patch(AssemblyDefinition assembly);
    }
}
