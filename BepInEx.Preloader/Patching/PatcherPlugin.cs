using System;
using System.Collections.Generic;

namespace BepInEx.Preloader.Patching
{
	/// <summary>
	///     A single assembly patcher.
	/// </summary>
	internal class PatcherPlugin
	{
		/// <summary>
		///     Target assemblies to patch.
		/// </summary>
		public IEnumerable<string> TargetDLLs { get; set; } = null;

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
		///     Name of the patcher.
		/// </summary>
		public string Name { get; set; } = string.Empty;
	}
}