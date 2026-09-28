using System;
using System.IO;
using System.Reflection;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.Configuration;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.DivingFish;
using Marisa.Plugin.Shared.Lxns;
using Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

/// <summary>
///     插件层解析查询目标（MaiMaiDx.ResolvePlayer / ResolveVersusPlayer）：查谁、用哪家、凭据够不够，
///     都在这里一次判定，fetcher 不再自己从消息里反推目标。
/// </summary>
[TestFixture]
[NonParallelizable]
public class MaiMaiResolvePlayerTest
{
    private const long Qq      = 9001;
    private const long OtherQq = 9002;

    private string _testRoot = null!;
    private string _sourceConfig = null!;
    private MaiMaiDx.MaiMaiDx _plugin = null!;

    [SetUp]
    public void SetUp()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), nameof(MaiMaiResolvePlayerTest), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _sourceConfig = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../..", "Marisa.StartUp/config.yaml"));

        UseConfig(divingFishOAuth: true);
        _plugin = new MaiMaiDx.MaiMaiDx();
    }

    [TearDown]
    public void TearDown()
    {
        ConfigurationManager.SetConfigFilePath(_sourceConfig);
        Directory.Delete(_testRoot, true);
    }

    #region ResolvePlayer

    [Test]
    public void EmptyCommand_ResolvesToSelf()
    {
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
    public void Mention_Wins_Over_Command_And_Uses_Target_Bind()
    {
        Bind(OtherQq, "lxns");
        LxnsTokenStore.SaveToken(OtherQq, "access", "refresh", 3600);

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
    public void Command_Is_Username_Only_When_Allowed()
    {
        // 发言者绑的是落雪，账号名查询仍然走水鱼（只有水鱼有按账号名查的公开接口）
        Bind(Qq, "lxns");

        var asUsername = Resolve("target", allowUsername: true);
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

    #endregion

    #region ResolveVersusPlayer

    [Test]
    public void Versus_LxnsBindWithoutToken_IsUnavailable()
    {
        Bind(Qq, "lxns");

        var (player, error) = ResolveVersus(Qq);

        Assert.Multiple(() =>
        {
            Assert.That(player, Is.Null);
            Assert.That(error, Is.EqualTo(LxnsDataFetcher.NotBoundHint));
        });
    }

    [Test]
    public void Versus_LxnsBindWithToken_UsesLxnsFetcher()
    {
        Bind(Qq, "lxns");
        LxnsTokenStore.SaveToken(Qq, "access", "refresh", 3600);

        var (player, error) = ResolveVersus(Qq);

        Assert.Multiple(() =>
        {
            Assert.That(player!.Fetcher, Is.TypeOf<LxnsDataFetcher>());
            Assert.That(error, Is.Null);
        });
    }

    [Test]
    public void Versus_DivingFishAuthorization_UsesDivingFishFetcher()
    {
        Bind(Qq, "DivingFish");
        DivingFishTokenStore.SaveAuthorization(Qq, "maimai", "sub-a", new DivingFishToken
        {
            AccessToken = "ticket",
            Scope = DivingFishOAuth.ScopeOf("maimai"),
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        });

        var (player, error) = ResolveVersus(Qq);

        Assert.Multiple(() =>
        {
            Assert.That(player!.Fetcher, Is.TypeOf<DivingFishDataFetcher>());
            Assert.That(error, Is.Null);
        });
    }

    [Test]
    public void Versus_UnboundWithDivingFishOAuth_IsUnavailable()
    {
        var (player, error) = ResolveVersus(Qq);

        Assert.Multiple(() =>
        {
            Assert.That(player, Is.Null);
            Assert.That(error, Is.EqualTo(DivingFishDataFetcher.NotBoundHint));
        });
    }

    [Test]
    public void Versus_UnboundWithoutDivingFishOAuth_UsesDivingFishFetcher()
    {
        UseConfig(divingFishOAuth: false);

        var (player, error) = ResolveVersus(Qq);

        Assert.Multiple(() =>
        {
            Assert.That(player!.Fetcher, Is.TypeOf<DivingFishDataFetcher>(), "dev token 模式按 QQ 查，不依赖用户票据");
            Assert.That(error, Is.Null);
        });
    }

    [Test]
    public void Versus_WahlapBind_IsRejected()
    {
        Bind(Qq, "Wahlap");

        var (player, error) = ResolveVersus(Qq);

        Assert.Multiple(() =>
        {
            Assert.That(player, Is.Null);
            Assert.That(error, Does.Contain("不支持 vs"));
        });
    }

    #endregion

    private void UseConfig(bool divingFishOAuth)
    {
        var configPath = Path.Combine(_testRoot, "config.yaml");
        File.WriteAllText(configPath, $$"""
            tempPath: '{{_testRoot}}'
            databasePath: resolve-player-test.db
            divingFish:
              clientId: {{(divingFishOAuth ? "test-client" : "")}}
              clientSecret: {{(divingFishOAuth ? "test-secret" : "")}}
            """);
        ConfigurationManager.SetConfigFilePath(configPath);
    }

    private static void Bind(long qq, string server)
    {
        using var realm = BotDbContext.OpenRealm();
        var tr = realm.BeginWrite();
        realm.AddWithAutoId(new MaiMaiDxBind(qq, 0) { ServerName = server });
        tr.Commit();
    }

    private ResolvedPlayer Resolve(string command, bool allowUsername = false, long atQq = 0)
    {
        var method = typeof(MaiMaiDx.MaiMaiDx).GetMethod("ResolvePlayer", BindingFlags.NonPublic | BindingFlags.Instance)!;

        return (ResolvedPlayer)method.Invoke(_plugin, [Message(command, atQq), allowUsername])!;
    }

    private (ResolvedPlayer? Player, string? Error) ResolveVersus(long qq)
    {
        var method = typeof(MaiMaiDx.MaiMaiDx).GetMethod("ResolveVersusPlayer", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var result = method.Invoke(_plugin, [Message(""), qq])!;
        var type   = result.GetType();

        return ((ResolvedPlayer?)type.GetField("Item1")!.GetValue(result),
                (string?)type.GetField("Item2")!.GetValue(result));
    }

    private static Message Message(string command, long atQq = 0)
    {
        var chain = atQq == 0
            ? new MessageChain(new MessageDataText(command.AsMemory()))
            : new MessageChain(new MessageDataAt(atQq), new MessageDataText(command.AsMemory()));

        return new Message(chain, null!) { Sender = new SenderInfo(Qq, "tester"), Command = command.AsMemory() };
    }
}
