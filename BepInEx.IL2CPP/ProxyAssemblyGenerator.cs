using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using AssemblyUnhollower;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppDumper;
using UnhollowerBaseLib;

namespace BepInEx.IL2CPP
{
    internal static class ProxyAssemblyGenerator
    {
        private static readonly ManualLogSource Il2cppDumperLogger = Logger.CreateLogSource("Il2CppDumper");

        public static string GameAssemblyPath => Path.Combine(Paths.GameRootPath, "GameAssembly.dll");

        private static string HashPath => Path.Combine(Preloader.IL2CPPUnhollowedPath, "assembly-hash.txt");

        private static string UnityBaseLibsDirectory => Path.Combine(Paths.BepInExRootPath, "unity-libs");

        private static string tempDumperDirectory => Path.Combine(Preloader.IL2CPPUnhollowedPath, "temp");

        private static string ComputeHash()
        {
            using var md5 = MD5.Create();

            var gameAssemblyBytes = File.ReadAllBytes(GameAssemblyPath);
            md5.TransformBlock(gameAssemblyBytes, 0, gameAssemblyBytes.Length, gameAssemblyBytes, 0);

            if (Directory.Exists(UnityBaseLibsDirectory))
                foreach (var file in Directory.EnumerateFiles(UnityBaseLibsDirectory, "*.dll",
                                                              SearchOption.TopDirectoryOnly))
                {
                    var pathBytes = Encoding.UTF8.GetBytes(Path.GetFileName(file));
                    md5.TransformBlock(pathBytes, 0, pathBytes.Length, pathBytes, 0);

                    var contentBytes = File.ReadAllBytes(file);
                    md5.TransformBlock(contentBytes, 0, contentBytes.Length, contentBytes, 0);
                }

            md5.TransformFinalBlock(new byte[0], 0, 0);

            return Utility.ByteArrayToString(md5.Hash);
        }

        public static bool CheckIfGenerationRequired()
        {
            if (!Directory.Exists(Preloader.IL2CPPUnhollowedPath))
                return true;

            if (!File.Exists(HashPath))
                return true;

            if (ComputeHash() != File.ReadAllText(HashPath))
            {
                Preloader.Log.LogInfo("Detected a game update, will regenerate proxy assemblies");
                return true;
            }

            return false;
        }

        public static void GenerateAssemblies()
        {
            var domain = AppDomainHelper.CreateDomain("GeneratorDomain", new AppDomainHelper.AppDomainSetup
            {
                ApplicationBase = Paths.BepInExAssemblyDirectory
            });

            var runner =
                (AppDomainRunner) AppDomainHelper.CreateInstanceAndUnwrap(domain,
                                                                          typeof(AppDomainRunner).Assembly.FullName,
                                                                          typeof(AppDomainRunner).FullName);

            runner.Setup(Paths.ExecutablePath, Preloader.IL2CPPUnhollowedPath);
            runner.GenerateAssembliesInternal(new AppDomainListener());

            AppDomain.Unload(domain);

            Directory.Delete(tempDumperDirectory, true);

            File.WriteAllText(HashPath, ComputeHash());
        }

        private static class AppDomainHelper
        {
            static AppDomainHelper()
            {
                var appDomain = typeof(AppDomain);
                var evidenceType = typeof(AppDomain).Assembly.GetType("System.Security.Policy.Evidence");
                AppDomainSetupType = typeof(AppDomain).Assembly.GetType("System.AppDomainSetup");
                CreateDomainInternal =
                    MethodInvoker.GetHandler(AccessTools.Method(appDomain, "CreateDomain",
                                                                new[]
                                                                {
                                                                    typeof(string), evidenceType, AppDomainSetupType
                                                                }));
                CreateInstanceAndUnwrap =
                    AccessTools.MethodDelegate<Func<AppDomain, string, string, object>>(AccessTools.Method(appDomain,
                        nameof(CreateInstanceAndUnwrap), new[] { typeof(string), typeof(string) }));
            }

            public static Func<AppDomain, string, string, object> CreateInstanceAndUnwrap { get; }
            private static FastInvokeHandler CreateDomainInternal { get; }
            private static Type AppDomainSetupType { get; }

            public static AppDomain CreateDomain(string name, AppDomainSetup setup)
            {
                var realSetup = AccessTools.CreateInstance(AppDomainSetupType);
                Traverse.IterateProperties(setup, realSetup, (pSrc, pTgt) => pTgt.SetValue(pSrc.GetValue()));
                return CreateDomainInternal(null, name, null, realSetup) as AppDomain;
            }

            public class AppDomainSetup
            {
                public string ApplicationBase { get; set; }
            }
        }

        [Serializable]
        private class AppDomainListener : MarshalByRefObject
        {
            public void DoPreloaderLog(object data, LogLevel level) => Preloader.Log.Log(level, data);

            public void DoDumperLog(object data, LogLevel level) => Il2cppDumperLogger.Log(level, data);

            public void DoUnhollowerLog(object data, LogLevel level) => Preloader.UnhollowerLog.Log(level, data);
        }

        [Serializable]
        private class AppDomainRunner : MarshalByRefObject
        {
            public void Setup(string executablePath, string unhollowedPath)
            {
                Paths.SetExecutablePath(executablePath);
                Preloader.IL2CPPUnhollowedPath = unhollowedPath;
            }

            public void GenerateAssembliesInternal(AppDomainListener listener)
            {
                listener.DoPreloaderLog("Generating Il2CppUnhollower assemblies", LogLevel.Message);

                Directory.CreateDirectory(Preloader.IL2CPPUnhollowedPath);

                foreach (var dllFile in Directory.EnumerateFiles(Preloader.IL2CPPUnhollowedPath, "*.dll",
                                                                 SearchOption.TopDirectoryOnly))
                    File.Delete(dllFile);

                var tempDumperDirectory = Path.Combine(Preloader.IL2CPPUnhollowedPath, "temp");
                Directory.CreateDirectory(tempDumperDirectory);


                var dumperConfig = new Config
                {
                    GenerateScript = false,
                    GenerateDummyDll = true
                };

                listener.DoPreloaderLog("Generating Il2CppDumper intermediate assemblies", LogLevel.Info);

                Il2CppDumper.Il2CppDumper.PerformDump(GameAssemblyPath,
                                                      Path.Combine(Paths.GameRootPath, $"{Paths.ProcessName}_Data",
                                                                   "il2cpp_data", "Metadata", "global-metadata.dat"),
                                                      tempDumperDirectory,
                                                      dumperConfig,
                                                      s => listener.DoDumperLog(s, LogLevel.Debug));


                listener.DoPreloaderLog("Executing Il2CppUnhollower generator", LogLevel.Info);

                LogSupport.InfoHandler += s => listener.DoUnhollowerLog(s, LogLevel.Info);
                LogSupport.WarningHandler += s => listener.DoUnhollowerLog(s, LogLevel.Warning);
                LogSupport.TraceHandler += s => listener.DoUnhollowerLog(s, LogLevel.Debug);
                LogSupport.ErrorHandler += s => listener.DoUnhollowerLog(s, LogLevel.Error);

                var unhollowerOptions = new UnhollowerOptions
                {
                    GameAssemblyPath = GameAssemblyPath,
                    MscorlibPath = Path.Combine(Paths.GameRootPath, "mono", "Managed", "mscorlib.dll"),
                    SourceDir = Path.Combine(tempDumperDirectory, "DummyDll"),
                    OutputDir = Preloader.IL2CPPUnhollowedPath,
                    UnityBaseLibsDir = Directory.Exists(UnityBaseLibsDirectory) ? UnityBaseLibsDirectory : null,
                    NoCopyUnhollowerLibs = true
                };

                Program.Main(unhollowerOptions);
            }
        }
    }
}
