using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Flurl.Http;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.Osu;
using Marisa.Plugin.Shared.Osu.Entity.Score;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class OsuTest
{
    [SetUp]
    public void Setup()
    {
        var configPath = Path.Join(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(), "Marisa.StartUp", "config.yaml");
        ConfigurationManager.SetConfigFilePath(configPath);
    }

    [Test]
    [TestCase(1794551, "1794551 25-ji, Night Code de x Hatsune Miku - Jishou Mushoku.osz")]
    public async Task Beatmap_Should_Be_Downloaded(long beatmapId, string filename)
    {
        if (File.Exists(filename))
        {
            File.Delete(filename);
        }

        Assert.IsFalse(File.Exists(filename));

        await OsuApi.DownloadBeatmap(beatmapId, Environment.CurrentDirectory);

        Assert.IsTrue(File.Exists(filename));
    }

    [Test]
    [TestCase(1931476, 4001513)]
    public void Beatmap_Should_Be_Got_By_Id(long beatmapsetId, long beatmapId)
    {
        Assert.DoesNotThrow(() => OsuApi.GetBeatmapPathByBeatmapId(beatmapsetId, beatmapId));
    }

    [Test]
    [TestCase(4001513)]
    public async Task BeatmapCover_Should_Be_Got(long beatmapId)
    {
        var result = await OsuApi.Request("https://osu.ppy.sh/api/v2/users/16265882/scores/best?include_fails=0&mode=mania&limit=1&offset=0").GetStringAsync();
        var score  = OsuScore.FromJson(result)!.First(x => x.Beatmap.Id == beatmapId);
        var cover  = score.Beatmap.TryGetCover();
        Assert.NotNull(cover);
        Assert.IsTrue(File.Exists(cover));
    }
    //
    // [Test]
    // public async Task T()
    // {
    //     var result = await OsuApi.Request("https://osu.ppy.sh/api/v2/users/16265882/scores/best?include_fails=0&mode=mania&limit=1&offset=0").GetStringAsync();
    //     
    //     await File.WriteAllTextAsync(@"C:\Users\sofee\Desktop\score.json", result);
    //
    //     result = await OsuApi.Request("https://osu.ppy.sh/api/v2/beatmaps/4001513").GetStringAsync();
    //     await File.WriteAllTextAsync(@"C:\Users\sofee\Desktop\beatmap.json", result);
    // }
}