using System;

namespace BepInEx.Shared
{
	/// <summary>
	/// This class is appended to AssemblyInfo.cs when BepInEx is built via a CI pipeline.
	/// It is mainly intended to signify that the current build is not a release build and is special, like for instance a bleeding edge build.
	/// </summary>
	public class BuildInfoAttribute : Attribute
	{
		public string Info { get; }

		public BuildInfoAttribute(string info)
		{
			Info = info;
		}
	}
}