using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace BepisLoader;

public class BepisLoader
{
    internal static string resoDir = string.Empty;
    internal static AssemblyLoadContext alc = null!;
    static void Main(string[] args)
    {
#if DEBUG
        File.WriteAllText("BepisLoader.log", "BepisLoader started\n");
#endif
        resoDir = Directory.GetCurrentDirectory();

        alc = new BepisLoadContext();

        // TODO: removing this breaks stuff, idk why
        AppDomain.CurrentDomain.AssemblyResolve += ResolveGameDll;

        var bepinPath = Path.Combine(resoDir, "BepInEx");
        var bepinArg = Array.IndexOf(args.Select(x => x?.ToLowerInvariant()).ToArray(), "--bepinex-target");
        if (bepinArg != -1 && args.Length > bepinArg + 1)
        {
            bepinPath = args[bepinArg + 1];
        }
        Log("Loading BepInEx from " + bepinPath);

        var asm = alc.LoadFromAssemblyPath(Path.Combine(bepinPath, "core", "BepInEx.NET.CoreCLR.dll"));

        var resoDllPath = Path.Combine(resoDir, "Renderite.Host.dll");
        if (!File.Exists(resoDllPath)) resoDllPath = Path.Combine(resoDir, "Resonite.dll");

        var t = asm.GetType("StartupHook");
        var m = t.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static, [typeof(string), typeof(string), typeof(AssemblyLoadContext)]);
        m.Invoke(null, [resoDllPath, bepinPath, alc]);

        // Find and load Resonite
        var resoAsm = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == "Resonite");
        if (resoAsm == null)
        {
            resoAsm = alc.LoadFromAssemblyPath(resoDllPath);
        }
        try
        {
            var result = resoAsm.EntryPoint!.Invoke(null, [args]);
            if (result is Task task) task.Wait();
        }
        catch (Exception e)
        {
            File.WriteAllLines("BepisCrash.log", [DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " - Resonite crashed", e.ToString()]);
        }
    }

    static Assembly? ResolveGameDll(object? sender, ResolveEventArgs args)
    {
        var assemblyName = new AssemblyName(args.Name);

        return ResolveInternal(assemblyName);
    }

    static Assembly? ResolveInternal(AssemblyName assemblyName)
    {
        var found = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(x => x.GetName().Name == assemblyName.Name);
        if (found != null)
        {
            return found;
        }

        if (assemblyName.Name == "System.Management") return null;

        var targetPath = Path.Combine(resoDir, assemblyName.Name + ".dll");
        if (File.Exists(targetPath))
        {
            var asm = alc.LoadFromAssemblyPath(targetPath);
            return asm;
        }

        return null;
    }

    private class BepisLoadContext : AssemblyLoadContext
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            return ResolveInternal(assemblyName);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var rid = GetRuntimeIdentifier();

            var nativeLibs = Path.Join(resoDir, "runtimes", rid, "native");
            IEnumerable<string> potentialPaths = [unmanagedDllName, Path.Combine(nativeLibs, GetUnmanagedLibraryName(unmanagedDllName))];
            if (unmanagedDllName.EndsWith("steam_api64.so")) potentialPaths = ((IEnumerable<string>)["libsteam_api.so"]).Concat(potentialPaths);

            Log("NativeLib " + unmanagedDllName);
            foreach (var path in potentialPaths)
            {
                Log("  Testing: " + path);
                if (File.Exists(path))
                {
                    Log("  Exists! " + path);
                    var dll = LoadUnmanagedDllFromPath(path);
                    if (dll != IntPtr.Zero)
                    {
                        Log("  Loaded! " + path);
                        return dll;
                    }
                }
            }

            return IntPtr.Zero;
        }


        private static string GetRuntimeIdentifier()
        {
            string os;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                os = "win";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                os = "osx";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                os = "linux";
            else
                throw new PlatformNotSupportedException();

            string arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.X86 => "-x86",
                Architecture.X64 => "-x64",
                Architecture.Arm64 => "-arm64",
                _ => ""
            };

            return $"{os}{arch}";
        }
        private static string GetUnmanagedLibraryName(string name)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return $"{name}.dll";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return $"lib{name}.so";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return $"lib{name}.dylib";

            throw new PlatformNotSupportedException();
        }
    }

#if DEBUG
    private static object _lock = new object();
#endif
    public static void Log(string message)
    {
#if DEBUG
        lock (_lock)
        {
            File.AppendAllLines("BepisLoader.log", [message]);
        }
#endif
    }
}
