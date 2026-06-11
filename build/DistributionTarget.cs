using System;

readonly struct DistributionTarget
{
    public DistributionTarget(string distributionIdentifier, string runtimeIdentifier)
    {
        DistributionIdentifier = distributionIdentifier;
        RuntimeIdentifier = runtimeIdentifier;
        FrameworkTarget = null;

        Os = RuntimeIdentifier.Split('-')[0];
        Arch = RuntimeIdentifier.Split('-')[1];
        Engine = DistributionIdentifier.Split('.')[0];
        Runtime = DistributionIdentifier.Split('.')[1];
        Target = $"{DistributionIdentifier}-{RuntimeIdentifier}";
    }

    public DistributionTarget(string distributionIdentifier, string runtimeIdentifier, string frameworkTarget) : this(distributionIdentifier, runtimeIdentifier)
    {
        FrameworkTarget = frameworkTarget;
        Target = $"{DistributionIdentifier}-{FrameworkTarget}-{RuntimeIdentifier}";
    }

    public readonly string DistributionIdentifier;
    public readonly string RuntimeIdentifier;
    public readonly string FrameworkTarget;

    public readonly string Arch;
    public readonly string Engine;
    public readonly string Os;
    public readonly string Runtime;
    public readonly string Target;

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
