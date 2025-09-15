#pragma warning disable CS1591
// ReSharper disable ClassNeverInstantiated.Global
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.DotNet.NuGet.Push;
using Cake.Common.Xml;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;
using Cake.Git;
using Cake.Json;
using Microsoft.Build.Definition;
using Microsoft.Build.Evaluation;

public static class Program
{
    public static int Main(string[] args)
    {
        return new CakeHost()
               .UseContext<BuildContext>()
               .Run(args);
    }
}

public class BuildContext : FrostingContext
{
    public enum ProjectBuildType
    {
        Release,
        Development,
        BleedingEdge
    }

    public const string DOORSTOP_VERSION = "4.3.0";
    public const string DOTNET_RUNTIME_VERSION = "6.0.7";
    public const string DOBBY_VERSION = "1.0.5";
    public const string HOOKFXR_VERSION = "1.1.0";
    public const string DOTNET_RUNTIME_ZIP_URL =
        $"https://github.com/BepInEx/dotnet-runtime/releases/download/{DOTNET_RUNTIME_VERSION}/mini-coreclr-Release.zip";

    internal readonly DistributionTarget[] Distributions =
    {
        //new("Unity.Mono", "win-x86"),
        //new("Unity.Mono", "win-x64"),
        //new("Unity.Mono", "linux-x86"),
        //new("Unity.Mono", "linux-x64"),
        //new("Unity.Mono", "macos-x64"),
        //new("Unity.IL2CPP", "win-x86"),
        //new("Unity.IL2CPP", "win-x64"),
        //new("Unity.IL2CPP", "linux-x64"),
        //new("Unity.IL2CPP", "macos-x64"),
        //new("NET.Framework", "win-x86", "net40"),
        //new("NET.Framework", "win-x86", "net452"),
        //new("NET.CoreCLR", "win-x64", "netcoreapp3.1"),
        //new("NET.CoreCLR", "win-x64", "net9.0"),
        new("NET", "BepisLoader", "win-x64", "net9.0"),
        // new("NET", "BepisLoader", "linux-x64", "net9.0")
    };


    public BuildContext(ICakeContext ctx)
        : base(ctx)
    {
        RootDirectory = ctx.Environment.WorkingDirectory.GetParent();
        OutputDirectory = RootDirectory.Combine("bin");
        CacheDirectory = OutputDirectory.Combine(".dep_cache");
        DistributionDirectory = OutputDirectory.Combine("dist");
        var props = Project.FromFile(RootDirectory.CombineWithFilePath("Directory.Build.props").FullPath,
                                     new ProjectOptions());
        VersionPrefix = props.GetPropertyValue("VersionPrefix");
        CurrentCommit = ctx.GitLogTip(RootDirectory);

        BuildType = ctx.Argument("build-type", ProjectBuildType.Development);
        BuildId = ctx.Argument("build-id", -1);
        LastBuildCommit = ctx.Argument("last-build-commit", "");
        NugetApiKey = ctx.Argument("nuget-api-key", "");
        NugetSource = ctx.Argument("nuget-source", "https://nuget.bepinex.dev/v3/index.json");
    }

    public ProjectBuildType BuildType { get; }
    public int BuildId { get; }
    public string LastBuildCommit { get; }
    public string NugetApiKey { get; }
    public string NugetSource { get; }

    public DirectoryPath RootDirectory { get; }
    public DirectoryPath OutputDirectory { get; }
    public DirectoryPath CacheDirectory { get; }
    public DirectoryPath DistributionDirectory { get; }

    public string VersionPrefix { get; }
    public GitCommit CurrentCommit { get; }

    public string VersionSuffix => BuildType switch
    {
        ProjectBuildType.Release => "",
        ProjectBuildType.Development => "dev",
        ProjectBuildType.BleedingEdge => $"be.{BuildId}",
        var _ => throw new ArgumentOutOfRangeException()
    };

    public string BuildPackageVersion =>
        VersionPrefix + BuildType switch
        {
            ProjectBuildType.Release => "",
            var _ => $"-{VersionSuffix}+{this.GitShortenSha(RootDirectory, CurrentCommit)}",
        };

    public static string DoorstopZipUrl(string arch) =>
        $"https://github.com/NeighTools/UnityDoorstop/releases/download/v{DOORSTOP_VERSION}/doorstop_{arch}_release_{DOORSTOP_VERSION}.zip";

    public static string DobbyZipUrl(string arch) =>
        $"https://github.com/BepInEx/Dobby/releases/download/v{DOBBY_VERSION}/dobby-{arch}.zip";

    public static string HookfxrZipUrl = $"https://github.com/ResoniteModding/hookfxr/releases/download/v{HOOKFXR_VERSION}/hookfxr-Release.zip";
}

[TaskName("Clean")]
public sealed class CleanTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext ctx)
    {
        ctx.CreateDirectory(ctx.OutputDirectory);
        ctx.CleanDirectory(ctx.OutputDirectory,
                           f => !f.Path.FullPath.Contains(".dep_cache"));

        ctx.Log.Information("Cleaning up old build objects");
        ctx.CleanDirectories(ctx.RootDirectory.Combine("**/BepInEx.*/**/bin").FullPath);
        ctx.CleanDirectories(ctx.RootDirectory.Combine("**/BepInEx.*/**/obj").FullPath);
    }
}

[TaskName("RestoreTools")]
[IsDependentOn(typeof(CleanTask))]
public sealed class RestoreToolsTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext ctx)
    {
        ctx.Log.Information("Restoring dotnet tools...");

        var settings = new Cake.Common.Tools.DotNet.Tool.DotNetToolSettings
        {
            WorkingDirectory = ctx.RootDirectory
        };

        ctx.DotNetTool("tool restore", settings);

        ctx.Log.Information("Dotnet tools restored successfully.");
    }
}

[TaskName("Compile")]
[IsDependentOn(typeof(RestoreToolsTask))]
public sealed class CompileTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext ctx)
    {
        var hasBepisLoader = ctx.Distributions.Any(d => d.Runtime == "BepisLoader");

        var buildSettings = new DotNetBuildSettings
        {
            Configuration = "Release"
        };
        if (ctx.BuildType != BuildContext.ProjectBuildType.Release)
        {
            buildSettings.MSBuildSettings = new()
            {
                VersionSuffix = ctx.VersionSuffix,
                Properties =
                {
                    ["SourceRevisionId"] = new[] { ctx.CurrentCommit.Sha },
                    ["RepositoryBranch"] = new[] { ctx.GitBranchCurrent(ctx.RootDirectory).FriendlyName },
                    ["DebugType"] = new[] { "embedded" },
                    ["DebugSymbols"] = new[] { "true" }
                }
            };
        }

        ctx.DotNetBuild(ctx.RootDirectory.FullPath, buildSettings);

        if (hasBepisLoader)
        {
            ctx.Log.Information("Publishing BepisLoader...");

            var bepisLoaderDist = ctx.Distributions.First(d => d.Runtime == "BepisLoader");
            var publishSettings = new Cake.Common.Tools.DotNet.Publish.DotNetPublishSettings
            {
                Configuration = "Release",
                Framework = bepisLoaderDist.FrameworkTarget,
                OutputDirectory = ctx.OutputDirectory.Combine("BepisLoader").Combine(bepisLoaderDist.FrameworkTarget),
                PublishSingleFile = false,
                PublishTrimmed = false
            };

            if (ctx.BuildType != BuildContext.ProjectBuildType.Release)
            {
                publishSettings.MSBuildSettings = new()
                {
                    VersionSuffix = ctx.VersionSuffix,
                    Properties =
                    {
                        ["SourceRevisionId"] = new[] { ctx.CurrentCommit.Sha },
                        ["RepositoryBranch"] = new[] { ctx.GitBranchCurrent(ctx.RootDirectory).FriendlyName },
                        ["DebugType"] = new[] { "embedded" },
                        ["DebugSymbols"] = new[] { "true" },
                        ["GeneratePackageOnBuild"] = new[] { "false" }
                    }
                };
            }
            else
            {
                publishSettings.MSBuildSettings = new()
                {
                    Properties =
                    {
                        ["GeneratePackageOnBuild"] = new[] { "false" }
                    }
                };
            }

            ctx.DotNetPublish(ctx.RootDirectory.Combine("Runtimes/NET/BepisLoader/BepisLoader.csproj").FullPath, publishSettings);
        }
    }
}

[TaskName("DownloadDependencies")]
public sealed class DownloadDependenciesTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext ctx)
    {
        ctx.Log.Information("Downloading dependencies");
        ctx.CreateDirectory(ctx.CacheDirectory);

        var cache = new DependencyCache(ctx, ctx.CacheDirectory.CombineWithFilePath("cache.json"));

        cache.Refresh("NeighTools/UnityDoorstop", BuildContext.DOORSTOP_VERSION, () =>
        {
            ctx.Log.Information($"Downloading Doorstop {BuildContext.DOORSTOP_VERSION}");
            var doorstopDir = ctx.CacheDirectory.Combine("doorstop");
            ctx.CreateDirectory(doorstopDir);
            ctx.CleanDirectory(doorstopDir);
            var archs = new[] { "win", "linux", "macos" };
            var versions = archs
                           .Select(a => ($"Doorstop ({a})",
                                         BuildContext.DoorstopZipUrl(a),
                                         doorstopDir.Combine($"doorstop_{a}")))
                           .ToArray();
            ctx.DownloadZipFiles($"Doorstop {BuildContext.DOORSTOP_VERSION}", versions);
        });

        cache.Refresh("BepInEx/Dobby", BuildContext.DOBBY_VERSION, () =>
        {
            ctx.Log.Information($"Downloading Dobby {BuildContext.DOBBY_VERSION}");
            var dobbyDir = ctx.CacheDirectory.Combine("dobby");
            ctx.CreateDirectory(dobbyDir);
            ctx.CleanDirectory(dobbyDir);
            var archs = new[] { "win", "linux", "macos" };
            var versions = archs
                           .Select(a => ($"Dobby ({a})", BuildContext.DobbyZipUrl(a), dobbyDir.Combine($"dobby_{a}")))
                           .ToArray();
            ctx.DownloadZipFiles($"Dobby {BuildContext.DOBBY_VERSION}", versions);
        });

        cache.Refresh("BepInEx/dotnet_runtime", BuildContext.DOTNET_RUNTIME_VERSION, () =>
        {
            ctx.Log.Information($"Downloading dotnet runtime {BuildContext.DOTNET_RUNTIME_VERSION}");
            var dotnetDir = ctx.CacheDirectory.Combine("dotnet");
            ctx.CreateDirectory(dotnetDir);
            ctx.CleanDirectory(dotnetDir);
            ctx.DownloadZipFiles($"dotnet-runtime {BuildContext.DOTNET_RUNTIME_VERSION}",
                                 ("dotnet runtime", BuildContext.DOTNET_RUNTIME_ZIP_URL, dotnetDir));
        });

        cache.Refresh("ResoniteModding/hookfxr", BuildContext.HOOKFXR_VERSION, () =>
        {
            ctx.Log.Information($"Downloading hookfxr {BuildContext.HOOKFXR_VERSION}");
            var hookfxrDir = ctx.CacheDirectory.Combine("hookfxr");
            ctx.CreateDirectory(hookfxrDir);
            ctx.CleanDirectory(hookfxrDir);
            ctx.DownloadZipFiles($"hookfxr {BuildContext.HOOKFXR_VERSION}",
                                 ("hookfxr", BuildContext.HookfxrZipUrl, hookfxrDir));
        });

        cache.Save();
    }
}

[TaskName("MakeDist")]
[IsDependentOn(typeof(CompileTask))]
[IsDependentOn(typeof(DownloadDependenciesTask))]
public sealed class MakeDistTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext ctx)
    {
        ctx.CreateDirectory(ctx.DistributionDirectory);
        ctx.CleanDirectory(ctx.DistributionDirectory);

        var latestTag = ctx.Git("describe --tags --abbrev=0");
        var changelog = new StringBuilder()
                        .AppendLine(
                                    $"{ctx.Git($"rev-list --count {latestTag}..HEAD")} changes since {latestTag}")
                        .AppendLine()
                        .AppendLine("Changelog (excluding merge commits):")
                        .AppendLine(ctx.Git(
                                            $"--no-pager log --no-merges --pretty=\"format:* (%h) [%an] %s\" {latestTag}..HEAD",
                                            Environment.NewLine))
                        .ToString();


        foreach (var dist in ctx.Distributions)
        {
            ctx.Log.Information($"Creating distribution {dist.Target}");
            var targetDir = ctx.DistributionDirectory.Combine(dist.Target);
            ctx.CreateDirectory(targetDir);
            ctx.CleanDirectory(targetDir);

            var bepInExDir = targetDir.Combine("BepInEx");
            var bepInExCoreDir = bepInExDir.Combine("core");

            // Only create BepInEx directories for non-BepisLoader distributions
            if (dist.Runtime != "BepisLoader")
            {
                ctx.CreateDirectory(bepInExDir);
                ctx.CreateDirectory(bepInExCoreDir);
                ctx.CreateDirectory(bepInExDir.Combine("plugins"));
                ctx.CreateDirectory(bepInExDir.Combine("patchers"));
            }

            var sourceDirectory = ctx.OutputDirectory.Combine(dist.DistributionIdentifier);
            if (dist.FrameworkTarget != null)
                sourceDirectory = sourceDirectory.Combine(dist.FrameworkTarget);

            if (dist.Runtime != "BepisLoader")
            {
                File.WriteAllText(targetDir.CombineWithFilePath("changelog.txt").FullPath, changelog);

                foreach (var filePath in ctx.GetFiles(sourceDirectory.Combine("*.*").FullPath))
                {
                    var fileName = filePath.GetFilename().FullPath.ToLower();
                    // Skip XML documentation files
                    if (!fileName.EndsWith(".xml"))
                    {
                        ctx.CopyFileToDirectory(filePath, bepInExCoreDir);
                    }
                }
            }

            if (dist.Engine == "Unity")
            {
                var doorstopPath =
                    ctx.CacheDirectory.Combine("doorstop").Combine($"doorstop_{dist.Os}").Combine(dist.Arch);
                foreach (var filePath in ctx.GetFiles(doorstopPath.Combine($"*.{dist.DllExtension}").FullPath))
                    ctx.CopyFileToDirectory(filePath, targetDir);
                ctx.CopyFileToDirectory(doorstopPath.CombineWithFilePath(".doorstop_version"), targetDir);
                var (doorstopConfigFile, doorstopConfigDistName) = dist.Os switch
                {
                    "win" => ($"doorstop_config_{dist.Runtime.ToLower()}.ini",
                              "doorstop_config.ini"),
                    "linux" or "macos" => ($"run_bepinex_{dist.Runtime.ToLower()}.sh",
                                           "run_bepinex.sh"),
                    var _ => throw new
                                 NotSupportedException(
                                                       $"Doorstop is not supported on {dist.Os}")
                };
                ctx.CopyFile(ctx.RootDirectory.Combine("Runtimes").Combine("Unity").Combine("Doorstop").CombineWithFilePath(doorstopConfigFile),
                             targetDir.CombineWithFilePath(doorstopConfigDistName));

                if (dist.Runtime == "IL2CPP")
                {
                    ctx.CopyFile(ctx.CacheDirectory.Combine("dobby").Combine($"dobby_{dist.Os}").CombineWithFilePath($"{dist.DllPrefix}dobby_{dist.Arch}.{dist.DllExtension}"),
                                 bepInExCoreDir.CombineWithFilePath($"{dist.DllPrefix}dobby.{dist.DllExtension}"));
                    ctx.CopyDirectory(ctx.CacheDirectory.Combine("dotnet").Combine(dist.RuntimeIdentifier),
                                      targetDir.Combine("dotnet"));
                }
            }
            else if (dist.Engine == "NET")
            {
                if (dist.Runtime == "Framework")
                {
                    ctx.DeleteFile(bepInExCoreDir.CombineWithFilePath("BepInEx.NET.Framework.Launcher.exe.config"));

                    ctx.MoveFileToDirectory(bepInExCoreDir.CombineWithFilePath("BepInEx.NET.Framework.Launcher.exe"), targetDir);
                }
                else if (dist.Runtime == "CoreCLR")
                {
                    foreach (var filePath in ctx.GetFiles(bepInExCoreDir.Combine("BepInEx.NET.CoreCLR.*").FullPath))
                        ctx.MoveFileToDirectory(filePath, targetDir);
                }
                else if (dist.Runtime == "BepisLoader")
                {
                    foreach (var filePath in ctx.GetFiles(sourceDirectory.Combine("BepisLoader.*").FullPath))
                    {
                        var fileName = filePath.GetFilename().FullPath.ToLower();
                        if (!fileName.EndsWith(".xml"))
                        {
                            ctx.CopyFileToDirectory(filePath, targetDir);
                        }
                    }

                    // Copy LinuxBootstrap.sh from BepisLoader project directory
                    var linuxBootstrapPath = ctx.RootDirectory.CombineWithFilePath("Runtimes/NET/BepisLoader/LinuxBootstrap.sh");
                    if (ctx.FileExists(linuxBootstrapPath))
                    {
                        ctx.CopyFileToDirectory(linuxBootstrapPath, targetDir);
                        ctx.Log.Information("Copied LinuxBootstrap.sh to distribution");
                    }
                    else
                    {
                        ctx.Log.Warning("LinuxBootstrap.sh not found at: " + linuxBootstrapPath);
                    }

                    var netCoreCLRSource = ctx.OutputDirectory.Combine("NET.CoreCLR").Combine("net9.0");
                    if (ctx.DirectoryExists(netCoreCLRSource))
                    {
                        // Create BepInEx directories only if we have files to copy
                        ctx.CreateDirectory(bepInExDir);
                        ctx.CreateDirectory(bepInExCoreDir);

                        foreach (var filePath in ctx.GetFiles(netCoreCLRSource.Combine("*.*").FullPath))
                        {
                            var fileName = filePath.GetFilename().FullPath.ToLower();
                            ctx.CopyFileToDirectory(filePath, bepInExCoreDir);
                        }
                    }
                    else
                    {
                        ctx.Log.Warning($"NET.CoreCLR output directory not found: {netCoreCLRSource}");
                        ctx.Log.Warning("Make sure to build NET.CoreCLR target first if you want BepInEx support");
                    }

                    // Copy hookfxr files to root directory (excluding readme files and pdb files)
                    var hookfxrPath = ctx.CacheDirectory.Combine("hookfxr");
                    if (ctx.DirectoryExists(hookfxrPath))
                    {
                        foreach (var filePath in ctx.GetFiles(hookfxrPath.Combine("*.*").FullPath))
                        {
                            var fileName = filePath.GetFilename().FullPath.ToLower();
                            if (!fileName.EndsWith(".md") && !fileName.EndsWith(".pdb"))
                            {
                                ctx.CopyFileToDirectory(filePath, targetDir);
                            }
                        }

                        // Update hookfxr.ini to target BepisLoader.dll
                        var hookfxrIniPath = targetDir.CombineWithFilePath("hookfxr.ini");
                        if (ctx.FileExists(hookfxrIniPath))
                        {
                            var iniContent = System.IO.File.ReadAllText(hookfxrIniPath.FullPath);
                            iniContent = iniContent.Replace("enable=true", "enable=false");
                            iniContent = iniContent.Replace("target_assembly=MyApplication.dll", "target_assembly=BepisLoader.dll");
                            iniContent = iniContent.Replace("merge_deps_json=true", "merge_deps_json=false");
                            System.IO.File.WriteAllText(hookfxrIniPath.FullPath, iniContent);
                            ctx.Log.Information("Updated hookfxr.ini to target BepisLoader.dll");
                        }
                    }
                    else
                    {
                        ctx.Log.Warning($"hookfxr cache directory not found: {hookfxrPath}");
                    }

                    // Replace contents of BepisLoader.runtimeconfig.json with the proper framework configuration for windows
                    var runtimeConfigPath = targetDir.CombineWithFilePath("BepisLoader.runtimeconfig.json");
                    if (ctx.FileExists(runtimeConfigPath))
                    {
                        var runtimeConfig = """
                            {
                              "runtimeOptions": {
                                "tfm": "net9.0",
                                "frameworks": [
                                  {
                                    "name": "Microsoft.NETCore.App",
                                    "version": "9.0.0"
                                  },
                                  {
                                    "name": "Microsoft.WindowsDesktop.App",
                                    "version": "9.0.0"
                                  }
                                ],
                                "configProperties": {
                                  "System.Reflection.Metadata.MetadataUpdater.IsSupported": false,
                                  "System.Runtime.Serialization.EnableUnsafeBinaryFormatterSerialization": false
                                }
                              }
                            }
                            """;
                        System.IO.File.WriteAllText(runtimeConfigPath.FullPath, runtimeConfig);
                        ctx.Log.Information("Updated BepisLoader.runtimeconfig.json with proper framework configuration");
                    }
                    else
                    {
                        ctx.Log.Warning($"BepisLoader.runtimeconfig.json not found at: {runtimeConfigPath}");
                    }

                }
            }
        }
    }
}

[TaskName("PushNuGet")]
public sealed class PushNuGetTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext ctx) => !string.IsNullOrWhiteSpace(ctx.NugetApiKey) &&
                                                        ctx.BuildType != BuildContext.ProjectBuildType.Development;

    public override void Run(BuildContext ctx)
    {
        var nugetPath = ctx.OutputDirectory.Combine("NuGet");
        var settings = new DotNetNuGetPushSettings
        {
            Source = ctx.NugetSource,
            ApiKey = ctx.NugetApiKey
        };
        foreach (var pkg in ctx.GetFiles(nugetPath.Combine("*.nupkg").FullPath))
            ctx.DotNetNuGetPush(pkg, settings);
    }
}

[TaskName("BuildThunderstorePackage")]
[IsDependentOn(typeof(MakeDistTask))]
public sealed class BuildThunderstorePackageTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext ctx) => ctx.Distributions.Any(d => d.Runtime == "BepisLoader");

    public override void Run(BuildContext ctx)
    {
        ctx.Log.Information("Building Thunderstore package for BepisLoader...");

        var bepisLoaderProjectPath = ctx.RootDirectory.CombineWithFilePath("Runtimes/NET/BepisLoader/BepisLoader.csproj");

        // Use XmlPeek to read version from csproj (simpler than MSBuild Project)
        var packageVersion = ctx.XmlPeek(bepisLoaderProjectPath, "/Project/PropertyGroup/Version");

        if (string.IsNullOrEmpty(packageVersion))
        {
            throw new Exception("Could not read Version property from BepisLoader.csproj");
        }

        ctx.Log.Information($"Building Thunderstore package with version {packageVersion}...");

        var exitCode = ctx.StartProcess("dotnet", new Cake.Core.IO.ProcessSettings
        {
            Arguments = $"tcli build --package-version {packageVersion}",
            WorkingDirectory = ctx.RootDirectory.Combine("Runtimes/NET/BepisLoader")
        });

        if (exitCode != 0)
        {
            ctx.Log.Error($"dotnet tcli build failed with exit code {exitCode}");
            throw new Exception($"Thunderstore package build failed with exit code {exitCode}");
        }

        ctx.Log.Information("Thunderstore package build completed successfully.");
    }
}

[TaskName("Publish")]
[IsDependentOn(typeof(MakeDistTask))]
[IsDependentOn(typeof(PushNuGetTask))]
[IsDependentOn(typeof(BuildThunderstorePackageTask))]
public sealed class PublishTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext ctx)
    {
        ctx.Log.Information("Packing BepInEx");

        foreach (var dist in ctx.Distributions)
        {
            var targetZipName = $"BepInEx-{dist.Target}-{ctx.BuildPackageVersion}.zip";
            ctx.Log.Information($"Packing {targetZipName}");
            ctx.Zip(ctx.DistributionDirectory.Combine(dist.Target),
                    ctx.DistributionDirectory
                       .CombineWithFilePath(targetZipName));
        }


        var changeLog = "";
        if (!string.IsNullOrWhiteSpace(ctx.LastBuildCommit))
        {
            var changeLogContents =
                ctx.Git($"--no-pager log --no-merges --pretty=\"format:<li>(<code>%h</code>) [%an] %s</li>\" {ctx.LastBuildCommit}..HEAD",
                        "\n");
            changeLog = $"<ul>{changeLogContents}</ul>";
        }

        ctx.SerializeJsonToPrettyFile(ctx.DistributionDirectory.CombineWithFilePath("info.json"),
                                      new Dictionary<string, object>
                                      {
                                          ["id"] = ctx.BuildId.ToString(),
                                          ["date"] = DateTime.Now.ToString("o"),
                                          ["changelog"] = changeLog,
                                          ["hash"] = ctx.CurrentCommit.Sha,
                                          ["short_hash"] = ctx.GitShortenSha(ctx.RootDirectory, ctx.CurrentCommit),
                                          ["artifacts"] = ctx.Distributions.Select(d => new Dictionary<string, string>
                                          {
                                              ["file"] = $"BepInEx-{d.Target}-{ctx.BuildPackageVersion}.zip",
                                              ["description"] =
                                                  $"BepInEx {d.Engine} ({d.Runtime}{(d.FrameworkTarget == null ? "" : " " + d.FrameworkTarget)}) for {d.ClearOsName} ({d.Arch}) games"
                                          }).ToArray()
                                      });
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(CompileTask))]
public class DefaultTask : FrostingTask { }
