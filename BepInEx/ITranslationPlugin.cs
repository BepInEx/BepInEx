using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BepInEx
{
    public interface ITranslationPlugin
    {
        bool TryTranslate(string input, out string output);
    }
}
