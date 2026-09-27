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
    public void BindingService_Allows_Same_Subject_For_Different_Qq()
    {
        var firstQq = Interlocked.Increment(ref _identitySeed);
        var secondQq = Interlocked.Increment(ref _identitySeed);
        var sub = $"shared-sub-{firstQq}";
        var token = new DivingFishToken
        {
            AccessToken = "token",
            Scope = DivingFishOAuth.ScopeOf("maimai"),
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        DivingFishBindingService.Commit(firstQq, sub, token, "maimai");
        DivingFishBindingService.Commit(secondQq, sub, token, "maimai");

        using var realm = BotDbContext.OpenRealm();
        var rows = realm.All<DivingFishAuthToken>()
            .Where(x => x.Sub == sub)
            .ToList();
        Assert.That(rows.Select(x => x.Qq), Is.EquivalentTo(new[] { firstQq, secondQq }));
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
