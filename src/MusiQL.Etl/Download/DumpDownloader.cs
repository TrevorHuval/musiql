using System.Security.Cryptography;

namespace MusiQL.Etl.Download;

public sealed class DumpDownloader(string baseUrl, string outputDirectory, Action<string> log)
{
    public const string DefaultBaseUrl = "https://data.metabrainz.org/pub/musicbrainz/data/fullexport";

    private static readonly string[] Tarballs = ["mbdump.tar.bz2", "mbdump-derived.tar.bz2"];

    public async Task<IReadOnlyList<string>> DownloadAsync()
    {
        Directory.CreateDirectory(outputDirectory);
        using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        var export = (await http.GetStringAsync($"{baseUrl}/LATEST")).Trim();
        var exportUrl = $"{baseUrl}/{export}";
        log($"latest full export: {export}");

        var checksums = ParseMd5Sums(await http.GetStringAsync($"{exportUrl}/MD5SUMS"));
        var paths = new List<string>();

        foreach (var name in Tarballs)
        {
            var destination = Path.Combine(outputDirectory, name);
            checksums.TryGetValue(name, out var expected);

            if (File.Exists(destination) && expected is not null && FileMd5(destination) == expected)
            {
                log($"{name}: present and verified");
            }
            else
            {
                await DownloadFileAsync(http, $"{exportUrl}/{name}", destination);
                if (expected is not null && FileMd5(destination) != expected)
                {
                    throw new InvalidOperationException($"checksum mismatch for {name}");
                }

                log($"{name}: downloaded and verified");
            }

            paths.Add(destination);
        }

        return paths;
    }

    private async Task DownloadFileAsync(HttpClient http, string url, string destination)
    {
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        var total = response.Content.Headers.ContentLength;

        await using var source = await response.Content.ReadAsStreamAsync();
        await using var target = File.Create(destination);

        var buffer = new byte[1 << 20];
        long copied = 0;
        var nextReport = 0L;
        int read;
        while ((read = await source.ReadAsync(buffer)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read));
            copied += read;
            if (copied >= nextReport)
            {
                log($"  {Path.GetFileName(destination)}: {Gib(copied)}{(total is { } t ? $" / {Gib(t)}" : "")}");
                nextReport = copied + (256L << 20);
            }
        }
    }

    private static Dictionary<string, string> ParseMd5Sums(string content)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var name = Path.GetFileName(parts[^1].TrimStart('*', '.', '/'));
            result[name] = parts[0];
        }

        return result;
    }

    private static string FileMd5(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(MD5.HashData(stream));
    }

    private static string Gib(long bytes) => $"{bytes / (double)(1 << 30):F2} GiB";
}
