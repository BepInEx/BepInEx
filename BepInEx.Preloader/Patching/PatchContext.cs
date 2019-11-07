using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using BepInEx.Bootstrap;
using Mono.Cecil;

namespace BepInEx.Preloader.Patching
{
	public class PatchContext
	{
		public ReadOnlyCollection<PatcherDefinition> LoadedPatchers { get; internal set; }

		/// <summary>
		/// A read-only list of legacy patchers that are loaded. Will be removed in a future release.
		/// </summary>
		[Obsolete("Will be removed in a future release.", false)]
		public IList<Type> LoadedLegacyPatchers { get; internal set; }

		public IList<AssemblyDefinition> AvailableAssemblies { get; internal set; }

		public Dictionary<string, AssemblyDefinition> AssemblyLocations { get; internal set; }

		internal PatchContext() { }
	}

	/// <summary>
	/// A preloader assembly patcher.
	/// </summary>
	public class PatcherDefinition
	{
		public PatcherInfo PatcherInfo { get; internal set; }

		public string TypeName { get; internal set; }
		public BasePatcher Instance { get; internal set; }

		public Dictionary<TargetAssemblyAttribute, MethodInfo> AssemblyDefinitionPatchers { get; internal set; } = new Dictionary<TargetAssemblyAttribute, MethodInfo>();

		public Dictionary<TargetClassAttribute, MethodInfo> TypeDefinitionPatchers { get; internal set; } = new Dictionary<TargetClassAttribute, MethodInfo>();

		internal PatcherDefinition() { }
	}

	/// <summary>
	///     A single assembly patcher, based on the old reflection contract.
	/// </summary>
	internal class LegacyPatcherPlugin : ICacheable
	{
		/// <summary>
		///     Target assemblies to patch.
		/// </summary>
		public Func<IEnumerable<string>> TargetDLLs { get; set; } = null;

		/// <summary>
		///     Initializer method that is run before any patching occurs.
		/// </summary>
		public Action Initializer { get; set; } = null;

		/// <summary>
		///     Finalizer method that is run after all patching is done.
		/// </summary>
		public Action Finalizer { get; set; } = null;

		/// <summary>
		///     The main patcher method that is called on every DLL defined in <see cref="TargetDLLs" />.
		/// </summary>
		public AssemblyPatcherDelegate Patcher { get; set; } = null;

		/// <summary>
		///     Type name of the patcher.
		/// </summary>
		public string TypeName { get; set; } = string.Empty;

		public void Save(BinaryWriter bw)
		{
			bw.Write(TypeName);
		}

		public void Load(BinaryReader br)
		{
			TypeName = br.ReadString();
		}
	}
}
