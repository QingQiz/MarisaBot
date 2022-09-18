using System.Diagnostics;
using System.IO.Compression;
using System.Net.NetworkInformation;
using System.Text.RegularExpressions;
using Flurl.Http;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.Osu.Drawer;
using Marisa.Plugin.Shared.Osu.Entity.Score;
using Marisa.Utils;

namespace Marisa.Plugin.Shared.Osu;

public static class PerformanceCalculator
{
    private static string PpCalculator => ConfigurationManager.Configuration.Osu.PpCalculator;

    private static Func<long, string> BeatmapsetPath => beatmapsetId => Path.Join(OsuDrawerCommon.TempPath, "beatmap", beatmapsetId.ToString());

    private static string GetBeatmapPath(long beatmapsetId, string checksum)
    {
        var path = BeatmapsetPath(beatmapsetId);

        foreach (var f in Directory.GetFiles(path, "*.osu"))
        {
            var hash = File.ReadAllText(f).GetMd5Hash();

            if (hash.Equals(checksum, StringComparison.OrdinalIgnoreCase))
            {
                return f;
            }
        }

        throw new FileNotFoundException($"Can not find beatmap with MD5 {checksum}");
    }

    private static async Task<string> GetBeatmapPath(Beatmap beatmap)
    {
        var path = BeatmapsetPath(beatmap.BeatmapsetId);

        if (Directory.Exists(path))
        {
            return GetBeatmapPath(beatmap.BeatmapsetId, beatmap.Checksum);
        }

        var download = await $"https://dl.sayobot.cn/beatmaps/download/mini/{beatmap.BeatmapsetId}"
            .WithHeader("Referer", "https://github.com/QingQiz/MarisaBot")
            .WithHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/105.0.0.0 Safari/537.36")
            .DownloadFileAsync(Path.GetDirectoryName(path));

        ZipFile.ExtractToDirectory(download, path);

        return GetBeatmapPath(beatmap.BeatmapsetId, beatmap.Checksum);
    }

    public static async Task<double> GetPerformance(OsuScore score)
    {
        var path = await GetBeatmapPath(score.Beatmap);

        // TODO std taiko catch
        var argument = score.ModeInt switch
        {
            3 => $"simulate mania \"{path}\" -s {score.Score} -j",
            _ => throw new NetworkInformationException()
        };

        argument += string.Join("", score.Mods.Select(m => $" -m {m}"));

        using var p = new Process();

        p.StartInfo.UseShellExecute        = false;
        p.StartInfo.CreateNoWindow         = true;
        p.StartInfo.FileName               = PpCalculator;
        p.StartInfo.Arguments              = argument;
        p.StartInfo.RedirectStandardOutput = true;

        p.Start();
        await p.WaitForExitAsync();

        var json = await p.StandardOutput.ReadToEndAsync();

        var regex = new Regex(@"""pp"":(.*?)}");

        return double.Parse(regex.Match(json).Groups[1].Value);
    }
}