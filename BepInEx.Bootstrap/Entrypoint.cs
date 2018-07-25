using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace BepInEx.Bootstrap
{
    public static class Entrypoint
    {
	    public static void Init()
	    {
		    AppDomain.CurrentDomain.AssemblyResolve += ResolveBepInEx;

			Linker.StartBepInEx();
	    }

	    private static readonly string LocalDirectory = Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);

	    private static Assembly ResolveBepInEx(object sender, ResolveEventArgs args)
	    {
		    string path = Path.Combine(LocalDirectory, $@"BepInEx\core\{new AssemblyName(args.Name).Name}.dll");

		    if (!File.Exists(path))
			    return null;

		    try
		    {
			    return Assembly.LoadFile(path);
		    }
		    catch (Exception)
		    {
			    return null;
		    }
	    }
    }
}