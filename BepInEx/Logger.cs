using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BepInEx
{
    public static class BepInLogger
    {
        public delegate void EntryLoggedEventHandler(string entry, bool show = false);

        public static event EntryLoggedEventHandler EntryLogged;


        public static void Log(string entry, bool show = false)
        {
            EntryLogged?.Invoke(entry, show);
        }
    }
}
