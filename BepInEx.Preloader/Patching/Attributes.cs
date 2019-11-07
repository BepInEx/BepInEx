using System;

namespace BepInEx.Preloader
{
	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
	public class TargetAssemblyAttribute : Attribute
	{
		public string AssemblyName { get; }

		public TargetAssemblyAttribute(string assemblyName)
		{
			AssemblyName = assemblyName;
		}
	}

	[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
	public class TargetClassAttribute : Attribute
	{
		public string AssemblyName { get; }
		public string ClassName { get; }

		public TargetClassAttribute(string assemblyName, string className)
		{
			AssemblyName = assemblyName;
			ClassName = className;
		}
	}

	[AttributeUsage(AttributeTargets.Class)]
	public class PatcherInfo : Attribute
	{
		public string GUID { get; }
		public string Name { get; }
		public Version Version { get; }

		public PatcherInfo(string guid, string name, string version)
		{
			GUID = guid;
			Name = name;
			Version = new Version(version);
		}
	}
}