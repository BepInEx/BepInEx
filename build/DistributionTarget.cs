using System;

readonly record struct DistributionTarget(string DistributionIdentifier, string RuntimeIndentifier)
{
    public readonly string Arch = RuntimeIndentifier.Split('-')[1];
    public readonly string Engine = DistributionIdentifier.Split('.')[0];
    public readonly string Os = RuntimeIndentifier.Split('-')[0];
    public readonly string Runtime = DistributionIdentifier.Split('.')[1];
    public readonly string Target = $"{DistributionIdentifier}-{RuntimeIndentifier}";

    public string ClearOsName => Os switch
    {
        "win"   => "Windows",
        "linux" => "Linux",
        "macos" => "macOS",
        var _   => throw new NotSupportedException($"OS {Os} is not supported")
    };

    public string DllExtension => Os switch
    {
        "win"   => "dll",
        "linux" => "so",
        "macos" => "dylib",
        var _   => throw new NotSupportedException($"Unsupported OS: {Os}")
    };

    public string DllPrefix => Os switch
    {
        "win"   => "",
        "linux" => "lib",
        "macos" => "lib",
        _       => throw new NotSupportedException($"Unsupported OS: {Os}")
    };
}
