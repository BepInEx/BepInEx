using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using BepInEx.Preloader.Core;
using Mono.Cecil;
using MonoMod.Utils;

namespace BepInEx.Unity.Mono.Preloader.Utils;

internal static class MonoAssemblyHelper
{
    static MonoAssemblyHelper()
    {
        // We can't use mono's __Internal because on Windows it will use GetModuleHandleW(NULL) that will
        // in turn return the module to the EXE and not mono.dll (at least on Unity versions < 5).
        typeof(MonoAssemblyHelper).ResolveDynDllImports(new()
        {
            ["mono"] = new()
            {
                EnvVars.DOORSTOP_MONO_LIB_PATH
            }
        });
    }

    private static ReadAssemblyResult ReadAssemblyData(string filePath)
    {
        return ReadAssemblyData(File.ReadAllBytes(filePath));
    }

    private static ReadAssemblyResult ReadAssemblyData(byte[] assemblyData)
    {
        using var ms = new MemoryStream(assemblyData);
        using var ad = AssemblyDefinition.ReadAssembly(ms);

        return new ReadAssemblyResult
        {
            AssemblyName = ad.Name.Name,
            AssemblyData = assemblyData
        };
    }

    private static Assembly GetAssemblyByName(string assemblyName)
    {
        return AppDomain.CurrentDomain.GetAssemblies()
                        .FirstOrDefault(a => Utility.TryParseAssemblyName(a.FullName, out var name) &&
                                             name.Name == assemblyName);
    }

    public static bool TryResolveDllAssembly(AssemblyName assemblyName, string directory, out Assembly assembly) =>
        Utility.TryResolveDllAssembly(assemblyName, directory, Load, out assembly);

    public static Assembly LoadFromMemory(byte[] data, string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        return ReadAssemblyData(data).Load(fullPath);
    }

    public static Assembly Load(string filePath)
    {
        var fullPath = Path.GetFullPath(filePath);
        return ReadAssemblyData(fullPath).Load(fullPath);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint ImageOpenDelegate(nint data,
                                            uint dataLength,
                                            bool needCopy,
                                            out MonoImageOpenStatus status,
                                            bool refOnly,
                                            [MarshalAs(UnmanagedType.LPStr)] string name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint AssemblyLoadDelegate(nint image,
                                               [MarshalAs(UnmanagedType.LPStr)] string fileName,
                                               out MonoImageOpenStatus status,
                                               bool refOnly);

    private enum MonoImageOpenStatus
    {
        MONO_IMAGE_OK,
        MONO_IMAGE_ERROR_ERRNO,
        MONO_IMAGE_MISSING_ASSEMBLYREF,
        MONO_IMAGE_IMAGE_INVALID
    }

    private class ReadAssemblyResult
    {
        public byte[] AssemblyData;
        public string AssemblyName;

        public unsafe Assembly Load(string fullPath)
        {
            var assembly = GetAssemblyByName(AssemblyName);
            if (assembly != null)
                return assembly;

            fixed (byte* data = &AssemblyData[0])
            {
                var image = imageOpen((nint) data, (uint) AssemblyData.Length, true,
                                      out var status, false, fullPath);
                if (status != MonoImageOpenStatus.MONO_IMAGE_OK)
                    throw new BadImageFormatException($"Failed to load image {fullPath}: {status}");
                assemblyLoad(image, fullPath, out status, false);
                if (status != MonoImageOpenStatus.MONO_IMAGE_OK)
                    throw new BadImageFormatException($"Failed to load assembly {fullPath}: {status}");
                return GetAssemblyByName(AssemblyName);
            }
        }
    }
#pragma warning disable CS0649
    [DynDllImport("mono", "mono_image_open_from_data_with_name")]
    private static ImageOpenDelegate imageOpen;

    [DynDllImport("mono", "mono_assembly_load_from_full")]
    private static AssemblyLoadDelegate assemblyLoad;
#pragma warning restore CS0649
}
