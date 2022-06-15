#nullable enable
#addin nuget:?package=Cake.FileHelpers&version=4.0.1
#addin nuget:?package=SharpZipLib&version=1.3.1
#addin nuget:?package=Cake.Compression&version=0.2.6
#addin nuget:?package=Cake.Json&version=6.0.1
#addin nuget:?package=Newtonsoft.Json&version=13.0.1
#addin nuget:?package=Cake.Http&version=1.3.0

var task = Argument("target", "Build"); // Note: "target" is special Cake argument, don't change
var isBleedingEdge = Argument("bleeding_edge", false);
var buildId = Argument("build_id", 0);
var cleanDependencyCache = Argument("clean_build_cache", false);
var lastBuildCommit = Argument("last_build_commit", "");
var nugetPushSource = Argument("nuget_push_source", "https://nuget.bepinex.dev/v3/index.json");
var nugetPushKey = Argument("nuget_push_key", "");

var buildVersion = "";
var currentCommit = RunGit("rev-parse HEAD");
var currentCommitShort = RunGit("log -n 1 --pretty=\"format:%h\"").Trim();
var currentBranch = RunGit("rev-parse --abbrev-ref HEAD");
var latestTag = RunGit("describe --tags --abbrev=0");

string RunGit(string command, string separator = "") {
    using var process = StartAndReturnProcess("git", new() { Arguments = command, RedirectStandardOutput = true });
    process.WaitForExit();
    return string.Join(separator, process.GetStandardOutput());
}

readonly record struct Target(string Abi, string Os, string Cpu, bool NeedsRuntime = false) {
    public string Abi { get; } = Abi.Contains('-') ? throw new FormatException($"Target ABI cannot be hyphenated: {Abi}") : Abi;
    public string Os { get; } = Os.Contains('-') ? throw new FormatException($"Target OS cannot be hyphenated: {Os}") : Os;
    public string Cpu { get; } = Cpu.Contains('-') ? throw new FormatException($"Target CPU cannot be hyphenated: {Cpu}") : Cpu;
    
    public string RuntimeIdentifier => $"{Os}-{Cpu}";

    public override string ToString() => $"{Abi}-{Os}-{Cpu}";

    public bool IsUnix => Os is "unix" or "linux" or "macos";

    public string LibExt => Os switch {
        "macos" => "dylib",
        "linux" => "so",
        "windows" => "dll",
        _ => throw new NotSupportedException($"Target OS {Os} is not supported")
    };

    public string LibPrefix => IsUnix ? "lib" : "";
}

var targets = new Target[] {
    new("UnityMono", "windows", "x86"),
    new("UnityMono", "windows", "x64"),
    new("UnityMono", "linux", "x86"),
    new("UnityMono", "linux", "x64"),
    new("UnityMono", "macos", "x64"),
    new("UnityIL2CPP", "windows", "x86", true),
    new("UnityIL2CPP", "windows", "x64", true),
    new("UnityIL2CPP", "linux", "x86", true),
    new("UnityIL2CPP", "linux", "x64", true),
    new("UnityIL2CPP", "macos", "x64", true),
    new("NetLauncher", "windows", "any"),
};

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
    CleanDirectories(GetDirectories(new GlobPattern("./BepInEx.*/**/bin/")));
    CleanDirectories(GetDirectories(new GlobPattern("./BepInEx.*/**/obj/")));
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

    if (isBleedingEdge) {
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
    }

    foreach (var file in GetFiles("./BepInEx.*/*.csproj")) {
        DotNetBuild(file.FullPath, buildSettings);
    }
});

const string DOORSTOP_VER = "4.0.0-rc.3";
const string DOTNET_MINI_VER = "2022.06.15.1";
const string DOORSTOP_PROXY_DLL = "winhttp.dll";
const string DOBBY_VER = "1.0.0";

var depCachePath = Directory($"./bin/{DEP_CACHE_NAME}");
var doorstopPath = depCachePath + Directory("doorstop");
var dotnetPath = depCachePath + Directory("dotnet");
var dobbyPath = depCachePath + Directory("dobby");

Task("DownloadDependencies")
    .Does(() =>
{
    var cacheFile = depCachePath + File("cache.json");
    var cache = FileExists(cacheFile) ? DeserializeJsonFromFile<Dictionary<string, string>>(cacheFile) : new Dictionary<string, string>();

    bool NeedsRedownload(string repo, string curVer) {
        if (!cache.TryGetValue(repo, out var ver) || ver != curVer) {
            cache[repo] = curVer;
            return true;
        }
        return false;
    }

    CreateDirectory(doorstopPath);
    CreateDirectory(dotnetPath);
    CreateDirectory(dobbyPath);

    if (NeedsRedownload("NeighTools/UnityDoorstop", DOORSTOP_VER)) {
        Information("Updating Doorstop");
        var doorstopZipPathWin = depCachePath + File("doorstop_win.zip");
        var doorstopZipPathLinux = depCachePath + File("doorstop_linux.zip");
        var doorstopZipPathMacOs = depCachePath + File("doorstop_macos.zip");

        DownloadFile($"https://github.com/NeighTools/UnityDoorstop/releases/download/v{DOORSTOP_VER}/doorstop_win_release_{DOORSTOP_VER}.zip", doorstopZipPathWin);
        DownloadFile($"https://github.com/NeighTools/UnityDoorstop/releases/download/v{DOORSTOP_VER}/doorstop_linux_release_{DOORSTOP_VER}.zip", doorstopZipPathLinux);
        DownloadFile($"https://github.com/NeighTools/UnityDoorstop/releases/download/v{DOORSTOP_VER}/doorstop_macos_release_{DOORSTOP_VER}.zip", doorstopZipPathMacOs);

        ZipUncompress(doorstopZipPathWin, doorstopPath + Directory("windows"));
        ZipUncompress(doorstopZipPathLinux, doorstopPath + Directory("linux"));
        ZipUncompress(doorstopZipPathMacOs, doorstopPath + Directory("macos"));
    }

    if (NeedsRedownload("BepInEx/Dobby", DOBBY_VER)) {
        Information("Updating Dobby");
        var dobbyZipPathWin = depCachePath + File("dobby_win.zip");
        var dobbyZipPathLinux = depCachePath + File("dobby_linux.zip");
        var dobbyZipPathMacOs = depCachePath + File("dobby_macos.zip");

        DownloadFile($"https://github.com/BepInEx/Dobby/releases/download/v{DOBBY_VER}/dobby-win.zip", dobbyZipPathWin);
        DownloadFile($"https://github.com/BepInEx/Dobby/releases/download/v{DOBBY_VER}/dobby-linux.zip", dobbyZipPathLinux);
        DownloadFile($"https://github.com/BepInEx/Dobby/releases/download/v{DOBBY_VER}/dobby-macos.zip", dobbyZipPathMacOs);

        ZipUncompress(dobbyZipPathWin, dobbyPath + Directory("windows"));
        ZipUncompress(dobbyZipPathLinux, dobbyPath + Directory("linux"));
        ZipUncompress(dobbyZipPathMacOs, dobbyPath + Directory("macos"));
    }

    if (NeedsRedownload("BepInEx/dotnet-runtime", DOTNET_MINI_VER)) {
        Information("Updating mini-dotnet, this might take a while");

        var dotnetZipPath = depCachePath + File("dotnet-mini.zip");

        DownloadFile($"https://github.com/BepInEx/dotnet-runtime/releases/download/{DOTNET_MINI_VER}/dotnet-mini.zip", dotnetZipPath);

        ZipUncompress(dotnetZipPath, dotnetPath);
    }

    SerializeJsonToFile(cacheFile, cache);
});

Task("PushNuGet")
    .IsDependentOn("Build")
    .Does(() =>
{
    if (nugetPushSource is null or "") {
        Information("NuGet push source is missing");
        return;
    }
    if (nugetPushKey is null or "") {
        Information("NuGet push key is missing");
        return;
    }

    foreach (var file in GetFiles("./bin/NuGet/*.nupkg")) {
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

    void PackageBepin(Target target, ConvertableDirectoryPath originDir, ConvertableFilePath? doorstopConfigFile = null) {
        Information($"Creating distributions for target \"{target}\"...");

        string? doorstopTargetPath = null;
        ConvertableDirectoryPath? doorstopTargetDir = null;

        if (doorstopConfigFile is not null) {
            doorstopTargetDir = doorstopPath + Directory(target.Os) + Directory(target.Cpu);
            doorstopTargetPath = $"{doorstopTargetDir + File(target.IsUnix ? "libdoorstop.*" : DOORSTOP_PROXY_DLL)}";
        }

        var distTargetDir = distDir + Directory($"{target}");
        var distBepinDir = distTargetDir + Directory("BepInEx");
        var distDoorstopDir = distTargetDir;

        CreateDirectory(distTargetDir);
        CreateDirectory(distDoorstopDir);
        CreateDirectory(distBepinDir + Directory("core"));
        CreateDirectory(distBepinDir + Directory("plugins"));
        CreateDirectory(distBepinDir + Directory("patchers"));

        if (doorstopConfigFile is not null) {
            CopyFile(Directory("./doorstop") + doorstopConfigFile, distTargetDir + File(target.IsUnix ? "run_bepinex.sh" : "doorstop_config.ini"));
            if (target.IsUnix) {
                CopyFiles(new GlobPattern(doorstopTargetPath), distDoorstopDir);
            } else {
                CopyFile(File(doorstopTargetPath), Directory(distDoorstopDir) + File(DOORSTOP_PROXY_DLL));
            }
            CopyFile(doorstopTargetDir + File(".doorstop_version"), distDoorstopDir + File(".doorstop_version"));

            if (target.IsUnix) {
                ReplaceTextInFiles(distTargetDir + File("run_bepinex.sh"), "\r\n", "\n");
            }

            if (target.NeedsRuntime) {
                CopyDirectory(dotnetPath + Directory(target.RuntimeIdentifier), Directory(distTargetDir) + Directory("dotnet"));
                CopyFile(dobbyPath + Directory(target.Os) + File($"{target.LibPrefix}dobby_{target.Cpu}.{target.LibExt}"), distBepinDir + Directory("core") + File($"{target.LibPrefix}dobby.{target.LibExt}"));
            }
        }

        CopyFiles(new GlobPattern($"{Directory("./bin") + originDir + File("*.*")}"), Directory(distBepinDir) + Directory("core"));

        FileWriteText(distTargetDir + File("changelog.txt"), changelog);

        if (target.Abi is "NetLauncher") {
            DeleteFile(Directory(distBepinDir) + Directory("core") + File("BepInEx.NetLauncher.exe.config"));
            MoveFiles(new GlobPattern($"{Directory(distBepinDir) + Directory("core") + File("BepInEx.NetLauncher.*")}"), Directory(distTargetDir));
        }
    }

    foreach (Target target in targets) {
        (ConvertableDirectoryPath originDir, ConvertableFilePath? doorstopConfigFile) = (Directory(target.Abi), null);
        if (target.Abi is var abi and ("UnityIL2CPP" or "UnityMono")) {
            originDir = Directory(abi is "UnityIL2CPP" ? "IL2CPP" : "Unity");
            // TODO: Move config file resolving to Target class
            doorstopConfigFile = File(
                target.IsUnix ?
                  (abi is "UnityIL2CPP" ? "run_bepinex_il2cpp.sh" : "run_bepinex_mono.sh")
                : (abi is "UnityIL2CPP" ? "doorstop_config_il2cpp.ini" : "doorstop_config_mono.ini")
            );
        }
        PackageBepin(target, originDir, doorstopConfigFile);
    }
});

Task("Pack")
    .IsDependentOn("MakeDist")
    .IsDependentOn("PushNuGet")
    .Does(() =>
{
    var distDir = Directory("./bin/dist");
    var commitSuffix = isBleedingEdge ? $"+{currentCommitShort}" : "";

    Information("Packing BepInEx");
    foreach (Target target in targets) {
        ZipCompress(distDir + Directory($"{target}"), distDir + File($"BepInEx-{target}-{buildVersion}{commitSuffix}.zip"));
    }

    if(isBleedingEdge) {
        var changelog = lastBuildCommit is null or "" ? "" :
            TransformText("<ul><%changelog%></ul>")
                .WithToken("changelog", RunGit($"--no-pager log --no-merges --pretty=\"format:<li>(<code>%h</code>) [%an] %s</li>\" {lastBuildCommit}..HEAD"))
                .ToString();

        FileWriteText(distDir + File("info.json"), SerializeJsonPretty(new Dictionary<string, object> {
            ["id"] = buildId.ToString(),
            ["date"] = DateTime.Now.ToString("o"),
            ["changelog"] = changelog,
            ["hash"] = currentCommit,
            ["short_hash"] = currentCommitShort,
            ["artifacts"] = targets.Select(target => {
                (string abiName, string abiGamesPrefix) = target.Abi switch {
                    "NetLauncher" => (".NET Launcher", ".NET Framework/XNA "),
                    "UnityIL2CPP" => ("Unity IL2CPP", ""),
                    "UnityMono" => ("Unity Mono", ""),
                    var abi => (abi, $"{abi} "),
                };
                (string osName, string osGamesSuffix) = target.Os switch {
                    "unix" => ("Linux & macOS", " using GCC"),
                    "linux" => ("Linux", " using GCC"),
                    "macos" => ("macOS", ""),
                    "windows" => ("Windows", ""),
                    var os => (os, ""),
                };
                string cpuOsNameSuffix = target.Cpu switch {
                    "any" => "",
                    var cpu => $" {cpu}",
                };
                return new Dictionary<string, object>() {
                    ["file"] = $"BepInEx-{target}-{buildVersion}{commitSuffix}.zip",
                    ["description"] = $"BepInEx {abiName} for {osName}{cpuOsNameSuffix} {abiGamesPrefix}games{osGamesSuffix}",
                };
            }).ToArray(),
        }));
    }
});

RunTarget(task);
