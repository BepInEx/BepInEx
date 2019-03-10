namespace BepInEx.Installer.Patching
{
	public enum InstallationType
	{
		Doorstop,
		CryptoRng,
		AssemblyPatch
	}

	public enum RuntimePatchesType
	{
		EnabledHarmony,
		EnabledManual,
		Disabled
	}
}