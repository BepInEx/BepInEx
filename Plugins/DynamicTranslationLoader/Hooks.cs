using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynamicTranslationLoader
{
    public static class Hooks
    {
        public static void LabelTextHook(ref string value)
        {
            value = DynamicTranslator.Translate(value);
        }

        public static void SetTextHook(ref string text)
        {
            text = DynamicTranslator.Translate(text);
        }
    }
}
