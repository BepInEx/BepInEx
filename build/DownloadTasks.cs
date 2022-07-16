using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Nuke.Common.IO;
using ShellProgressBar;

static class DownloadTasks
{
    public static void DownloadFiles(string name, params (string Name, string Url, AbsolutePath Destination)[] files)
    {
        using var progress = new ProgressBar(files.Length, $"Downloading {name}");
        Task.WaitAll(files.Select(t => DownloadFile(progress, t.Name, t.Url, t.Destination)).ToArray());
    }

    public static void DownloadZipFiles(string name, params (string Name, string Url, AbsolutePath Destination)[] files)
    {
        using var progress = new ProgressBar(files.Length, $"Downloading {name}");
        Task.WaitAll(files.Select(async t =>
        {
            var zipFilePath = (AbsolutePath) $"{t.Destination}_tmp.zip";
            await DownloadFile(progress, t.Name, t.Url, zipFilePath);
            await UnzipFile(progress, t.Name, zipFilePath, t.Destination);
            File.Delete(zipFilePath!);
        }).ToArray());
    }

    static async Task DownloadFile(ProgressBarBase bar, string name, string url, AbsolutePath destination)
    {
        using var client = new HttpClient();
        await using var fs = File.Create(destination);
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode)
            throw new Exception($"Failed to download {name}");
        await using var stream = await response.Content.ReadAsStreamAsync();
        using var childBar = bar.Spawn((int) (response.Content.Headers.ContentLength ?? 1), $"Downloading {name}");

        var buffer = new byte[4096];
        int read;
        while ((read = await stream.ReadAsync(buffer)) > 0)
        {
            await fs.WriteAsync(buffer.AsMemory(0, read));
            childBar.Tick(childBar.CurrentTick + read);
        }
    }

    static async Task UnzipFile(ProgressBarBase bar, string name, AbsolutePath zipFile, AbsolutePath destination)
    {
        using var zip = ZipFile.OpenRead(zipFile);
        using var childBar = bar.Spawn((int) zip.Entries.Select(e => e.Length).Sum(), $"Extracting {name}");

        foreach (var entry in zip.Entries)
        {
            // Use Path.Combine to ensure the trailing slash is preserved.
            var fullPath = Path.Combine(destination, entry.FullName);
            var fileName = Path.GetFileName(fullPath);
            if (string.IsNullOrEmpty(fileName))
            {
                Directory.CreateDirectory(fullPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException());
            await using var fs = File.Create(fullPath);
            await using var stream = entry.Open();
            var buffer = new byte[4096];
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                await fs.WriteAsync(buffer.AsMemory(0, read));
                childBar.Tick(childBar.CurrentTick + read);
            }
        }
    }
}
