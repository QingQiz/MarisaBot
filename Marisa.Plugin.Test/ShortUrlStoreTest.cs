using System;
using System.IO;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Lxns;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class ShortUrlStoreTest
{
    [SetUp]
    public void SetUp()
    {
        var configPath = Path.Join(
            Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(),
            "Marisa.StartUp",
            "config.yaml");
        ConfigurationManager.SetConfigFilePath(configPath);
    }

    [Test]
    public void CreateShortUrl_Should_RoundTrip_And_Build_Go_Url()
    {
        const string target = "https://auth.diving-fish.com/oauth/authorize?state=test";

        var code = ShortUrlStore.CreateShortUrl(target, TimeSpan.FromMinutes(1));

        Assert.Multiple(() =>
        {
            Assert.That(code, Has.Length.EqualTo(6));
            Assert.That(ShortUrlStore.GetUrl(code), Is.EqualTo(target));
            Assert.That(ShortUrlStore.GetShortUrl(code), Does.EndWith($"/go/{code}"));
        });
    }

    [Test]
    public void CreateShortUrl_Should_Expire_When_Ttl_Is_Elapsed()
    {
        var code = ShortUrlStore.CreateShortUrl("https://example.com", TimeSpan.Zero);

        Assert.That(ShortUrlStore.GetUrl(code), Is.Null);
    }
}
