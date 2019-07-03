using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using Mono.Cecil;

namespace BepInEx.Bootstrap
{
	/// <summary>
	/// Provides methods for loading specified types from an assembly.
	/// </summary>
	public static class TypeLoader
	{
		private static DefaultAssemblyResolver resolver;
		private static ReaderParameters readerParameters;

		public static event AssemblyResolveEventHandler AssemblyResolve;

		static TypeLoader()
		{
			resolver = new DefaultAssemblyResolver();
			readerParameters = new ReaderParameters { AssemblyResolver = resolver };

			resolver.ResolveFailure += (sender, reference) =>
			{
				var name = new AssemblyName(reference.FullName);

				if (Utility.TryResolveDllAssembly(name, Paths.BepInExAssemblyDirectory, readerParameters, out AssemblyDefinition assembly) ||
					Utility.TryResolveDllAssembly(name, Paths.PluginPath, readerParameters, out assembly) ||
					Utility.TryResolveDllAssembly(name, Paths.ManagedPath, readerParameters, out assembly))
					return assembly;

				return AssemblyResolve?.Invoke(sender, reference);
			};
		}

		/// <summary>
		/// Loads a list of types from a directory containing assemblies, that derive from a base type.
		/// </summary>
		/// <typeparam name="T">The specific base type to search for.</typeparam>
		/// <param name="directory">The directory to search for assemblies.</param>
		/// <returns>Returns a list of found derivative types.</returns>
		public static Dictionary<AssemblyDefinition, List<T>> FindPluginTypes<T>(string directory, Func<TypeDefinition, T> typeSelector, Func<AssemblyDefinition, bool> assemblyFilter = null) where T : class
		{
			var result = new Dictionary<AssemblyDefinition, List<T>>();

			foreach (string dll in Directory.GetFiles(Path.GetFullPath(directory), "*.dll", SearchOption.AllDirectories))
			{
				try
				{
					var ass = AssemblyDefinition.ReadAssembly(dll, readerParameters);

					if (!assemblyFilter?.Invoke(ass) ?? false)
					{
						ass.Dispose();
						continue;
					}

					var matches = ass.MainModule.Types.Select(typeSelector).Where(t => t != null).ToList();

					if (matches.Count == 0)
					{
						ass.Dispose();
						continue;
					}

					result[ass] = matches;
				}
				catch (Exception e)
				{
					Logger.LogError(e.ToString());
				}
			}

			return result;
		}

		public static string TypeLoadExceptionToString(ReflectionTypeLoadException ex)
		{
			StringBuilder sb = new StringBuilder();
			foreach (Exception exSub in ex.LoaderExceptions)
			{
				sb.AppendLine(exSub.Message);
				if (exSub is FileNotFoundException exFileNotFound)
				{
					if (!string.IsNullOrEmpty(exFileNotFound.FusionLog))
					{
						sb.AppendLine("Fusion Log:");
						sb.AppendLine(exFileNotFound.FusionLog);
					}
				}
				else if (exSub is FileLoadException exLoad)
				{
					if (!string.IsNullOrEmpty(exLoad.FusionLog))
					{
						sb.AppendLine("Fusion Log:");
						sb.AppendLine(exLoad.FusionLog);
					}
				}

				sb.AppendLine();
			}

			return sb.ToString();
		}
	}
}