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

    MSBuild("./BepInEx.sln", new MSBuildSettings {
        Configuration = "Release",
        Restore = true
    });
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

const string DOORSTOP_VER = "2.11.1.0";
const string DOORSTOP_DLL = "winhttp.dll";
Task("DownloadDoorstop")
    .Does(() =>
{
    Information("Downloading Doorstop");

    var doorstopPath = Directory("./bin/doorstop");
    var doorstopX64Path = doorstopPath + File("doorstop_x64.zip");
    var doorstopX86Path = doorstopPath + File("doorstop_x86.zip");
    CreateDirectory(doorstopPath);

    DownloadFile($"https://github.com/NeighTools/UnityDoorstop/releases/download/v{DOORSTOP_VER}/Doorstop_x64_{DOORSTOP_VER}.zip", doorstopX64Path);
    DownloadFile($"https://github.com/NeighTools/UnityDoorstop/releases/download/v{DOORSTOP_VER}/Doorstop_x86_{DOORSTOP_VER}.zip", doorstopX86Path);

    Information("Extracting Doorstop");
    ZipUncompress(doorstopX86Path, doorstopPath + Directory("x86"));
    ZipUncompress(doorstopX64Path, doorstopPath + Directory("x64"));
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

    void PackageBepin(string arch) 
    {
        var distArchDir = distDir + Directory(arch);
        var doorstopArchPath = doorstopPath + Directory(arch) + File(DOORSTOP_DLL);
        var bepinDir = distArchDir + Directory("BepInEx");
        
        CreateDirectory(distArchDir);
        CreateDirectory(bepinDir + Directory("core"));
        CreateDirectory(bepinDir + Directory("plugins"));
        CreateDirectory(bepinDir + Directory("patchers"));

        CopyFiles("./bin/*.*", bepinDir + Directory("core"));
        CopyFileToDirectory(doorstopArchPath, distArchDir);
        FileWriteText(distArchDir + File("changelog.txt"), changelog);
    }

    PackageBepin("x86");
    PackageBepin("x64");
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

    Information("Packing BepInEx.Patcher");
    ZipCompress(distDir + Directory("patcher"), distDir + File($"BepInEx_Patcher{commitPrefix}{buildVersion}.zip"));

    if(isBleedingEdge) 
    {
        FileWriteText(distDir + File("info.json"), 
            SerializeJsonPretty(new Dictionary<string, object>{
                ["id"] = buildId.ToString(),
                ["date"] = DateTime.Now.ToString("o"),
                ["changelog"] = "",
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
                        ["file"] = $"BepInEx_Patcher{commitPrefix}{buildVersion}.zip",
                        ["description"] = "Hardpatcher for BepInEx. IMPORTANT: USE ONLY IF DOORSTOP DOES NOT WORK FOR SOME REASON!"
                    }
                },
                ["commit"] = currentCommit
            }));
    }
});

RunTarget(target);