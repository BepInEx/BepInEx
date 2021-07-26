using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using AssemblyUnhollower;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Il2CppDumper;
using UnhollowerBaseLib;

namespace BepInEx.IL2CPP
{
    internal static class ProxyAssemblyGenerator
    {
        private static readonly ConfigEntry<string> ConfigUnityBaseLibrariesSource = ConfigFile.CoreConfig.Bind(
         "IL2CPP", "UnityBaseLibrariesSource",
         "http://unity.bepinex.dev/libraries/{VERSION}.zip",
         new StringBuilder()
             .AppendLine("URL to the ZIP of managed Unity base libraries.")
             .AppendLine("The base libraries are used by Il2CppUnhollower to generate unhollowed Unity assemblies")
             .AppendLine("The URL template MUST use HTTP.")
             .AppendLine("The URL can include {VERSION} template which will be replaced with the game's Unity engine version")
             .ToString());

        private static readonly ManualLogSource Il2cppDumperLogger = Logger.CreateLogSource("Il2CppDumper");

        public static string GameAssemblyPath => Path.Combine(Paths.GameRootPath, "GameAssembly.dll");

        private static string HashPath => Path.Combine(Preloader.IL2CPPUnhollowedPath, "assembly-hash.txt");

        private static string UnityBaseLibsDirectory => Path.Combine(Paths.BepInExRootPath, "unity-libs");

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

            runner.Setup(Paths.ExecutablePath, Preloader.IL2CPPUnhollowedPath, Paths.BepInExRootPath,
                         Paths.ManagedPath);
            runner.GenerateAssembliesInternal(new AppDomainListener(), Preloader.UnityVersion.ToString(3));

            AppDomain.Unload(domain);

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
            public void Setup(string executablePath, string unhollowedPath, string bepinPath, string managedPath)
            {
                Paths.SetExecutablePath(executablePath, bepinPath, managedPath);
                Preloader.IL2CPPUnhollowedPath = unhollowedPath;
                AppDomain.CurrentDomain.AddCecilPlatformAssemblies(Paths.ManagedPath);
                AppDomain.CurrentDomain.AddCecilPlatformAssemblies(UnityBaseLibsDirectory);
            }

            public void GenerateAssembliesInternal(AppDomainListener listener, string unityVersion)
            {
                var source =
                    ConfigUnityBaseLibrariesSource.Value.Replace("{VERSION}", unityVersion);

                if (!string.IsNullOrEmpty(source))
                {
                    listener.DoPreloaderLog("Downloading unity base libraries", LogLevel.Message);

                    Directory.CreateDirectory(UnityBaseLibsDirectory);
                    Directory.EnumerateFiles(UnityBaseLibsDirectory, "*.dll").Do(File.Delete);

                    using var httpClient = new HttpClient();
                    using var zipStream = httpClient.GetStreamAsync(source).GetAwaiter().GetResult();
                    using var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read);

                    listener.DoPreloaderLog("Extracting downloaded unity base libraries", LogLevel.Message);
                    zipArchive.ExtractToDirectory(UnityBaseLibsDirectory);
                }

                listener.DoPreloaderLog("Generating Il2CppUnhollower assemblies", LogLevel.Message);

                Directory.CreateDirectory(Preloader.IL2CPPUnhollowedPath);
                Directory.EnumerateFiles(Preloader.IL2CPPUnhollowedPath, "*.dll").Do(File.Delete);

                listener.DoPreloaderLog("Generating Il2CppDumper intermediate assemblies", LogLevel.Info);

                Il2CppDumper.Il2CppDumper.Init(GameAssemblyPath,
                                               Path.Combine(Paths.GameRootPath,
                                                            $"{Paths.ProcessName}_Data",
                                                            "il2cpp_data",
                                                            "Metadata",
                                                            "global-metadata.dat"),
                                               new Config
                                               {
                                                   GenerateStruct = false,
                                                   GenerateDummyDll = true
                                               },
                                               s => listener.DoDumperLog(s, LogLevel.Debug),
                                               out var metadata,
                                               out var il2Cpp);

                var executor = new Il2CppExecutor(metadata, il2Cpp);
                var dummy = new DummyAssemblyGenerator(executor, true);
                
                var unhollowerOptions = new UnhollowerOptions
                {
                    GameAssemblyPath = GameAssemblyPath,
                    MscorlibPath = Path.Combine(Paths.ManagedPath, "mscorlib.dll"),
                    Source = dummy.Assemblies,
                    OutputDir = Preloader.IL2CPPUnhollowedPath,
                    UnityBaseLibsDir = Directory.Exists(UnityBaseLibsDirectory) ? UnityBaseLibsDirectory : null,
                    NoCopyUnhollowerLibs = true
                };

                string renameMapLocation = Path.Combine(Paths.BepInExRootPath, "DeobfuscationMap.csv.gz");
                if (File.Exists(renameMapLocation))
                {
                    listener.DoPreloaderLog("Parsing deobfuscation rename mappings", LogLevel.Info);

                    using var fileStream = new FileStream(renameMapLocation, FileMode.Open, FileAccess.Read, FileShare.Read);
                    using var gzipStream = new GZipStream(fileStream, CompressionMode.Decompress);

                    using var reader = new StreamReader(gzipStream, Encoding.UTF8, false);
                    while (!reader.EndOfStream)
                    {
                        var line = reader.ReadLine();

                        if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#"))
                            continue;

                        var mapping = line.Split(';');

                        if (mapping.Length != 2)
                            continue;

                        unhollowerOptions.RenameMap[mapping[0]] = mapping[1];
                    }
                }
                

                listener.DoPreloaderLog("Executing Il2CppUnhollower generator", LogLevel.Info);

                LogSupport.InfoHandler += s => listener.DoUnhollowerLog(s, LogLevel.Info);
                LogSupport.WarningHandler += s => listener.DoUnhollowerLog(s, LogLevel.Warning);
                LogSupport.TraceHandler += s => listener.DoUnhollowerLog(s, LogLevel.Debug);
                LogSupport.ErrorHandler += s => listener.DoUnhollowerLog(s, LogLevel.Error);

                try
                {
                    Program.Main(unhollowerOptions);
                }
                catch (Exception e)
                {
                    listener.DoUnhollowerLog($"Exception while unhollowing: {e}", LogLevel.Error);
                }

                dummy.Assemblies.Do(x => x.Dispose());
            }
        }
    }
}
