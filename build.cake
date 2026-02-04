#addin nuget:?package=Cake.FileHelpers&version=5.0.0
#addin nuget:?package=SharpZipLib&version=1.4.2
#addin nuget:?package=Cake.Compression&version=0.3.0
#addin nuget:?package=Cake.Json&version=7.0.1
#addin nuget:?package=Newtonsoft.Json&version=13.0.3

const string DOORSTOP_VER = "4.5.0";

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
    DotNetRestore("./BepInEx.sln");
});

Task("Build")
    .IsDependentOn("Cleanup")
    .IsDependentOn("PullDependencies")
    .Does(() =>
{
    var bepinExProperties = Directory("./BepInEx/Properties");

    buildVersion = FindRegexMatchGroupInFile(File("Directory.Build.props"), "<BepInExVersionPrefix>(.+?)<\\/BepInExVersionPrefix>", 1, System.Text.RegularExpressions.RegexOptions.None).Value;

    var settings = new DotNetPublishSettings
    {
        Configuration = "Release",
    };

    if(isBleedingEdge)
    {
        settings.MSBuildSettings = new DotNetMSBuildSettings().WithProperty("BepInExVersionSuffix", "be." + buildId);

        buildVersion = buildVersion + "-be." + buildId;

        CopyFile(bepinExProperties + File("AssemblyInfo.cs"), bepinExProperties + File("AssemblyInfo.cs.bak"));

        FileAppendText(bepinExProperties + File("AssemblyInfo.cs"), 
            TransformText("\n[assembly: BepInEx.BuildInfo(\"BLEEDING EDGE Build #<%buildNumber%> from <%shortCommit%> at <%branchName%>\")]\n")
                .WithToken("buildNumber", buildId)
                .WithToken("shortCommit", currentCommit)
                .WithToken("branchName", currentBranch)
                .ToString());
    }


    settings.OutputDirectory = "./bin/";
    DotNetPublish("./BepInEx.Preloader/BepInEx.Preloader.csproj", settings);

    settings.OutputDirectory = "./bin/patcher/";
    DotNetPublish("./BepInEx.Patcher/BepInEx.Patcher.csproj", settings);
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
    var doorstopWinPath = doorstopPath + File("doorstop_win.zip");
    var doorstopLinuxPath = doorstopPath + File("doorstop_linux.zip");
    var doorstopMacPath = doorstopPath + File("doorstop_macos.zip");
    CreateDirectory(doorstopPath);

    DownloadFile($"https://github.com/NeighTools/UnityDoorstop/releases/download/v{DOORSTOP_VER}/doorstop_win_release_{DOORSTOP_VER}.zip", doorstopWinPath);
    DownloadFile($"https://github.com/NeighTools/UnityDoorstop/releases/download/v{DOORSTOP_VER}/doorstop_linux_release_{DOORSTOP_VER}.zip", doorstopLinuxPath);
    DownloadFile($"https://github.com/NeighTools/UnityDoorstop/releases/download/v{DOORSTOP_VER}/doorstop_macos_release_{DOORSTOP_VER}.zip", doorstopMacPath);

    Information("Extracting Doorstop");
    ZipUncompress(doorstopWinPath, doorstopPath + Directory("win"));
    ZipUncompress(doorstopLinuxPath, doorstopPath + Directory("linux"));
    ZipUncompress(doorstopMacPath, doorstopPath + Directory("macos"));
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

    void PackageBepin(string os, string arch, string copyPattern, string doorstopConfigPattern, bool ensureLf = false) 
    {
        var distArchDir = distDir + Directory($"{os}_{arch}");
        var bepinDir = distArchDir + Directory("BepInEx");
        var doorstopTargetDir = distArchDir;
        var doorstopOsArchDir = doorstopPath + Directory(os) + Directory(arch);

        var doorstopFiles = doorstopOsArchDir + File(copyPattern);
        var doorstopVersionFiles = doorstopOsArchDir + File(".doorstop_version");

        CreateDirectory(distArchDir);
        CreateDirectory(doorstopTargetDir);
        CreateDirectory(bepinDir + Directory("core"));
        CreateDirectory(bepinDir + Directory("plugins"));
        CreateDirectory(bepinDir + Directory("patchers"));

        CopyFiles($"./doorstop/{doorstopConfigPattern}", distArchDir);
        if(ensureLf)
            ReplaceTextInFiles($"{distArchDir}/{doorstopConfigPattern}", "\r\n", "\n");
        CopyFiles("./bin/*.*", bepinDir + Directory("core"));
        CopyFiles(doorstopFiles.ToString(), doorstopTargetDir);
        CopyFiles(doorstopVersionFiles.ToString(), doorstopTargetDir);
        FileWriteText(distArchDir + File("changelog.txt"), changelog);
    }

    PackageBepin("win", "x64", "winhttp.dll", "doorstop_config.ini");
    PackageBepin("win", "x86", "winhttp.dll", "doorstop_config.ini");
    PackageBepin("linux", "x64", "libdoorstop.so", "run_bepinex.sh", true);
    PackageBepin("linux", "x86", "libdoorstop.so", "run_bepinex.sh", true);
    PackageBepin("macos", "x64", "libdoorstop.dylib", "run_bepinex.sh", true);
    CopyFileToDirectory(File("./bin/patcher/BepInEx.Patcher.exe"), distPatcherDir);
});

Task("Pack")
    .IsDependentOn("MakeDist")
    .Does(() =>
{
    var distDir = Directory("./bin/dist");
    var commitPrefix = isBleedingEdge ? $"_{currentCommitShort}_" : "_";

    Information("Packing BepInEx");
    ZipCompress(distDir + Directory("win_x86"), distDir + File($"BepInEx_win_x86{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("win_x64"), distDir + File($"BepInEx_win_x64{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("linux_x86"), distDir + File($"BepInEx_linux_x86{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("linux_x64"), distDir + File($"BepInEx_linux_x64{commitPrefix}{buildVersion}.zip"));
    ZipCompress(distDir + Directory("macos_x64"), distDir + File($"BepInEx_macos_x64{commitPrefix}{buildVersion}.zip"));

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