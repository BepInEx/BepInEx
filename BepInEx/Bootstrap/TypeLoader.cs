using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using BepInEx.Contract;
using BepInEx.Logging;
using Mono.Cecil;

namespace BepInEx.Bootstrap
{
	/// <summary>
	/// Provides methods for loading specified types from an assembly.
	/// </summary>
	public static class TypeLoader
	{
		private static bool Is(this TypeDefinition self, Type td)
		{
			if (self.FullName == td.FullName)
				return true;
			return self.FullName != "System.Object" && (self.BaseType?.Resolve().Is(td) ?? false);
		}

		private static DefaultAssemblyResolver resolver;
		private static ReaderParameters readerParameters;

		static TypeLoader()
		{
			resolver = new DefaultAssemblyResolver();
			readerParameters = new ReaderParameters { AssemblyResolver = resolver };

			resolver.ResolveFailure += (sender, reference) =>
			{
				var name = new AssemblyName(reference.FullName);

				if (Utility.TryResolveDllAssembly(name, Paths.BepInExAssemblyDirectory, readerParameters, out AssemblyDefinition assembly) || Utility.TryResolveDllAssembly(name, Paths.PluginPath, readerParameters, out assembly) || Utility.TryResolveDllAssembly(name, Paths.ManagedPath, readerParameters, out assembly))
					return assembly;

				return null;
			};
		}

		/// <summary>
		/// Loads a list of types from a directory containing assemblies, that derive from a base type.
		/// </summary>
		/// <typeparam name="T">The specific base type to search for.</typeparam>
		/// <param name="directory">The directory to search for assemblies.</param>
		/// <returns>Returns a list of found derivative types.</returns>
		public static Dictionary<AssemblyDefinition, IEnumerable<PluginInfo>> FindPluginTypes(string directory)
		{
			var result = new Dictionary<AssemblyDefinition, IEnumerable<PluginInfo>>();
			var pluginType = typeof(BaseUnityPlugin);
			string currentProcess = Process.GetCurrentProcess().ProcessName.ToLower();

			foreach (string dll in Directory.GetFiles(Path.GetFullPath(directory), "*.dll", SearchOption.AllDirectories))
			{
				try
				{
					var ass = AssemblyDefinition.ReadAssembly(dll, readerParameters);

					var matchingTypes = ass.MainModule.Types.Where(t => !t.IsInterface && !t.IsAbstract && t.Is(pluginType)).ToList();

					if (matchingTypes.Count == 0)
						continue;

					var pluginInfos = new List<PluginInfo>();

					foreach (var pluginTypeDefinition in matchingTypes)
					{
						var metadata = BepInPlugin.FromCecilType(pluginTypeDefinition);

						if (metadata == null)
						{
							Logger.LogWarning($"Skipping over type [{pluginTypeDefinition.Name}] as no metadata attribute is specified");
							continue;
						}

						//Perform a filter for currently running process
						var filters = BepInProcess.FromCecilType(pluginTypeDefinition);

						bool invalidProcessName = filters.Any(x => x.ProcessName.ToLower().Replace(".exe", "") == currentProcess);

						if (invalidProcessName)
						{
							Logger.LogInfo($"Skipping over plugin [{metadata.GUID}] due to process filter");
							continue;
						}

						var dependencies = BepInDependency.FromCecilType(pluginTypeDefinition);

						pluginInfos.Add(new PluginInfo
						{
							Metadata = metadata,
							Processes = filters,
							Dependencies = dependencies,
							CecilType = pluginTypeDefinition,
							Location = dll
						});
					}

					result[ass] = pluginInfos;
				}
				catch (Exception e)
				{
					Logger.LogError(e.ToString());
				}
			}

			return result;
		}

		private static string TypeLoadExceptionToString(ReflectionTypeLoadException ex)
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