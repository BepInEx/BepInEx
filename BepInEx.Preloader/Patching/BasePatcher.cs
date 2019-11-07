using System.Collections.Generic;
using BepInEx.Preloader.Patching;
using Mono.Cecil;

namespace BepInEx.Preloader
{
	public abstract class BasePatcher
	{
		private PatchContext _patchContext;
		protected PatchContext PatchContext => _patchContext;

		public virtual void Setup(PatchContext patchContext)
		{
			_patchContext = patchContext;
		}

		public virtual void PatchAll(IList<AssemblyDefinition> assemblyDefinitions)
		{

		}

		public virtual void Finalize()
		{

		}

		[TargetAssembly(assemblyName: "Terraria")]
		public void Patch(AssemblyDefinition assemblyDefinition)
		{

		}
	}
}
