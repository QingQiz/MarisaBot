using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Lxns;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

[TestFixture]
public class LxnsTokenStoreTest
{
    private static readonly string TempDir =
        Path.Combine(Path.GetTempPath(), "lxns-token-store-test");

    private static readonly FieldInfo CacheField = typeof(LxnsTokenStore)
        .GetField("_cache", BindingFlags.NonPublic | BindingFlags.Static)!;

    private static readonly FieldInfo StorePathField = typeof(LxnsTokenStore)
        .GetField("_storePath", BindingFlags.NonPublic | BindingFlags.Static)!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var configPath = Path.Join(
            Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(),
            "Marisa.StartUp", "config.yaml");
        ConfigurationManager.SetConfigFilePath(configPath);
    }

    [SetUp]
    public void SetUp()
    {
        if (Directory.Exists(TempDir)) Directory.Delete(TempDir, true);
        Directory.CreateDirectory(TempDir);
        StorePathField.SetValue(null, Path.Combine(TempDir, "lxns_oauth_tokens.json"));
        CacheField.SetValue(null, null);
    }
    [TearDown]
    public void TearDown()
    {
        CacheField.SetValue(null, null);
        StorePathField.SetValue(null, null);
        if (Directory.Exists(TempDir)) Directory.Delete(TempDir, true);
    }

    [Test]
    public async Task GetValidToken_WhenTokenNotExpired_ReturnsCachedWithoutRefresh()
    {
        // access token 1 小时后过期 → 应直接复用，不触发刷新
        LxnsTokenStore.SaveToken(1, "access-1", "refresh-1", 3600);

        var token = await LxnsTokenStore.GetValidToken(1);

        Assert.That(token, Is.Not.Null);
        Assert.That(token!.AccessToken, Is.EqualTo("access-1"));
        Assert.That(token.RefreshToken, Is.EqualTo("refresh-1"));
    }

    [Test]
    public async Task GetValidToken_WhenNoToken_ReturnsNull()
    {
        var token = await LxnsTokenStore.GetValidToken(999);

        Assert.That(token, Is.Null);
    }

    [Test]
    public async Task GetValidToken_WhenExpired_RefreshFailure_KeepsToken()
    {
        // 已过期的 token：会触发刷新。测试环境 clientId 未配置 → 刷新抛配置异常（非 token 失效）
        // 此时旧 token 应被保留，而不是删除
        LxnsTokenStore.SaveToken(1, "access-old", "refresh-1", -3600);

        try
        {
            await LxnsTokenStore.GetValidToken(1);
        }
        catch (Exception)
        {
            // 预期：刷新失败抛异常
        }

        var after = LxnsTokenStore.GetToken(1);
        Assert.That(after, Is.Not.Null, "刷新失败（非 token 失效）不应删除 token");
        Assert.That(after!.RefreshToken, Is.EqualTo("refresh-1"));
    }

    [Test]
    public async Task GetValidToken_ConcurrentRefresh_DoesNotRemoveToken()
    {
        // 模拟并发查询：多个任务同时请求同一 qq 的 token
        LxnsTokenStore.SaveToken(1, "access-old", "refresh-1", -3600);

        var tasks = new Task[5];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                try
                {
                    await LxnsTokenStore.GetValidToken(1);
                }
                catch (Exception)
                {
                    // 刷新失败是预期的（无配置）；关键是 token 不应被删除
                }
            });
        }

        await Task.WhenAll(tasks);

        var after = LxnsTokenStore.GetToken(1);
        Assert.That(after, Is.Not.Null, "并发刷新后 token 不应被删除");
    }
}
