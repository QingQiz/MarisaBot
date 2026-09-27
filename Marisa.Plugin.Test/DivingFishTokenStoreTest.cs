using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Flurl.Http.Testing;
using Marisa.Configuration;
using Marisa.Database;
using Marisa.Database.Entity.Plugin.DivingFish;
using Marisa.Plugin.Shared.DivingFish;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

[TestFixture]
[NonParallelizable]
[Category("DivingFishOAuth")]
public class DivingFishTokenStoreTest
{
    private const string TokenUrl = "https://auth.diving-fish.com/oauth/token";
    private static long _identitySeed = 7_000_000_000;

    private HttpTest _http = null!;
    private string _testRoot = null!;
    private string _sourceConfig = null!;
    private long _user;

    [SetUp]
    public void SetUp()
    {
        _user = Interlocked.Increment(ref _identitySeed);
        _testRoot = Path.Combine(Path.GetTempPath(), nameof(DivingFishTokenStoreTest), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _sourceConfig = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../..", "Marisa.StartUp/config.yaml"));
        var configPath = Path.Combine(_testRoot, "config.yaml");
        File.WriteAllText(configPath, $$"""
            tempPath: '{{_testRoot}}'
            databasePath: token-store-test.db
            divingFish:
              clientId: test-client
              clientSecret: test-secret
            """);
        ConfigurationManager.SetConfigFilePath(configPath);

        _http = new HttpTest();
        _http.ForCallsTo("https://auth.diving-fish.com/.well-known/openid-configuration")
            .RespondWithJson(new
            {
                token_endpoint = TokenUrl,
                device_authorization_endpoint = "https://auth.diving-fish.com/oauth/device/code"
            });
    }

    [TearDown]
    public void TearDown()
    {
        _http.Dispose();
        ConfigurationManager.SetConfigFilePath(_sourceConfig);
        Directory.Delete(_testRoot, true);
    }

    [Test]
    public async Task NoAuthorization_ReturnsNull_WithoutNetwork()
    {
        Assert.That(await DivingFishTokenStore.GetValidToken(_user, "maimai"), Is.Null);

        _http.ShouldNotHaveMadeACall();
    }

    [Test]
    public async Task FreshStoredTicket_ReturnsWithoutNetwork()
    {
        Seed();

        var token = await DivingFishTokenStore.GetValidToken(_user, "maimai");

        Assert.That(token!.AccessToken, Is.EqualTo("stored-ticket"));
        Assert.That(token.Scope, Is.EqualTo(DivingFishOAuth.ScopeOf("maimai")));
        _http.ShouldNotHaveMadeACall();
    }

    [Test]
    public async Task ExpiredStoredTicket_MintsAndPersists()
    {
        Seed(expiresInSeconds: -60);
        MockMint("ticket-b");

        var token = await DivingFishTokenStore.GetValidToken(_user, "maimai");

        Assert.That(token!.AccessToken, Is.EqualTo("ticket-b"));
        Assert.That(ReadRow().AccessToken, Is.EqualTo("ticket-b"));

        // 新票已落库：再次取票不再拉票
        Assert.That((await DivingFishTokenStore.GetValidToken(_user, "maimai"))!.AccessToken, Is.EqualTo("ticket-b"));
        _http.ShouldHaveCalled(TokenUrl).WithRequestBody("*on-behalf-of*").Times(1);
    }

    [Test]
    public async Task ConsentRevoked_RemovesRowAndReturnsNull()
    {
        Seed(expiresInSeconds: -60);
        _http.ForCallsTo(TokenUrl).WithRequestBody("*on-behalf-of*")
            .RespondWithJson(new { error = "consent_required" }, 400);

        Assert.That(await DivingFishTokenStore.GetValidToken(_user, "maimai"), Is.Null);

        using var realm = BotDbContext.OpenRealm();
        Assert.That(realm.All<DivingFishAuthToken>().Count(x => x.Qq == _user), Is.Zero);
    }

    [Test]
    public async Task Authorization_IsIsolatedPerGame()
    {
        Seed(); // 只有 maimai 授权

        Assert.That(await DivingFishTokenStore.GetValidToken(_user, "chunithm"), Is.Null);
    }

    [Test]
    public async Task RemoveToken_NextCallMintsAgain()
    {
        Seed();
        DivingFishTokenStore.RemoveToken(_user, "maimai");
        MockMint("ticket-b");

        var token = await DivingFishTokenStore.GetValidToken(_user, "maimai");

        Assert.That(token!.AccessToken, Is.EqualTo("ticket-b"));
        Assert.That(ReadRow().Sub, Is.EqualTo("sub-a"), "丢票不应影响授权");
        _http.ShouldHaveCalled(TokenUrl).WithRequestBody("*on-behalf-of*").Times(1);
    }

    [Test]
    public async Task ConcurrentMint_OnlyFetchesOnce()
    {
        Seed(expiresInSeconds: -60);
        MockMint("ticket-b");

        var tokens = await Task.WhenAll(
            DivingFishTokenStore.GetValidToken(_user, "maimai"),
            DivingFishTokenStore.GetValidToken(_user, "maimai"));

        Assert.That(tokens.Select(x => x!.AccessToken), Is.All.EqualTo("ticket-b"));
        _http.ShouldHaveCalled(TokenUrl).WithRequestBody("*on-behalf-of*").Times(1);
    }

    [Test]
    public void SaveAuthorization_Rebind_OverwritesRow()
    {
        Seed(sub: "sub-a", accessToken: "ticket-a");
        Seed(sub: "sub-b", accessToken: "ticket-b");

        using var realm = BotDbContext.OpenRealm();
        var rows = realm.All<DivingFishAuthToken>().Where(x => x.Qq == _user).ToList();
        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Sub, Is.EqualTo("sub-b"));
        Assert.That(rows[0].AccessToken, Is.EqualTo("ticket-b"));
    }

    private void Seed(string sub = "sub-a", int expiresInSeconds = 3600, string accessToken = "stored-ticket")
    {
        DivingFishTokenStore.SaveAuthorization(_user, "maimai", sub, new DivingFishToken
        {
            AccessToken = accessToken,
            Scope = DivingFishOAuth.ScopeOf("maimai"),
            ExpiresAt = DateTime.UtcNow.AddSeconds(expiresInSeconds)
        });
    }

    private void MockMint(string accessToken, int expiresInSeconds = 3600)
    {
        _http.ForCallsTo(TokenUrl).WithRequestBody("*on-behalf-of*").RespondWithJson(new
        {
            access_token = accessToken,
            token_type = "Bearer",
            expires_in = expiresInSeconds,
            scope = DivingFishOAuth.ScopeOf("maimai")
        });
    }

    private (string Sub, string AccessToken) ReadRow()
    {
        using var realm = BotDbContext.OpenRealm();
        var row = realm.All<DivingFishAuthToken>().FirstOrDefault(x => x.Qq == _user);
        Assert.That(row, Is.Not.Null);
        return (row!.Sub, row.AccessToken);
    }
}
