using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;
using Marisa.Plugin.Shared.Util.SongDb;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiDxTest
{
    private AllNetDataFetcher _allNet;
    private DivingFishDataFetcher _divingFish = null!;
    private MaiMaiDx.MaiMaiDx _maiMaiDx;
    private SongDb<MaiMaiSong> _songDb;

    [SetUp]
    public void Setup()
    {
        var configPath = Path.Join(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(), "Marisa.StartUp", "config.yaml");
        ConfigurationManager.SetConfigFilePath(configPath);

        _maiMaiDx = new MaiMaiDx.MaiMaiDx();
        _songDb = _maiMaiDx.SongDb;
        _allNet = new AllNetDataFetcher(_songDb);
        _divingFish = new DivingFishDataFetcher(_songDb);
    }


    [Test]
    public void All_Song_Should_Have_Cover()
    {
        Console.WriteLine(ResourceManager.ResourcePath);
        var res = _songDb.SongList.Where(s =>
        {
            var p = Path.Join(ResourceManager.ResourcePath, "cover", s.Id.ToString());
            return !(File.Exists(p + ".jpg") || File.Exists(p + ".png") || File.Exists(p + ".jpeg"));
        });

        Assert.IsEmpty(res);
    }

    [Test]
    [TestCase(920759985)] // Laplaze
    public async Task Score_Should_Be_Fetched(long qq)
    {
        var m = new Message(null!, [])
        {
            Sender = new SenderInfo(qq, "test")
        };

        var res = await _allNet.GetRating(m);

        Assert.That(res.Rating > 0);
    }

    [Test]
    public async Task DivingFish_Should_Fetch_Rating_By_Username_When_DevToken_Configured()
    {
        try
        {
            _ = ConfigurationManager.Configuration.DivingFish.DevToken;
        }
        catch (MissingConfigurationException)
        {
            Assert.Ignore("divingFish.devToken is not configured.");
        }

        var m = new Message(null!, [])
        {
            Sender = new SenderInfo(1, "test"),
            Command = "laplaze".AsMemory()
        };

        var res = await _divingFish.GetRating(m);

        Assert.That(res.Nickname, Is.Not.Empty);
        Assert.That(res.Rating, Is.GreaterThan(0));
    }
}
