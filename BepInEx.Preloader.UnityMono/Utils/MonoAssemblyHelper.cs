using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using Mono.Cecil;

namespace BepInEx.Preloader.UnityMono.Utils;

internal static class MonoAssemblyHelper
{
    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint mono_image_open_from_data_with_name(nint data,
                                                                   uint dataLength,
                                                                   bool needCopy,
                                                                   out MonoImageOpenStatus status,
                                                                   bool refOnly,
                                                                   [MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport("__Internal", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint mono_assembly_load_from_full(nint image,
                                                            [MarshalAs(UnmanagedType.LPStr)] string fileName,
                                                            out MonoImageOpenStatus status,
                                                            bool refOnly);

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
                var image = mono_image_open_from_data_with_name((nint) data, (uint) AssemblyData.Length, true,
                                                                out var status, false, fullPath);
                if (status != MonoImageOpenStatus.MONO_IMAGE_OK)
                    throw new BadImageFormatException($"Failed to load image {fullPath}: {status}");
                mono_assembly_load_from_full(image, fullPath, out status, false);
                if (status != MonoImageOpenStatus.MONO_IMAGE_OK)
                    throw new BadImageFormatException($"Failed to load assembly {fullPath}: {status}");
                return GetAssemblyByName(AssemblyName);
            }
        }
    }
}
