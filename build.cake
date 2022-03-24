#addin nuget:?package=Cake.FileHelpers&version=4.0.1
#addin nuget:?package=SharpZipLib&version=1.3.1
#addin nuget:?package=Cake.Compression&version=0.2.6
#addin nuget:?package=Cake.Json&version=6.0.1
#addin nuget:?package=Newtonsoft.Json&version=13.0.1
#addin nuget:?package=Cake.Http&version=1.3.0

var target = Argument("target", "Build");
var isBleedingEdge = Argument("bleeding_edge", false);
var buildId = Argument("build_id", 0);
var cleanDependencyCache = Argument("clean_build_cache", false);
var lastBuildCommit = Argument("last_build_commit", "");
var nugetPushSource = Argument("nuget_push_source", "https://nuget.bepinex.dev/v3/index.json");
var nugetPushKey = Argument("nuget_push_key", "");
var customBuildVersion = Argument("build_version", "");

var buildVersion = "";
var currentCommit = RunGit("rev-parse HEAD");
var currentCommitShort = RunGit("log -n 1 --pretty=\"format:%h\"").Trim();
var currentBranch = RunGit("rev-parse --abbrev-ref HEAD");
var latestTag = RunGit("describe --tags --abbrev=0");

string RunGit(string command, string separator = "") 
{
    using var process = StartAndReturnProcess("git", new() { Arguments = command, RedirectStandardOutput = true });
    process.WaitForExit();
    return string.Join(separator, process.GetStandardOutput());
}

const string DEP_CACHE_NAME = ".dep_cache";

Task("Cleanup")
    .Does(() =>
{
    Information("Removing old binaries");
    CreateDirectory("./bin");
    CleanDirectory("./bin", fi => {
      var cachePath = fi.Path.FullPath.Contains(DEP_CACHE_NAME);
      return !cachePath || cleanDependencyCache && cachePath;
    });

    Information("Cleaning up old build objects");
    CleanDirectories(GetDirectories("./BepInEx.*/**/bin/"));
    CleanDirectories(GetDirectories("./BepInEx.*/**/obj/"));
});

Task("Build")
    .IsDependentOn("Cleanup")
    .Does(() =>
{
    var bepinExProperties = Directory("./BepInEx.Shared");

    buildVersion = FindRegexMatchGroupInFile(bepinExProperties + File("BepInEx.Shared.projitems"), @"\<Version\>([0-9]+\.[0-9]+\.[0-9]+)\<\/Version\>", 1, System.Text.RegularExpressions.RegexOptions.None).Value;

    var buildSettings = new DotNetBuildSettings {
        Configuration = "Release",
		MSBuildSettings = new() // Apparently needed in some versions of CakeBuild
    };

    if (isBleedingEdge) 
    {
        buildSettings.MSBuildSettings.Properties["BuildInfo"] = new[] {
            TransformText("BLEEDING EDGE Build #<%buildNumber%> from <%shortCommit%> at <%branchName%>")
                .WithToken("buildNumber", buildId)
                .WithToken("shortCommit", currentCommit)
                .WithToken("branchName", currentBranch)
                .ToString()
        };

        buildSettings.MSBuildSettings.Properties["AssemblyVersion"] = new[] { buildVersion + "." + buildId };
        buildVersion += "-be." + buildId;
        buildSettings.MSBuildSettings.Properties["Version"] = new[] { buildVersion };
    } else if (!string.IsNullOrEmpty(customBuildVersion))
    {
        buildVersion = customBuildVersion;
        buildSettings.MSBuildSettings.Properties["Version"] = new[] { buildVersion };
    }

    foreach (var file in GetFiles("./BepInEx.*/*.csproj"))
    {
        DotNetBuild(file.FullPath, buildSettings);
    }
});

const string DOORSTOP_VER = "4.0.0-alpha.1";
const string MONO_VER = "2021.6.24";
const string DOORSTOP_PROXY_DLL = "winhttp.dll";

var depCachePath = Directory($"./bin/{DEP_CACHE_NAME}");
var doorstopPath = depCachePath + Directory("doorstop");
var monoPath = depCachePath + Directory("mono");

Task("DownloadDependencies")
    .Does(() =>
{
    var cacheFile = depCachePath + File("cache.json");
    var cache = FileExists(cacheFile) ? DeserializeJsonFromFile<Dictionary<string, string>>(cacheFile) : new Dictionary<string, string>();

    bool NeedsRedownload(string repo, string curVer)
    {
        if (!cache.TryGetValue(repo, out var ver) || ver != curVer)
        {
            cache[repo] = curVer;
            return true;
        }
        return false;
    }

    CreateDirectory(doorstopPath);
    CreateDirectory(monoPath);

    if (NeedsRedownload("NeighTools/UnityDoorstop", DOORSTOP_VER))
    {
        Information("Updating Doorstop");
        var doorstopZipPathWin = doorstopPath + File("doorstop_win.zip");
        var doorstopZipPathLinux = doorstopPath + File("doorstop_linux.zip");
        var doorstopZipPathMacOs = doorstopPath + File("doorstop_macos.zip");

        // TODO: Fix URLs once Doorstop 4 is released
        DownloadFile($"https://nightly.link/NeighTools/UnityDoorstop/workflows/build-be/wip-rewrite/Doorstop_WIN-RELEASE.zip", doorstopZipPathWin);
        DownloadFile($"https://nightly.link/NeighTools/UnityDoorstop/workflows/build-be/wip-rewrite/Doorstop_LINUX-RELEASE.zip", doorstopZipPathLinux);
        DownloadFile($"https://nightly.link/NeighTools/UnityDoorstop/workflows/build-be/wip-rewrite/Doorstop_MACOS-RELEASE.zip", doorstopZipPathMacOs);

        ZipUncompress(doorstopZipPathWin, doorstopPath + Directory("win"));
        ZipUncompress(doorstopZipPathLinux, doorstopPath + Directory("unix"));
        ZipUncompress(doorstopZipPathMacOs, doorstopPath + Directory("unix"));
    }

    if (NeedsRedownload("BepInEx/mono", MONO_VER))
    {
        // TODO: Instead download dotnet + BCL
        Information("Updating Mono");

        var monoX64Path = doorstopPath + File("mono_x64.zip");
        var monoX86Path = doorstopPath + File("mono_x86.zip");

        DownloadFile($"https://github.com/BepInEx/mono/releases/download/{MONO_VER}/mono-x64.zip", monoX64Path);
        DownloadFile($"https://github.com/BepInEx/mono/releases/download/{MONO_VER}/mono-x86.zip", monoX86Path);

        ZipUncompress(monoX64Path, monoPath + Directory("x64"));
        ZipUncompress(monoX86Path, monoPath + Directory("x86"));
    }

    SerializeJsonToFile(cacheFile, cache);
});

Task("PushNuGet")
    .IsDependentOn("Build")
    .Does(() => 
{
    if (string.IsNullOrEmpty(nugetPushSource))
    {
        Information("NuGet push source is missing");
        return;
    }
    if (string.IsNullOrEmpty(nugetPushKey))
    {
        Information("NuGet push key is missing");
        return;
    }
    
    foreach (var file in GetFiles("./bin/NuGet/*.nupkg"))
    {
        Information($"Pushing {file}");
        DotNetNuGetPush(file.FullPath, new() {
            Source = nugetPushSource,
            ApiKey = nugetPushKey,
        });
    }
});

Task("MakeDist")
    .IsDependentOn("Build")
    .IsDependentOn("DownloadDependencies")
    .Does(() =>
{
    var distDir = Directory("./bin/dist");

    CreateDirectory(distDir);

    var changelog = TransformText("<%commit_count%> commits since <%last_tag%>\r\n\r\nChangelog (excluding merges):\r\n<%commit_log%>")
                        .WithToken("commit_count", RunGit($"rev-list --count {latestTag}..HEAD"))
                        .WithToken("last_tag", latestTag)
                        .WithToken("commit_log", RunGit($"--no-pager log --no-merges --pretty=\"format:* (%h) [%an] %s\" {latestTag}..HEAD", "\r\n"))
                        .ToString();

    void PackageBepin(string platform, string os, string doorstopArch, string originDir, string doorstopConfigFile = null, bool copyMono = false) 
    {
        // TODO: Right now platform name can be quite long (e.g. UnityMono_win_x86), should it be simplified?
        var platformName = platform + (os == null ? "" : "_" + os) + (doorstopArch == null ? "" : "_" + doorstopArch);
        var isUnix = os == "unix";

        Information("Creating distributions for platform \"" + platformName + "\"...");
    
        string doorstopArchPath = null;
        ConvertableDirectoryPath doorstopArchDir = null;
        
        if (doorstopConfigFile != null)
        {
            doorstopArchDir = doorstopPath + Directory(os) + Directory(isUnix ? "libdoorstop" : doorstopArch);
            doorstopArchPath = doorstopArchDir + File(isUnix ? "*.*" : DOORSTOP_PROXY_DLL);
        }
        
        var distArchDir = distDir + Directory(platformName);
        var bepinDir = distArchDir + Directory("BepInEx");
        var doorstopDir = distArchDir;
        if (isUnix) doorstopDir += Directory("doorstop_libs");
        
        CreateDirectory(distArchDir);
        CreateDirectory(doorstopDir);
        CreateDirectory(bepinDir + Directory("core"));
        CreateDirectory(bepinDir + Directory("plugins"));
        CreateDirectory(bepinDir + Directory("patchers"));

        if (doorstopConfigFile != null)
        {
            CopyFile("./doorstop/" + doorstopConfigFile, Directory(distArchDir) + File(isUnix ? "run_bepinex.sh" : "doorstop_config.ini"));
            if (isUnix)
            {
                CopyFiles(doorstopArchPath, doorstopDir);
                MoveFile(doorstopDir + File(".doorstop_version"), distArchDir + File(".doorstop_version"));
            }
            else
            {
                CopyFile(doorstopArchPath, Directory(doorstopDir) + File(DOORSTOP_PROXY_DLL));
                CopyFile(doorstopArchDir + File(".doorstop_version"), doorstopDir + File(".doorstop_version"));
            }

            if (isUnix)
            {
                ReplaceTextInFiles($"{distArchDir}/run_bepinex.sh", "\r\n", "\n");
            }

            if (copyMono)
            {
                CopyDirectory(monoPath + Directory(doorstopArch) + Directory("mono"), Directory(distArchDir) + Directory("mono"));
            }
        }

        CopyFiles($"./bin/{originDir}/*.*", Directory(bepinDir) + Directory("core"));

        FileWriteText(distArchDir + File("changelog.txt"), changelog);

        if (platform == "NetLauncher")
        {
            DeleteFile(Directory(bepinDir) + Directory("core") + File("BepInEx.NetLauncher.exe.config"));
            MoveFiles((string)(Directory(bepinDir) + Directory("core") + File("BepInEx.NetLauncher.*")), Directory(distArchDir));
        }
    }

    PackageBepin("UnityMono", "win", "x86", "Unity", "doorstop_config_mono.ini");
    PackageBepin("UnityMono", "win", "x64", "Unity", "doorstop_config_mono.ini");
    PackageBepin("UnityMono", "unix", null, "Unity", "run_bepinex.sh");
    PackageBepin("UnityIL2CPP", "win", "x86", "IL2CPP", "doorstop_config_il2cpp.ini", copyMono: true);
    PackageBepin("UnityIL2CPP", "win", "x64", "IL2CPP", "doorstop_config_il2cpp.ini", copyMono: true);
    PackageBepin("NetLauncher", "win", null, "NetLauncher");
});

Task("Pack")
    .IsDependentOn("MakeDist")
    .IsDependentOn("PushNuGet")
    .Does(() =>
{
    var distDir = Directory("./bin/dist");
    var commitPrefix = isBleedingEdge ? $"_{currentCommitShort}_" : "_";

    Information("Packing BepInEx");
    ZipCompress(distDir + Directory("UnityMono_win_x86"), distDir + File($"BepInEx_UnityMono_win_x86{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("UnityMono_win_x64"), distDir + File($"BepInEx_UnityMono_win_x64{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("UnityMono_unix"), distDir + File($"BepInEx_UnityMono_unix{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("UnityIL2CPP_win_x86"), distDir + File($"BepInEx_UnityIL2CPP_win_x86{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("UnityIL2CPP_win_x64"), distDir + File($"BepInEx_UnityIL2CPP_win_x64{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("NetLauncher_win"), distDir + File($"BepInEx_NetLauncher_win{commitPrefix}{buildVersion}.zip"));

    if(isBleedingEdge) 
    {
        var changelog = "";

        if(!string.IsNullOrEmpty(lastBuildCommit)) {
            changelog = TransformText("<ul><%changelog%></ul>")
                        .WithToken("changelog", RunGit($"--no-pager log --no-merges --pretty=\"format:<li>(<code>%h</code>) [%an] %s</li>\" {lastBuildCommit}..HEAD"))
                        .ToString();
        }

        FileWriteText(distDir + File("info.json"), 
            SerializeJsonPretty(new Dictionary<string, object> {
                ["id"] = buildId.ToString(),
                ["date"] = DateTime.Now.ToString("o"),
                ["changelog"] = changelog,
                ["hash"] = currentCommit,
                ["short_hash"] = currentCommitShort,
                ["artifacts"] = new Dictionary<string, object>[] {
                    new() {
                        ["file"] = $"BepInEx_UnityMono_win_x64{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx Unity Mono for Windows x64 games"
                    },
                    new() {
                        ["file"] = $"BepInEx_UnityMono_win_x86{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx Unity Mono for Windows x86 games"
                    },
                    new() {
                        ["file"] = $"BepInEx_UnityMono_unix{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx Unity Mono for Unix games using GCC (Linux, MacOS)"
                    },
                    new() {
                        ["file"] = $"BepInEx_UnityIL2CPP_win_x64{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx Unity IL2CPP for Windows x64 games"
                    },
                    new() {
                        ["file"] = $"BepInEx_UnityIL2CPP_win_x86{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx Unity IL2CPP for Windows x86 games"
                    },
                    new() {
                        ["file"] = $"BepInEx_NetLauncher_win{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx .NET Launcher for .NET Framework/XNA games"
                    },
                }
            }));
    }
});

RunTarget(target);
