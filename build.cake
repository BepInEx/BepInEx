#addin nuget:?package=Cake.FileHelpers&version=3.2.1
#addin nuget:?package=SharpZipLib&version=1.2.0
#addin nuget:?package=Cake.Compression&version=0.2.4
#addin nuget:?package=Cake.Json&version=4.0.0
#addin nuget:?package=Newtonsoft.Json&version=11.0.2

var target = Argument("target", "Build");
var isBleedingEdge = Argument("bleeding_edge", false);
var buildId = Argument("build_id", 0);
var lastBuildCommit = Argument("last_build_commit", "");

var buildVersion = "";
var currentCommit = RunGit("rev-parse HEAD");
var currentCommitShort = RunGit("log -n 1 --pretty=\"format:%h\"").Trim();
var currentBranch = RunGit("rev-parse --abbrev-ref HEAD");
var latestTag = RunGit("describe --tags --abbrev=0");

string RunGit(string command, string separator = "") 
{
    using(var process = StartAndReturnProcess("git", new ProcessSettings { Arguments = command, RedirectStandardOutput = true })) 
    {
        process.WaitForExit();
        return string.Join(separator, process.GetStandardOutput());
    }
}

Task("Cleanup")
    .Does(() =>
{
    Information("Removing old binaries");
    CreateDirectory("./bin");
    CleanDirectory("./bin");

    Information("Cleaning up old build objects");
    CleanDirectories(GetDirectories("./**/bin/"));
    CleanDirectories(GetDirectories("./**/obj/"));
});

Task("PullDependencies")
    .Does(() =>
{
    Information("Updating git submodules");
    StartProcess("git", "submodule update --init --recursive");

    Information("Restoring NuGet packages");
    NuGetRestore("./BepInEx.sln");
});

Task("Build")
    .IsDependentOn("Cleanup")
    .IsDependentOn("PullDependencies")
    .Does(() =>
{
    var bepinExProperties = Directory("./BepInEx.Core/Properties");

    if(isBleedingEdge)
    {
        CopyFile(bepinExProperties + File("AssemblyInfo.cs"), bepinExProperties + File("AssemblyInfo.cs.bak"));
        ReplaceRegexInFiles(bepinExProperties + File("AssemblyInfo.cs"), "([0-9]+\\.[0-9]+\\.[0-9]+\\.)[0-9]+", "${1}" + buildId);

        FileAppendText(bepinExProperties + File("AssemblyInfo.cs"), 
            TransformText("\n[assembly: BuildInfo(\"BLEEDING EDGE Build #<%buildNumber%> from <%shortCommit%> at <%branchName%>\")]\n")
                .WithToken("buildNumber", buildId)
                .WithToken("shortCommit", currentCommit)
                .WithToken("branchName", currentBranch)
                .ToString());
    }

    buildVersion = FindRegexMatchInFile(bepinExProperties + File("AssemblyInfo.cs"), "[0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+", System.Text.RegularExpressions.RegexOptions.None);

    var buildSettings = new MSBuildSettings {
        Configuration = "Release",
        Restore = true
    };

    //buildSettings.Properties["TargetFrameworks"] = new []{ "net35" };
    MSBuild("./BepInEx.Unity/BepInEx.Unity.csproj", buildSettings);
	
    //buildSettings.Properties["TargetFrameworks"] = new []{ "net452" };
    MSBuild("./BepInEx.NetLauncher/BepInEx.NetLauncher.csproj", buildSettings);
	
    //buildSettings.Properties["TargetFrameworks"] = new []{ "net472" };
    MSBuild("./BepInEx.IL2CPP/BepInEx.IL2CPP.csproj", buildSettings);
})
.Finally(() => 
{
    var bepinExProperties = Directory("./BepInEx.Core/Properties");
    if(isBleedingEdge)
    {
        DeleteFile(bepinExProperties + File("AssemblyInfo.cs"));
        MoveFile(bepinExProperties + File("AssemblyInfo.cs.bak"), bepinExProperties + File("AssemblyInfo.cs"));
    }
});

const string DOORSTOP_VER_WIN = "3.1.0.0";
const string DOORSTOP_VER_UNIX = "1.3.0.0";
const string MONO_VER = "2020.11.08";
const string DOORSTOP_DLL = "winhttp.dll";
Task("DownloadDependencies")
    .Does(() =>
{
    Information("Downloading Doorstop");

    var doorstopPath = Directory("./bin/doorstop");
    var doorstopX64Path = doorstopPath + File("doorstop_x64.zip");
    var doorstopX86Path = doorstopPath + File("doorstop_x86.zip");
    var doorstopLinuxPath = doorstopPath + File("doorstop_linux.zip");
    var doorstopMacPath = doorstopPath + File("doorstop_macos.zip");
    CreateDirectory(doorstopPath);

    DownloadFile($"https://github.com/NeighTools/UnityDoorstop/releases/download/v{DOORSTOP_VER_WIN}/Doorstop_x64_{DOORSTOP_VER_WIN}.zip", doorstopX64Path);
    DownloadFile($"https://github.com/NeighTools/UnityDoorstop/releases/download/v{DOORSTOP_VER_WIN}/Doorstop_x86_{DOORSTOP_VER_WIN}.zip", doorstopX86Path);
    
    DownloadFile($"https://github.com/NeighTools/UnityDoorstop.Unix/releases/download/v{DOORSTOP_VER_UNIX}/doorstop_v{DOORSTOP_VER_UNIX}_linux.zip", doorstopLinuxPath);
    DownloadFile($"https://github.com/NeighTools/UnityDoorstop.Unix/releases/download/v{DOORSTOP_VER_UNIX}/doorstop_v{DOORSTOP_VER_UNIX}_macos.zip", doorstopMacPath);

    Information("Extracting Doorstop");
    ZipUncompress(doorstopX86Path, doorstopPath + Directory("x86"));
    ZipUncompress(doorstopX64Path, doorstopPath + Directory("x64"));

    ZipUncompress(doorstopLinuxPath, doorstopPath + Directory("unix"));
    ZipUncompress(doorstopMacPath, doorstopPath + Directory("unix"));

    
    Information("Downloading Mono");

    var monoPath = Directory("./bin/doorstop/mono");
    var monoX64Path = doorstopPath + File("mono_x64.zip");
    var monoX86Path = doorstopPath + File("mono_x86.zip");
    CreateDirectory(monoPath);

    DownloadFile($"https://github.com/BepInEx/mono/releases/download/{MONO_VER}/mono-x64.zip", monoX64Path);
    DownloadFile($"https://github.com/BepInEx/mono/releases/download/{MONO_VER}/mono-x86.zip", monoX86Path);

    Information("Extracting Mono");

    ZipUncompress(monoX64Path, monoPath + Directory("x64"));
    ZipUncompress(monoX86Path, monoPath + Directory("x86"));
});

Task("MakeDist")
    .IsDependentOn("Build")
    .IsDependentOn("DownloadDependencies")
    .Does(() =>
{
    var distDir = Directory("./bin/dist");
    //var distPatcherDir = distDir + Directory("patcher");
    var doorstopPath = Directory("./bin/doorstop");

    CreateDirectory(distDir);
    //CreateDirectory(distPatcherDir);

    var changelog = TransformText("<%commit_count%> commits since <%last_tag%>\r\n\r\nChangelog (excluding merges):\r\n<%commit_log%>")
                        .WithToken("commit_count", RunGit($"rev-list --count {latestTag}..HEAD"))
                        .WithToken("last_tag", latestTag)
                        .WithToken("commit_log", RunGit($"--no-pager log --no-merges --pretty=\"format:* (%h) [%an] %s\" {latestTag}..HEAD", "\r\n"))
                        .ToString();

    void PackageBepin(string platform, string arch, string originDir, string doorstopConfigFile = null, bool copyMono = false) 
    {
		string platformName = platform + (arch == null ? "" : "_" + arch);
		bool isUnix = arch == "unix";

		Information("Creating distributions for platform \"" + platformName + "\"...");
	
		string doorstopArchPath = null;
		
		if (arch != null)
		{
            doorstopArchPath = doorstopPath + Directory(arch)
                + File(isUnix ? "*.*" : DOORSTOP_DLL);
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

		if (doorstopArchPath != null)
		{
			CopyFile("./doorstop/" + doorstopConfigFile, Directory(distArchDir) + File(isUnix ? "run_bepinex.sh" : "doorstop_config.ini"));
			CopyFiles(doorstopArchPath, doorstopDir);

            if (isUnix)
            {
                ReplaceTextInFiles($"{distArchDir}/run_bepinex.sh", "\r\n", "\n");
            }

            if (copyMono)
            {
                CopyDirectory("./bin/doorstop/mono/" + arch + "/mono", Directory(distArchDir) + Directory("mono"));
            }
		}

        CopyFiles("./bin/" + Directory(originDir) + "/*.*", Directory(bepinDir) + Directory("core"));


        FileWriteText(distArchDir + File("changelog.txt"), changelog);
    }

    PackageBepin("UnityMono", "x86", "Unity", "doorstop_config_mono.ini");
    PackageBepin("UnityMono", "x64", "Unity", "doorstop_config_mono.ini");
    PackageBepin("UnityMono", "unix", "Unity", "run_bepinex.sh");
    PackageBepin("UnityIL2CPP", "x86", "IL2CPP", "doorstop_config_il2cpp.ini", copyMono: true);
    PackageBepin("UnityIL2CPP", "x64", "IL2CPP", "doorstop_config_il2cpp.ini", copyMono: true);
    PackageBepin("NetLauncher", null, "NetLauncher");
    //CopyFileToDirectory(File("./bin/patcher/BepInEx.Patcher.exe"), distPatcherDir);
});

Task("Pack")
    .IsDependentOn("MakeDist")
    .Does(() =>
{
    var distDir = Directory("./bin/dist");
    var commitPrefix = isBleedingEdge ? $"_{currentCommitShort}_" : "_";

    Information("Packing BepInEx");
    ZipCompress(distDir + Directory("UnityMono_x86"), distDir + File($"BepInEx_UnityMono_x86{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("UnityMono_x64"), distDir + File($"BepInEx_UnityMono_x64{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("UnityMono_unix"), distDir + File($"BepInEx_UnityMono_unix{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("UnityIL2CPP_x86"), distDir + File($"BepInEx_UnityIL2CPP_x86{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("UnityIL2CPP_x64"), distDir + File($"BepInEx_UnityIL2CPP_x64{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("NetLauncher"), distDir + File($"BepInEx_NetLauncher{commitPrefix}{buildVersion}.zip"));

    // Information("Packing BepInEx.Patcher");
    // ZipCompress(distDir + Directory("patcher"), distDir + File($"BepInEx_Patcher{commitPrefix}{buildVersion}.zip"));

    if(isBleedingEdge) 
    {
        var changelog = "";

        if(!string.IsNullOrEmpty(lastBuildCommit)) {
            changelog = TransformText("<ul><%changelog%></ul>")
                        .WithToken("changelog", RunGit($"--no-pager log --no-merges --pretty=\"format:<li>(<code>%h</code>) [%an] %s</li>\" {lastBuildCommit}..HEAD"))
                        .ToString();
        }

        FileWriteText(distDir + File("info.json"), 
            SerializeJsonPretty(new Dictionary<string, object>{
                ["id"] = buildId.ToString(),
                ["date"] = DateTime.Now.ToString("o"),
                ["changelog"] = changelog,
                ["hash"] = currentCommit,
                ["artifacts"] = new Dictionary<string, object>[] {
                    new Dictionary<string, object> {
                        ["file"] = $"BepInEx_UnityMono_x64{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx Unity Mono for Windows x64 machines"
                    },
                    new Dictionary<string, object> {
                        ["file"] = $"BepInEx_UnityMono_x86{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx Unity Mono for Windows x86 machines"
                    },
                    new Dictionary<string, object> {
                        ["file"] = $"BepInEx_unix{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx Unity Mono for Unix machines with GCC (Linux, MacOS)"
                    },
                    new Dictionary<string, object> {
                        ["file"] = $"BepInEx_UnityIL2CPP_x64{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx Unity IL2CPP for Windows x64 machines"
                    },
                    new Dictionary<string, object> {
                        ["file"] = $"BepInEx_UnityIL2CPP_x86{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx Unity IL2CPP for Windows x86 machines"
                    },
                    new Dictionary<string, object> {
                        ["file"] = $"BepInEx_NetLauncher{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx .NET Launcher for .NET Framework/XNA games"
                    },
                    // new Dictionary<string, object> {
                        // ["file"] = $"BepInEx_Patcher{commitPrefix}{buildVersion}.zip",
                        // ["description"] = "Hardpatcher for BepInEx. IMPORTANT: USE ONLY IF DOORSTOP DOES NOT WORK FOR SOME REASON!"
                    // }
                }
            }));
    }
});

RunTarget(target);