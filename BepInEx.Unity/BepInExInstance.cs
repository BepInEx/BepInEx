using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BepInEx.Unity.Bootstrap;

namespace BepInEx.Unity
{
    public static class BepInExInstance
    {
		public static UnityChainloader Chainloader { get; }
    }
}
