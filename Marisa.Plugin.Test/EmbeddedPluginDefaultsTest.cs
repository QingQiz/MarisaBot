using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Marisa.BotDriver;
using Marisa.Configuration;
using TodayFortunePlugin = Marisa.Plugin.TodayFortune.TodayFortune;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class EmbeddedPluginDefaultsTest
{
    [SetUp]
    public void SetUp()
    {
        var configPath = Path.Join(
            Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(),
            "Marisa.StartUp",
            "config.yaml");

        ConfigurationManager.SetConfigFilePath(configPath);
        ResetMessageDispatcherCache();
    }

    [Test]
    public async Task Chi_Should_Use_Embedded_Default_Places()
    {
        var driver = TestBackend.Create(typeof(Chi));

        _ = driver.Invoke();

        await driver.SetMessage(114514, 1919810, "listplace");
        driver.Finish();
        await driver.ProcAll();

        var messages = await driver.GetAllSend();

        Assert.That(messages, Has.Count.EqualTo(1));
        Assert.That(messages[0].MessageChain.ToString(), Does.Contain("西工大"));
    }

    [Test]
    public async Task TodayFortune_Should_Have_Embedded_Default_Data()
    {
        var driver = TestBackend.Create(typeof(TodayFortunePlugin));

        _ = driver.Invoke();

        await driver.SetMessage(114514, 1919810, "今日运势");
        driver.Finish();
        await driver.ProcAll();

        var messages = await driver.GetAllSend();

        Assert.That(messages, Has.Count.EqualTo(1));
        Assert.That(messages[0].MessageChain.ToString(), Does.Contain("今日音游"));
        Assert.That(messages[0].MessageChain.ToString(), Does.Contain("街机音游黄金位"));
    }

    private static void ResetMessageDispatcherCache()
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;

        typeof(MessageDispatcher).GetField("_plugins", flags)?.SetValue(null, null);
        typeof(MessageDispatcher).GetField("_commands", flags)?.SetValue(null, null);
        typeof(MessageDispatcher).GetField("_subCommands", flags)?.SetValue(null, null);
    }
}