using System.Reflection;
// ReSharper disable once RedundantUsingDirective
using BepInEx.Shared;

[assembly:AssemblyCopyright("Copyright © 2020 BepInEx Team")]
[assembly: AssemblyVersion(VersionInfo.VERSION)]
[assembly: AssemblyFileVersion(VersionInfo.VERSION)]
[assembly: AssemblyInformationalVersion(VersionInfo.VERSION)]

internal static class VersionInfo
{
	public const string VERSION = "6.0.0.0";
}