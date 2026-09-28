using System;
using System.IO;
using System.Reflection;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.Configuration;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.Chunithm;
using Marisa.Plugin.Shared.Chunithm.DataFetcher;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

/// <summary>
///     插件层解析查询目标（Chunithm.ResolvePlayer）：查谁、用哪家、命令文本算不算账号名，
///     都在这里一次判定，fetcher 不再自己从消息里反推目标。
/// </summary>
[TestFixture]
[NonParallelizable]
public class ChunithmResolvePlayerTest
{
    private const long Qq      = 9001;
    private const long OtherQq = 9002;

    private string _testRoot = null!;
    private string _sourceConfig = null!;
    private Chunithm.Chunithm _plugin = null!;

    [SetUp]
    public void SetUp()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), nameof(ChunithmResolvePlayerTest), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _sourceConfig = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../..", "Marisa.StartUp/config.yaml"));

        UseConfig();
        _plugin = new Chunithm.Chunithm();
    }

    [TearDown]
    public void TearDown()
    {
        ConfigurationManager.SetConfigFilePath(_sourceConfig);
        Directory.Delete(_testRoot, true);
    }

    [Test]
    public void EmptyCommand_ResolvesToSelf_WithBindFetcher()
    {
        Bind(Qq, "DivingFish");

        var player = Resolve("");

        Assert.Multiple(() =>
        {
            Assert.That(player.Qq, Is.EqualTo(Qq));
            Assert.That(player.Username, Is.Null);
            Assert.That(player.IsSelf, Is.True);
            Assert.That(player.Fetcher, Is.TypeOf<DivingFishDataFetcher>());
        });
    }

    [Test]
    public void Unbound_ResolvesToDivingFish()
    {
        var player = Resolve("");

        Assert.Multiple(() =>
        {
            Assert.That(player.Fetcher, Is.TypeOf<DivingFishDataFetcher>(), "没绑定记录时按水鱼处理");
            Assert.That(player.IsSelf, Is.True);
        });
    }

    [Test]
    public void EmptyCommand_Uses_Bound_Louis()
    {
        Bind(Qq, "Louis");

        var player = Resolve("");

        Assert.That(player.Fetcher, Is.TypeOf<LouisDataFetcher>());
    }

    [Test]
    public void Mention_Wins_Over_Command_And_Uses_Target_Bind()
    {
        Bind(OtherQq, "lxns");

        var player = Resolve("14+", allowUsername: true, atQq: OtherQq);

        Assert.Multiple(() =>
        {
            Assert.That(player.Qq, Is.EqualTo(OtherQq), "@ 的目标优先于命令文本");
            Assert.That(player.Username, Is.Null, "有 @ 时命令文本不当账号名");
            Assert.That(player.IsSelf, Is.False);
            Assert.That(player.Fetcher, Is.TypeOf<LxnsDataFetcher>(), "按 @ 目标的绑定选查分器");
        });
    }

    [Test]
    public void Mention_Self_Counts_As_Self()
    {
        Bind(Qq, "Louis");

        var player = Resolve("", atQq: Qq);

        Assert.Multiple(() =>
        {
            Assert.That(player.Qq, Is.EqualTo(Qq));
            Assert.That(player.IsSelf, Is.True, "@ 自己仍是本人，可以用本人票据");
            Assert.That(player.Fetcher, Is.TypeOf<LouisDataFetcher>());
        });
    }

    [Test]
    public void Command_Is_Username_Only_When_Allowed()
    {
        // 发言者绑的是落雪，账号名查询仍然走水鱼（只有水鱼有按账号名查的公开接口）
        Bind(Qq, "lxns");

        var asUsername  = Resolve("target", allowUsername: true);
        var notUsername = Resolve("14+", allowUsername: false);

        Assert.Multiple(() =>
        {
            Assert.That(asUsername.Username, Is.EqualTo("target"));
            Assert.That(asUsername.Qq, Is.EqualTo(Qq));
            Assert.That(asUsername.IsSelf, Is.False, "账号名不代表本人，拿不到本人票据");
            Assert.That(asUsername.Fetcher, Is.TypeOf<DivingFishDataFetcher>());

            Assert.That(notUsername.Username, Is.Null, "命令参数（等级/定数等）不当账号名");
            Assert.That(notUsername.IsSelf, Is.True);
            Assert.That(notUsername.Fetcher, Is.TypeOf<LxnsDataFetcher>());
        });
    }

    private void UseConfig()
    {
        var configPath = Path.Combine(_testRoot, "config.yaml");
        File.WriteAllText(configPath, $$"""
            tempPath: '{{_testRoot}}'
            databasePath: resolve-player-test.db
            """);
        ConfigurationManager.SetConfigFilePath(configPath);
    }

    private static void Bind(long qq, string server)
    {
        using var realm = BotDbContext.OpenRealm();
        var tr = realm.BeginWrite();
        realm.AddWithAutoId(new ChunithmBind(qq, server));
        tr.Commit();
    }

    private ResolvedPlayer Resolve(string command, bool allowUsername = false, long atQq = 0)
    {
        var method = typeof(Chunithm.Chunithm).GetMethod("ResolvePlayer", BindingFlags.NonPublic | BindingFlags.Instance)!;

        return (ResolvedPlayer)method.Invoke(_plugin, [Message(command, atQq), allowUsername])!;
    }

    private static Message Message(string command, long atQq = 0)
    {
        var chain = atQq == 0
            ? new MessageChain(new MessageDataText(command.AsMemory()))
            : new MessageChain(new MessageDataAt(atQq), new MessageDataText(command.AsMemory()));

        return new Message(chain, null!) { Sender = new SenderInfo(Qq, "tester"), Command = command.AsMemory() };
    }
}
