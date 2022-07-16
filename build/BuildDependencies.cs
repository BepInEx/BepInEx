static class BuildDependencies
{
    public const string DoorstopVersion = "4.0.0-rc.5";
    public const string DotnetRuntimeVersion = "6.0.6-test";
    public const string DobbyVersion = "1.0.0";

    public const string DotnetRuntimeZipUrl =
        $"https://github.com/BepInEx/dotnet-runtime/releases/download/{DotnetRuntimeVersion}/mini-coreclr-Release.zip";

    public static string DoorstopZipUrl(string arch) =>
        $"https://github.com/NeighTools/UnityDoorstop/releases/download/v{DoorstopVersion}/doorstop_{arch}_release_{DoorstopVersion}.zip";

    public static string DobbyZipUrl(string arch) =>
        $"https://github.com/BepInEx/Dobby/releases/download/v{DobbyVersion}/dobby-{arch}.zip";
}
