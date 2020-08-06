using System;
using System.Collections.Generic;
using System.IO;
using BepInEx.Bootstrap;

namespace BepInEx.Preloader.Patching
{
	/// <summary>
	///     A single assembly patcher.
	/// </summary>
	public class PatcherPlugin : ICacheable
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

		/// <inheritdoc />
		public void Save(BinaryWriter bw)
		{
			bw.Write(TypeName);
		}

		/// <inheritdoc />
		public void Load(BinaryReader br)
		{
			TypeName = br.ReadString();
		}
	}
}