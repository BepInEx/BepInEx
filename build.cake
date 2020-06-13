#addin nuget:?package=Cake.FileHelpers&version=3.2.1
#addin nuget:?package=SharpZipLib&version=1.2.0
#addin nuget:?package=Cake.Compression&version=0.2.4
#addin nuget:?package=Cake.Json&version=4.0.0
#addin nuget:?package=Newtonsoft.Json&version=11.0.2

const string DOORSTOP_VER_WIN = "3.0.1.0";
const string DOORSTOP_VER_NIX = "1.2.0.0";
const string DOORSTOP_DLL = "winhttp.dll";

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
    var bepinExProperties = Directory("./BepInEx/Properties");

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
    buildSettings.Properties["TargetFrameworks"] = new []{ "net35" };
    MSBuild("./BepInEx.sln", buildSettings);
})
.Finally(() => 
{
    var bepinExProperties = Directory("./BepInEx/Properties");
    if(isBleedingEdge)
    {
        DeleteFile(bepinExProperties + File("AssemblyInfo.cs"));
        MoveFile(bepinExProperties + File("AssemblyInfo.cs.bak"), bepinExProperties + File("AssemblyInfo.cs"));
    }
});

Task("DownloadDoorstop")
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

    DownloadFile($"https://github.com/NeighTools/UnityDoorstop.Unix/releases/download/v{DOORSTOP_VER_NIX}/doorstop_v{DOORSTOP_VER_NIX}_linux.zip", doorstopLinuxPath);
    DownloadFile($"https://github.com/NeighTools/UnityDoorstop.Unix/releases/download/v{DOORSTOP_VER_NIX}/doorstop_v{DOORSTOP_VER_NIX}_macos.zip", doorstopMacPath);

    Information("Extracting Doorstop");
    ZipUncompress(doorstopX86Path, doorstopPath + Directory("x86"));
    ZipUncompress(doorstopX64Path, doorstopPath + Directory("x64"));

    ZipUncompress(doorstopLinuxPath, doorstopPath + Directory("nix"));
    ZipUncompress(doorstopMacPath, doorstopPath + Directory("nix"));
});

Task("MakeDist")
    .IsDependentOn("Build")
    .IsDependentOn("DownloadDoorstop")
    .Does(() =>
{
    var distDir = Directory("./bin/dist");
    var distPatcherDir = distDir + Directory("patcher");
    var doorstopPath = Directory("./bin/doorstop");

    CreateDirectory(distDir);
    CreateDirectory(distPatcherDir);

    var changelog = TransformText("<%commit_count%> commits since <%last_tag%>\r\n\r\nChangelog (excluding merges):\r\n<%commit_log%>")
                        .WithToken("commit_count", RunGit($"rev-list --count {latestTag}..HEAD"))
                        .WithToken("last_tag", latestTag)
                        .WithToken("commit_log", RunGit($"--no-pager log --no-merges --pretty=\"format:* (%h) [%an] %s\" {latestTag}..HEAD", "\r\n"))
                        .ToString();

    void PackageBepin(string arch, string copyPattern, string doorstopConfigPattern, string libDir = null, bool ensureLf = false) 
    {
        var distArchDir = distDir + Directory(arch);
        var bepinDir = distArchDir + Directory("BepInEx");
        var doorstopTargetDir = distArchDir;
        if(libDir != null) doorstopTargetDir += Directory(libDir);

        var doorstopFiles = doorstopPath + Directory(arch) + File(copyPattern);

        CreateDirectory(distArchDir);
        CreateDirectory(doorstopTargetDir);
        CreateDirectory(bepinDir + Directory("core"));
        CreateDirectory(bepinDir + Directory("plugins"));
        CreateDirectory(bepinDir + Directory("patchers"));

        CopyFiles($"./doorstop/{doorstopConfigPattern}", distArchDir);
        if(ensureLf)
            ReplaceTextInFiles($"{distArchDir}/{doorstopConfigPattern}", "\r\n", "\n");
        CopyFiles("./bin/*.*", bepinDir + Directory("core"));
        CopyFiles(doorstopFiles, doorstopTargetDir);
        FileWriteText(distArchDir + File("changelog.txt"), changelog);
    }

    PackageBepin("x86", DOORSTOP_DLL, "doorstop_config.ini");
    PackageBepin("x64", DOORSTOP_DLL, "doorstop_config.ini");
    PackageBepin("nix", "*.*", "run_bepinex.sh", "doorstop_libs", true);
    CopyFileToDirectory(File("./bin/patcher/BepInEx.Patcher.exe"), distPatcherDir);
});

Task("Pack")
    .IsDependentOn("MakeDist")
    .Does(() =>
{
    var distDir = Directory("./bin/dist");
    var commitPrefix = isBleedingEdge ? $"_{currentCommitShort}_" : "_";

    Information("Packing BepInEx");
    ZipCompress(distDir + Directory("x86"), distDir + File($"BepInEx_x86{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("x64"), distDir + File($"BepInEx_x64{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("nix"), distDir + File($"BepInEx_unix{commitPrefix}{buildVersion}.zip"));

    Information("Packing BepInEx.Patcher");
    ZipCompress(distDir + Directory("patcher"), distDir + File($"BepInEx_Patcher{commitPrefix}{buildVersion}.zip"));

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
                        ["file"] = $"BepInEx_x64{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx for x64 machines"
                    },
                    new Dictionary<string, object> {
                        ["file"] = $"BepInEx_x86{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx for x86 machines"
                    },
                    new Dictionary<string, object> {
                        ["file"] = $"BepInEx_unix{commitPrefix}{buildVersion}.zip",
                        ["description"] = "BepInEx for Unix with GCC (Linux, MacOS)"
                    },
                    new Dictionary<string, object> {
                        ["file"] = $"BepInEx_Patcher{commitPrefix}{buildVersion}.zip",
                        ["description"] = "Hardpatcher for BepInEx. IMPORTANT: USE ONLY IF DOORSTOP DOES NOT WORK FOR SOME REASON!"
                    }
                }
            }));
    }
});

RunTarget(target);