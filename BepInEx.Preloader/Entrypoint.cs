using System;
using System.Linq;
using System.Reflection;

namespace BepInEx.Preloader
{
    internal static class Entrypoint
    {
        /// <summary>
        ///     The main entrypoint of BepInEx, called from Doorstop.
        /// </summary>
        /// <param name="args">
        ///     The arguments passed in from Doorstop. First argument is the path of the currently executing
        ///     process.
        /// </param>
        public static void Main(string[] args)
        {
            // Manually set up the path for patchers to work
            typeof(Paths).GetProperty(nameof(Paths.ExecutablePath)).SetValue(null, args[0], null);
            //Paths.ExecutablePath = args[0];
            AppDomain.CurrentDomain.AssemblyResolve += LocalResolve;

            Preloader.Run();
        }

        /// <summary>
        ///     A handler for <see cref="AppDomain" />.AssemblyResolve to perform some special handling.
        ///     <para>
        ///         It attempts to check currently loaded assemblies (ignoring the version), and then checks the BepInEx/core path,
        ///         BepInEx/patchers path and the BepInEx folder, all in that order.
        ///     </para>
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        internal static Assembly LocalResolve(object sender, ResolveEventArgs args)
        {
            var assemblyName = new AssemblyName(args.Name);

            var foundAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(x => x.GetName().Name == assemblyName.Name);

            if (foundAssembly != null)
                return foundAssembly;

            if (Utility.TryResolveDllAssembly(assemblyName, Paths.BepInExAssemblyDirectory, out foundAssembly)
                || Utility.TryResolveDllAssembly(assemblyName, Paths.PatcherPluginPath, out foundAssembly)
                || Utility.TryResolveDllAssembly(assemblyName, Paths.PluginPath, out foundAssembly))
                return foundAssembly;

            return null;
        }
    }
}