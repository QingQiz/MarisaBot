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
using Marisa.Plugin.Shared.DivingFish;
using Marisa.Plugin.Shared.Lxns;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

[TestFixture, NonParallelizable]
public class ChunithmVersusPlayerTest
{
    private const long Self = 9201;
    private const long Opponent = 9202;
    private string _directory = null!;
    private string _originalConfig = null!;
    private Chunithm.Chunithm _plugin = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), nameof(ChunithmVersusPlayerTest), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _originalConfig = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../..", "Marisa.StartUp/config.yaml"));
        Configure(true);
        _plugin = new Chunithm.Chunithm();
    }

    [TearDown]
    public void TearDown()
    {
        ConfigurationManager.SetConfigFilePath(_originalConfig);
        Directory.Delete(_directory, true);
    }

    [Test]
    public void OpponentUsesOwnLxnsAuthorizationAndKeepsNonSelfIdentity()
    {
        Bind(Opponent, "lxns");
        LxnsTokenStore.SaveToken(Opponent, "opponent-access", "opponent-refresh", 3600);
        var result = Resolve(Opponent);
        Assert.Multiple(() =>
        {
            Assert.That(result.Error, Is.Null);
            Assert.That(result.Player!.Qq, Is.EqualTo(Opponent));
            Assert.That(result.Player.IsSelf, Is.False);
            Assert.That(result.Player.Username, Is.Null);
            Assert.That(result.Player.Fetcher, Is.TypeOf<LxnsDataFetcher>());
        });
    }

    [Test]
    public void SenderAuthorizationDoesNotAuthorizeOpponent()
    {
        Bind(Opponent, "lxns");
        LxnsTokenStore.SaveToken(Self, "self-access", "self-refresh", 3600);
        Assert.That(Resolve(Opponent).Player, Is.Null);
    }

    [Test]
    public void MaimaiAuthorizationDoesNotAuthorizeChunithm()
    {
        Authorize(Opponent, "maimai");
        Assert.That(Resolve(Opponent).Player, Is.Null);
        Authorize(Opponent, "chunithm");
        var result = Resolve(Opponent);
        Assert.That(result.Player!.Fetcher, Is.TypeOf<DivingFishDataFetcher>());
        Assert.That(result.Player.IsSelf, Is.False);
        Assert.That(result.Error, Is.Null);
    }

    [TestCase("Louis")]
    [TestCase("Aqua")]
    [TestCase("RinNET")]
    [TestCase("example.invalid")]
    public void UnsupportedBindingsAreRejectedBeforeConstructingFetcher(string server)
    {
        Bind(Opponent, server);
        var result = Resolve(Opponent);
        Assert.That(result.Player, Is.Null);
        Assert.That(result.Error, Does.Contain("仅支持水鱼和落雪"));
    }

    [Test]
    public void UnconfiguredOAuthKeepsExistingDivingFishDevMode()
    {
        Configure(false);
        Assert.That(Resolve(Opponent).Player!.Fetcher, Is.TypeOf<DivingFishDataFetcher>());
    }

    private void Configure(bool oauth)
    {
        var config = Path.Combine(_directory, "config.yaml");
        File.WriteAllText(config, $$"""
            tempPath: '{{_directory}}'
            databasePath: versus-player-test.db
            divingFish:
              clientId: {{(oauth ? "test-client" : "")}}
              clientSecret: {{(oauth ? "test-secret" : "")}}
            """);
        ConfigurationManager.SetConfigFilePath(config);
    }

    private static void Bind(long qq, string server)
    {
        using var realm = BotDbContext.OpenRealm();
        realm.Write(() => realm.AddWithAutoId(new ChunithmBind(qq, server)));
    }

    private static void Authorize(long qq, string game) => DivingFishTokenStore.SaveAuthorization(qq, game, "versus-sub",
        new DivingFishToken { AccessToken = "synthetic-access", Scope = DivingFishOAuth.ScopeOf(game), ExpiresAt = DateTime.UtcNow.AddHours(1) });

    private (ResolvedPlayer? Player, string? Error) Resolve(long qq)
    {
        var message = new Message(new MessageChain(new MessageDataText("")), null!)
            { Sender = new SenderInfo(Self, "tester"), Type = MessageType.GroupMessage };
        return ((ResolvedPlayer?, string?))typeof(Chunithm.Chunithm)
            .GetMethod("ResolveVersusPlayer", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(_plugin, [message, qq])!;
    }
}
