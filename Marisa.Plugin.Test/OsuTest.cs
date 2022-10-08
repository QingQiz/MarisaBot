using System;
using System.IO;
using System.Threading.Tasks;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.Osu;
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
}