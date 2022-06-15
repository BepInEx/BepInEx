using System;
using System.Diagnostics;
using System.IO;

namespace BepInEx.IL2CPP.Utils;

internal static class NotifySend
{
    private const string EXECUTABLE_NAME = "notify-send";

    private static string Find(string fileName)
    {
        if (File.Exists(fileName))
            return Path.GetFullPath(fileName);

        var paths = Environment.GetEnvironmentVariable("PATH");
        if (paths == null)
            return null;

        foreach (var path in paths.Split(Path.PathSeparator))
        {
            var fullPath = Path.Combine(path, fileName);
            if (File.Exists(fullPath))
                return fullPath;
        }

        return null;
    }

    public static bool IsSupported => Find(EXECUTABLE_NAME) != null;

    public static void Send(string summary, string body)
    {
        if (!IsSupported) throw new NotSupportedException();

        var processStartInfo = new ProcessStartInfo(Find(EXECUTABLE_NAME))
        {
            ArgumentList =
            {
                summary,
                body,
                "--app-name=BepInEx",
            },
        };

        Process.Start(processStartInfo);
    }
}
