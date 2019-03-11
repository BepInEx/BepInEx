using BepInEx.Installer.Patching;

namespace BepInEx.Installer
{
	public class Config
	{
		private const string ConfigTemplate =
@"[BepInEx]
console=false
console-shiftjis=false
preloader-logconsole=false
logger-displayed-levels=Info
chainloader-log-unity-messages=true
chainloader-plugins-directory=plugins

[Preloader]
entrypoint-assembly=UnityEngine.dll
entrypoint-type=Application
entrypoint-method=.cctor
dump-assemblies=false";

		public string OutputConfig { get; protected set; }

		public string TargetAssembly { get; protected set; }
		public string TargetType { get; protected set; }
		public string TargetMethod { get; protected set; }

		public RuntimePatchesType RuntimePatchesType { get; protected set; }

		public Config(string targetAssembly, string targetType, string targetMethod, RuntimePatchesType runtimePatchesType)
		{
			TargetAssembly = targetAssembly;
			TargetType = targetType;
			TargetMethod = targetMethod;
			RuntimePatchesType = runtimePatchesType;

			OutputConfig = ConfigTemplate
						   .Replace("entrypoint-assembly=UnityEngine.dll", $"entrypoint-assembly={targetAssembly}")
						   .Replace("entrypoint-type=Application", $"entrypoint-type={targetType}")
						   .Replace("entrypoint-method=.cctor", $"entrypoint-method={targetMethod}");

			//config option for runtime patches is not implemented yet
		}
	}
}