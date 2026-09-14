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
    public void DeviceCodeMode_IsAvailable_With_Confidential_Client()
    {
        Assert.That(DivingFishOAuth.CanUseDeviceCode, Is.True);
    }

    [Test]
    public void DeviceSubjectRef_Is_Bare_Lowercase_Sha256()
    {
        var subjectRef = DivingFishOAuth.DeviceSubjectRef(123456789);

        Assert.That(subjectRef, Does.Match("^[0-9a-f]{64}$"));
        Assert.That(subjectRef, Does.Not.StartWith("ref:"));
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
    public void DeviceBindingConfirmation_Only_Initiator_Can_Consume()
    {
        const long initiator = 123456789;
        const long otherUser = 987654321;
        var code = DivingFishDeviceBindingConfirmation.Issue(
            initiator,
            "waterfish-sub",
            "maimai",
            DivingFishOAuth.ScopeOf("maimai"));

        Assert.That(
            DivingFishDeviceBindingConfirmation.Consume(code, otherUser).Status,
            Is.EqualTo(DivingFishDeviceBindingConfirmation.ConsumeStatus.WrongUser));
        Assert.That(
            DivingFishDeviceBindingConfirmation.Consume(code, initiator).IsSuccess,
            Is.True);
    }

    [Test]
    public async Task DeviceBindingConfirmation_ConcurrentConsume_OnlyOne_Succeeds()
    {
        const long initiator = 123456789;
        var code = DivingFishDeviceBindingConfirmation.Issue(
            initiator,
            "waterfish-sub",
            "maimai",
            DivingFishOAuth.ScopeOf("maimai"));

        var results = await RunConcurrently(() =>
            DivingFishDeviceBindingConfirmation.Consume(code, initiator));

        Assert.That(results.Count(x => x.IsSuccess), Is.EqualTo(1));
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
