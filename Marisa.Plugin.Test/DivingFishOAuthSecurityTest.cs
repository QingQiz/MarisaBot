using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Marisa.Configuration;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.DivingFish;
using Marisa.Plugin.Shared.DivingFish;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

[TestFixture]
[NonParallelizable]
[Category("DivingFishOAuth")]
public class DivingFishOAuthSecurityTest
{
    private const int ConcurrentAttemptCount = 32;

    private static long _identitySeed = 8_000_000_000;

    private string _sourceConfigPath = null!;
    private string _tempRoot = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _sourceConfigPath = Path.Join(
            Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(),
            "Marisa.StartUp", "config.yaml");
        _tempRoot = Path.Combine(
            Path.GetTempPath(),
            "Marisa.Plugin.Test",
            nameof(DivingFishOAuthSecurityTest),
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);

        var escapedTempRoot = _tempRoot.Replace("\\", "\\\\");
        var configPath = Path.Combine(_tempRoot, "config.yaml");
        File.WriteAllText(configPath, $$"""
            tempPath: "{{escapedTempRoot}}"
            resourceRoot: ""
            databasePath: "oauth-security-test.db"
            divingFish:
              clientId: "test-client-id"
              clientSecret: "test-client-secret"
              redirectUri: "https://bot.example.test/oauth/callback/divingfish"
            """);

        ConfigurationManager.SetConfigFilePath(configPath);
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        ConfigurationManager.SetConfigFilePath(_sourceConfigPath);
        if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, true);
    }

    [TestCase("maimai", "prober.records.read")]
    [TestCase("chunithm", "chunithm.records.read")]
    public void ScopeOf_KnownGame_ReturnsExactScope(string game, string expected)
    {
        Assert.That(DivingFishOAuth.ScopeOf(game), Is.EqualTo(expected));
    }

    [Test]
    public void ScopeOf_UnknownGame_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DivingFishOAuth.ScopeOf("unknown"));
    }

    [Test]
    public void CallbackPath_Matches_Configured_RedirectUri()
    {
        Assert.That(DivingFishOAuth.CallbackPath, Is.EqualTo("/oauth/callback/divingfish"));
        Assert.That(DivingFishOAuth.CanAuthorize, Is.True);
    }

    [Test]
    public void SubjectRef_SameClientAndExternalId_IsStableLowercaseSha256()
    {
        const string expected = "7be34ed48f3de4511cfb3987c08091ea28d59cc5ba0695bce6a60c40dff1fa75";

        var first = DivingFishOAuth.SubjectRef("123456789");
        var second = DivingFishOAuth.SubjectRef("123456789");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(expected));
            Assert.That(second, Is.EqualTo(expected));
            Assert.That(first, Does.Match("^[0-9a-f]{64}$"));
            Assert.That(DivingFishOAuth.SubjectRef("123456788"), Is.Not.EqualTo(first));
        });
    }

    [Test]
    public async Task PendingAuth_ConcurrentAcquire_OnlyOneAttemptSucceeds()
    {
        var pending = DivingFishPendingAuth.Begin("maimai");

        var results = await RunConcurrently(() => DivingFishPendingAuth.AcquireForCallback(pending.State));
        var acquired = results.Where(x => x.IsAcquired).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(acquired, Has.Length.EqualTo(1));
            Assert.That(results.Count(x => x.Status == DivingFishPendingAuth.AcquireStatus.InProgress),
                Is.EqualTo(ConcurrentAttemptCount - 1));
        });

        var code = DivingFishBindingConfirmation.Issue(
            acquired[0].Entry!, "sub", "tester", DivingFishOAuth.ScopeOf("maimai"));
        Assert.That(code, Is.Not.Null);
        Assert.That(DivingFishBindingConfirmation.Consume(code!).Status,
            Is.EqualTo(DivingFishBindingConfirmation.ConsumeStatus.Success));
    }

    [Test]
    public async Task BindingConfirmation_ConcurrentConsume_OnlyOneAttemptSucceeds()
    {
        var code = IssueConfirmation("maimai");

        var results = await RunConcurrently(() => DivingFishBindingConfirmation.Consume(code));

        Assert.Multiple(() =>
        {
            Assert.That(results.Count(x => x.IsSuccess), Is.EqualTo(1));
            Assert.That(results.Count(x => x.Status == DivingFishBindingConfirmation.ConsumeStatus.NotFound),
                Is.EqualTo(ConcurrentAttemptCount - 1));
        });
    }

    [Test]
    public void PendingAuth_Does_Not_Supersede_Another_Flow()
    {
        var first = DivingFishPendingAuth.Begin("maimai");
        var second = DivingFishPendingAuth.Begin("chunithm");

        var firstAcquire = DivingFishPendingAuth.AcquireForCallback(first.State);
        var secondAcquire = DivingFishPendingAuth.AcquireForCallback(second.State);

        Assert.Multiple(() =>
        {
            Assert.That(firstAcquire.IsAcquired, Is.True);
            Assert.That(secondAcquire.IsAcquired, Is.True);
        });
    }

    [Test]
    public void BindingService_Allows_Same_Subject_For_Different_Qq()
    {
        var firstQq = Interlocked.Increment(ref _identitySeed);
        var secondQq = Interlocked.Increment(ref _identitySeed);
        var sub = $"shared-sub-{firstQq}";
        var scope = DivingFishOAuth.ScopeOf("maimai");

        DivingFishBindingService.Commit(firstQq, sub, "tester", scope, "maimai");
        DivingFishBindingService.Commit(secondQq, sub, "tester", scope, "maimai");

        using var realm = BotDbContext.OpenRealm();
        var bindings = realm.All<DivingFishOAuthBind>()
            .Where(x => x.Sub == sub)
            .ToList();
        Assert.That(bindings.Select(x => x.Qq), Is.EquivalentTo(new[] { firstQq, secondQq }));
    }

    private static string IssueConfirmation(string game)
    {
        var start = DivingFishPendingAuth.Begin(game);
        var acquired = DivingFishPendingAuth.AcquireForCallback(start.State);
        Assert.That(acquired.IsAcquired, Is.True);

        var code = DivingFishBindingConfirmation.Issue(
            acquired.Entry!, "sub", "tester", DivingFishOAuth.ScopeOf(game));
        Assert.That(code, Is.Not.Null);
        return code!;
    }

    private static async Task<T[]> RunConcurrently<T>(Func<T> action)
    {
        using var ready = new CountdownEvent(ConcurrentAttemptCount);
        using var start = new ManualResetEventSlim(false);

        var tasks = Enumerable.Range(0, ConcurrentAttemptCount)
            .Select(_ => Task.Factory.StartNew(() =>
                {
                    ready.Signal();
                    start.Wait();
                    return action();
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        var allWorkersReady = ready.Wait(TimeSpan.FromSeconds(20));
        start.Set();
        var results = await Task.WhenAll(tasks);
        Assert.That(allWorkersReady, Is.True, "并发测试 worker 启动超时");
        return results;
    }
}
