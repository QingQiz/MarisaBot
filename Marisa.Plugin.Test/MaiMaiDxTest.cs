using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
    private MaiMaiDx.MaiMaiDx _maiMaiDx;
    private SongDb<MaiMaiSong> _songDb;

    [SetUp]
    public void Setup()
    {
        var configPath = Path.Join(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(), "Marisa.StartUp", "config.yaml");
        ConfigurationManager.SetConfigFilePath(configPath);

        _allNet   = new AllNetDataFetcher(_songDb);
        _maiMaiDx = new MaiMaiDx.MaiMaiDx();

        var field = typeof(MaiMaiDx.MaiMaiDx).GetField("_songDb", BindingFlags.NonPublic | BindingFlags.Instance)!;

        _songDb = (SongDb<MaiMaiSong>)field.GetValue(_maiMaiDx)!;
        _allNet = new AllNetDataFetcher(_songDb);
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
}