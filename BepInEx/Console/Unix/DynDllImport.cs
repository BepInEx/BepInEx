// Dummy file from https://github.com/MonoMod/MonoMod/pull/65 until it gets merged

#pragma warning disable IDE1006 // Naming Styles

using System.Reflection;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;
using System.IO;
using System.ComponentModel;
using System.Linq;

namespace MonoMod.Utils.Dummy
{
    internal static class DynDll
    {
        /// <summary>
        /// Allows you to remap library paths / names and specify loading flags. Useful for cross-platform compatibility. Applies only to DynDll.
        /// </summary>
        public static Dictionary<string, List<DynDllMapping>> Mappings = new Dictionary<string, List<DynDllMapping>>();

        #region kernel32 imports

        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);
        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hLibModule);
        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        #endregion

        #region dl imports

        [DllImport("dl", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dlopen(string filename, int flags);
        [DllImport("dl", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern bool dlclose(IntPtr handle);
        [DllImport("dl", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dlsym(IntPtr handle, string symbol);
        [DllImport("dl", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
        private static extern IntPtr dlerror();

        #endregion

        private static bool CheckError(out Exception exception)
        {
            if (PlatformHelper.Is(Platform.Windows))
            {
                int errorCode = Marshal.GetLastWin32Error();
                if (errorCode != 0)
                {
                    exception = new Win32Exception(errorCode);
                    return false;
                }
            }
            else
            {
                IntPtr errorCode = dlerror();
                if (errorCode != IntPtr.Zero)
                {
                    exception = new Win32Exception(Marshal.PtrToStringAnsi(errorCode));
                    return false;
                }
            }
            
            exception = null;
            return true;
        }

		/// <summary>
		/// Open a given library and get its handle.
		/// </summary>
		/// <param name="name">The library name.</param>
		/// <param name="skipMapping">Whether to skip using the mapping or not.</param>
		/// <param name="flags">Any optional platform-specific flags.</param>
		/// <returns>The library handle.</returns>
		public static IntPtr OpenLibrary(string name, bool skipMapping = false, int? flags = null)
		{
			if (!InternalTryOpenLibrary(name, out var libraryPtr, skipMapping, flags))
				throw new DllNotFoundException($"Unable to load library '{name}'");

			if (!CheckError(out var exception))
				throw exception;

			return libraryPtr;
		}

        /// <summary>
        /// Try to open a given library and get its handle.
        /// </summary>
        /// <param name="name">The library name.</param>
		/// <param name="libraryPtr">The library handle, or null if it failed loading.</param>
        /// <param name="skipMapping">Whether to skip using the mapping or not.</param>
        /// <param name="flags">Any optional platform-specific flags.</param>
        /// <returns>True if the handle was obtained, false otherwise.</returns>
        public static bool TryOpenLibrary(string name, out IntPtr libraryPtr, bool skipMapping = false, int? flags = null)
		{
			if (!InternalTryOpenLibrary(name, out libraryPtr, skipMapping, flags))
				return false;

			if (!CheckError(out _))
				return false;

			return true;
		}

        private static bool InternalTryOpenLibrary(string name, out IntPtr libraryPtr, bool skipMapping, int? flags)
        {
            if (name != null && !skipMapping && Mappings.TryGetValue(name, out List<DynDllMapping> mappingList))
            {
				foreach (var mapping in mappingList)
				{
					if (InternalTryOpenLibrary(mapping.LibraryName, out libraryPtr, true, mapping.Flags))
						return true;
				}

				libraryPtr = IntPtr.Zero;
				return true;
			}

            if (PlatformHelper.Is(Platform.Windows))
			{
				libraryPtr = name == null
					? GetModuleHandle(name)
					: LoadLibrary(name);
			}
            else
            {
                int _flags = flags ?? (DlopenFlags.RTLD_NOW | DlopenFlags.RTLD_GLOBAL); // Default should match LoadLibrary.

				libraryPtr = dlopen(name, _flags);

                if (libraryPtr == IntPtr.Zero && File.Exists(name))
					libraryPtr = dlopen(Path.GetFullPath(name), _flags);
            }

			return libraryPtr != IntPtr.Zero;
		}

        /// <summary>
        /// Release a library handle obtained via OpenLibrary. Don't release the result of OpenLibrary(null)!
        /// </summary>
        /// <param name="lib">The library handle.</param>
        public static bool CloseLibrary(IntPtr lib)
        {
			if (PlatformHelper.Is(Platform.Windows))
				CloseLibrary(lib);
			else
				dlclose(lib);

			return CheckError(out _);
        }

        /// <summary>
        /// Get a function pointer for a function in the given library.
        /// </summary>
        /// <param name="libraryPtr">The library handle.</param>
        /// <param name="name">The function name.</param>
        /// <returns>The function pointer.</returns>
        public static IntPtr GetFunction(this IntPtr libraryPtr, string name)
		{
			if (!InternalTryGetFunction(libraryPtr, name, out var functionPtr))
				throw new MissingMethodException($"Unable to load function '{name}'");

			if (!CheckError(out var exception))
				throw exception;

            return functionPtr;
		}

        /// <summary>
        /// Get a function pointer for a function in the given library.
        /// </summary>
        /// <param name="libraryPtr">The library handle.</param>
        /// <param name="name">The function name.</param>
        /// <param name="functionPtr">The function pointer, or null if it wasn't found.</param>
        /// <returns>True if the function pointer was obtained, false otherwise.</returns>
        public static bool TryGetFunction(this IntPtr libraryPtr, string name, out IntPtr functionPtr)
		{
			if (!InternalTryGetFunction(libraryPtr, name, out functionPtr))
				return false;

			if (!CheckError(out _))
				return false;

			return true;
        }

        private static bool InternalTryGetFunction(IntPtr libraryPtr, string name, out IntPtr functionPtr)
        {
            if (libraryPtr == IntPtr.Zero)
                throw new ArgumentNullException(nameof(libraryPtr));

            functionPtr = PlatformHelper.Is(Platform.Windows)
				? GetProcAddress(libraryPtr, name)
				: dlsym(libraryPtr, name);

			return functionPtr != IntPtr.Zero;
		}

        /// <summary>
        /// Extension method wrapping Marshal.GetDelegateForFunctionPointer
        /// </summary>
        public static T AsDelegate<T>(this IntPtr s) where T : class
        {
#pragma warning disable CS0618 // Type or member is obsolete
            return Marshal.GetDelegateForFunctionPointer(s, typeof(T)) as T;
#pragma warning restore CS0618 // Type or member is obsolete
        }

        /// <summary>
        /// Fill all static delegate fields with the DynDllImport attribute.
        /// Call this early on in the static constructor.
        /// </summary>
        /// <param name="type">The type containing the DynDllImport delegate fields.</param>
        /// <param name="mappings">Any optional mappings similar to the static mappings.</param>
        public static void ResolveDynDllImports(this Type type, Dictionary<string, List<DynDllMapping>> mappings = null)
			=> InternalResolveDynDllImports(type, null, mappings);

        /// <summary>
        /// Fill all instance delegate fields with the DynDllImport attribute.
        /// Call this early on in the constructor.
        /// </summary>
        /// <param name="instance">An instance of a type containing the DynDllImport delegate fields.</param>
        /// <param name="mappings">Any optional mappings similar to the static mappings.</param>
        public static void ResolveDynDllImports(object instance, Dictionary<string, List<DynDllMapping>> mappings = null)
			=> InternalResolveDynDllImports(instance.GetType(), instance, mappings);

        private static void InternalResolveDynDllImports(Type type, object instance, Dictionary<string, List<DynDllMapping>> mappings)
        {
            BindingFlags fieldFlags = BindingFlags.Public | BindingFlags.NonPublic;
            if (instance == null)
                fieldFlags |= BindingFlags.Static;
            else
                fieldFlags |= BindingFlags.Instance;

            foreach (FieldInfo field in type.GetFields(fieldFlags))
            {
                bool found = true;

                foreach (DynDllImportAttribute attrib in field.GetCustomAttributes(typeof(DynDllImportAttribute), true))
                {
                    found = false;

					IntPtr libraryPtr = IntPtr.Zero;

                    if (mappings != null && mappings.TryGetValue(attrib.LibraryName, out List<DynDllMapping> mappingList))
					{
						bool mappingFound = false;

						foreach (var mapping in mappingList)
						{
							if (TryOpenLibrary(mapping.LibraryName, out libraryPtr, true, mapping.Flags))
							{
								mappingFound = true;
								break;
							}
						}

						if (!mappingFound)
							continue;
					}
					else
					{
						if (!TryOpenLibrary(attrib.LibraryName, out libraryPtr))
							continue;
                    }


                    foreach (string entryPoint in attrib.EntryPoints.Concat(new[] { field.Name, field.FieldType.Name }))
                    {
                        if (!libraryPtr.TryGetFunction(entryPoint, out IntPtr functionPtr))
                            continue;

#pragma warning disable CS0618 // Type or member is obsolete
                        field.SetValue(instance, Marshal.GetDelegateForFunctionPointer(functionPtr, field.FieldType));
#pragma warning restore CS0618 // Type or member is obsolete

                        found = true;
                        break;
                    }

                    if (found)
                        break;
                }

                if (!found)
                    throw new EntryPointNotFoundException($"No matching entry point found for {field.Name} in {field.DeclaringType.FullName}");
            }
        }

		public static class DlopenFlags
		{
			public const int RTLD_LAZY = 0x0001;
			public const int RTLD_NOW = 0x0002;
			public const int RTLD_LOCAL = 0x0000;
			public const int RTLD_GLOBAL = 0x0100;
		}
    }

    /// <summary>
    /// Similar to DllImport, but requires you to run typeof(DeclaringType).ResolveDynDllImports();
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class DynDllImportAttribute : Attribute
    {
        /// <summary>
        /// The library or library alias to use.
        /// </summary>
        public string LibraryName { get; set; }

        /// <summary>
        /// A list of possible entrypoints that the function can be resolved to. Implicitly includes the field name and delegate name.
        /// </summary>
        public string[] EntryPoints { get; set; }

        /// <param name="libraryName">The library or library alias to use.</param>
        /// <param name="entryPoints">A list of possible entrypoints that the function can be resolved to. Implicitly includes the field name and delegate name.</param>
        public DynDllImportAttribute(string libraryName, params string[] entryPoints)
        {
            LibraryName = libraryName;
            EntryPoints = entryPoints;
        }
    }

    /// <summary>
    /// A mapping entry, to be used by <see cref="DynDllImportAttribute"/>.
    /// </summary>
    public sealed class DynDllMapping
    {
        /// <summary>
        /// The name as which the library will be resolved as. Useful to remap libraries or to provide full paths.
        /// </summary>
        public string LibraryName { get; set; }

        /// <summary>
        /// Platform-dependent loading flags.
        /// </summary>
        public int? Flags { get; set; }

        /// <param name="libraryName">The name as which the library will be resolved as. Useful to remap libraries or to provide full paths.</param>
        /// <param name="flags">Platform-dependent loading flags.</param>
		public DynDllMapping(string libraryName, int? flags = null)
		{
			LibraryName = libraryName ?? throw new ArgumentNullException(nameof(libraryName));
			Flags = flags;
		}

		public static implicit operator DynDllMapping(string libraryName)
		{
            return new DynDllMapping(libraryName);
		}
	}
}