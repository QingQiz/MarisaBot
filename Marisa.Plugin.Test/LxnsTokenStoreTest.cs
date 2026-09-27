using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Flurl.Http;
using Flurl.Http.Testing;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Lxns;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

[TestFixture]
[NonParallelizable]
public class LxnsTokenStoreTest
{
    private const string RefreshUrl = "https://maimai.lxns.net/api/v0/oauth/token";
    private const long Qq = 1;

    private HttpTest _http = null!;
    private string _testRoot = null!;
    private string _sourceConfig = null!;

    [SetUp]
    public void SetUp()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), nameof(LxnsTokenStoreTest), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _sourceConfig = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../..", "Marisa.StartUp/config.yaml"));
        var configPath = Path.Combine(_testRoot, "config.yaml");
        File.WriteAllText(configPath, $$"""
            tempPath: '{{_testRoot}}'
            databasePath: lxns-token-store-test.db
            lxns:
              oauth:
                clientId: test-client
            """);
        ConfigurationManager.SetConfigFilePath(configPath);

        _http = new HttpTest();
    }

    [TearDown]
    public void TearDown()
    {
        _http.Dispose();
        ConfigurationManager.SetConfigFilePath(_sourceConfig);
        Directory.Delete(_testRoot, true);
    }

    [Test]
    public async Task GetValidToken_WhenTokenNotExpired_ReturnsWithoutRefresh()
    {
        // access token 1 小时后过期 → 应直接复用，不触发刷新
        LxnsTokenStore.SaveToken(Qq, "access-1", "refresh-1", 3600);

        var token = await LxnsTokenStore.GetValidToken(Qq);

        Assert.That(token, Is.Not.Null);
        Assert.That(token!.AccessToken, Is.EqualTo("access-1"));
        Assert.That(token.RefreshToken, Is.EqualTo("refresh-1"));
        _http.ShouldNotHaveMadeACall();
    }

    [Test]
    public async Task GetValidToken_WhenNoToken_ReturnsNull()
    {
        Assert.That(await LxnsTokenStore.GetValidToken(999), Is.Null);
    }

    [Test]
    public async Task GetValidToken_WhenExpired_RefreshFailure_KeepsToken()
    {
        // 刷新遇到服务端错误（非 token 失效）时，旧 token 应被保留，而不是删除
        LxnsTokenStore.SaveToken(Qq, "access-old", "refresh-1", -3600);
        _http.ForCallsTo(RefreshUrl).RespondWithJson(new { message = "server error" }, 500);

        Assert.ThrowsAsync<FlurlHttpException>(() => LxnsTokenStore.GetValidToken(Qq));

        var after = LxnsTokenStore.GetToken(Qq);
        Assert.That(after, Is.Not.Null, "刷新失败（非 token 失效）不应删除 token");
        Assert.That(after!.RefreshToken, Is.EqualTo("refresh-1"));
    }

    [Test]
    public async Task GetValidToken_ConcurrentRefresh_DoesNotRemoveToken()
    {
        // 模拟并发查询：多个任务同时请求同一 qq 的 token
        LxnsTokenStore.SaveToken(Qq, "access-old", "refresh-1", -3600);
        _http.ForCallsTo(RefreshUrl).RespondWithJson(new { message = "server error" }, 500);

        var tasks = new Task[5];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    await LxnsTokenStore.GetValidToken(Qq);
                }
                catch (Exception)
                {
                    // 刷新失败是预期的；关键是 token 不应被删除
                }
            });
        }

        await Task.WhenAll(tasks);

        var after = LxnsTokenStore.GetToken(Qq);
        Assert.That(after, Is.Not.Null, "并发刷新后 token 不应被删除");
    }

    [Test]
    public async Task GetValidToken_WhenExpired_RefreshesAndPersists()
    {
        LxnsTokenStore.SaveToken(Qq, "access-old", "refresh-old", -3600);
        MockRefresh("access-new", "refresh-new");

        var token = await LxnsTokenStore.GetValidToken(Qq);

        Assert.That(token!.AccessToken, Is.EqualTo("access-new"));
        Assert.That(token.RefreshToken, Is.EqualTo("refresh-new"));

        var stored = LxnsTokenStore.GetToken(Qq);
        Assert.That(stored!.AccessToken, Is.EqualTo("access-new"));

        // 新票已落库：再次取票不再刷新
        Assert.That((await LxnsTokenStore.GetValidToken(Qq))!.AccessToken, Is.EqualTo("access-new"));
        _http.ShouldHaveCalled(RefreshUrl).Times(1);
    }

    [Test]
    public async Task RemoveToken_KeepsRefreshToken_AndNextCallRefreshes()
    {
        LxnsTokenStore.SaveToken(Qq, "access-old", "refresh-1", 3600);

        LxnsTokenStore.RemoveToken(Qq);

        var stored = LxnsTokenStore.GetToken(Qq);
        Assert.That(stored, Is.Not.Null, "丢票不应删除授权");
        Assert.That(stored!.RefreshToken, Is.EqualTo("refresh-1"));

        MockRefresh("access-new", "refresh-new");
        var token = await LxnsTokenStore.GetValidToken(Qq);
        Assert.That(token!.AccessToken, Is.EqualTo("access-new"));
    }

    [Test]
    public async Task GetValidToken_WhenRefreshRejected_RemovesAuthorization()
    {
        LxnsTokenStore.SaveToken(Qq, "access-old", "refresh-revoked", -3600);
        _http.ForCallsTo(RefreshUrl)
            .RespondWithJson(new { error = "invalid_grant", error_description = "refresh token revoked" }, 401);

        Assert.ThrowsAsync<HttpRequestException>(() => LxnsTokenStore.GetValidToken(Qq));

        Assert.That(LxnsTokenStore.GetToken(Qq), Is.Null, "刷新令牌失效应删除授权，要求重新授权");
    }

    private void MockRefresh(string accessToken, string refreshToken, int expiresIn = 3600)
    {
        _http.ForCallsTo(RefreshUrl).RespondWithJson(new
        {
            access_token = accessToken,
            refresh_token = refreshToken,
            expires_in = expiresIn
        });
    }
}
