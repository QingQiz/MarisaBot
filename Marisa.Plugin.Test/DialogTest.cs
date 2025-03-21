using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Marisa.BotDriver.DI.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.Util;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class DialogTest
{
    private TestBackend _driver;

    [SetUp]
    public void SetUp()
    {
        WebApi.DisableWebApi = true;
        var configPath = Path.Join(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(), "Marisa.StartUp", "config.yaml");
        ConfigurationManager.SetConfigFilePath(configPath);

        _driver = TestBackend.Create(typeof(Chunithm.Chunithm), typeof(Dialog));

        _ = _driver.Invoke();
    }

    [Test]
    public async Task Chunithm_Preview_Should_Be_Triggered()
    {
        await _driver.SetMessage(0, 0, "chu preview e");
        await _driver.SetMessage(0, 0, "p2");
        await _driver.SetMessage(0, 0, "5");
        await _driver.SetMessage(0, 0, "3");
        _driver.Finish();
        await _driver.ProcAll();

        var m = await _driver.GetAllSend();

        Assert.AreEqual(m.Count, 4);
        AssertMessageContainsType(m[0], MessageDataType.Text);
        AssertMessageContainsType(m[1], MessageDataType.Text);
        AssertMessageContainsType(m[2], MessageDataType.Text);
        AssertMessageContainsType(m[3], MessageDataType.Image);
    }

    [Test]
    public async Task Chunithm_Tol_Should_Be_Triggered()
    {
        await _driver.SetMessage(0, 0, "chu tol e");
        await _driver.SetMessage(0, 0, "p2");
        await _driver.SetMessage(0, 0, "5");
        await _driver.SetMessage(0, 0, "3 1010000");

        _driver.Finish();
        await _driver.ProcAll();

        var m = await _driver.GetAllSend();

        Assert.AreEqual(m.Count, 4);
        AssertMessageContainsType(m[0], MessageDataType.Text);
        AssertMessageContainsType(m[1], MessageDataType.Text);
        AssertMessageContainsType(m[2], MessageDataType.Text);
        AssertMessageContainsType(m[3], MessageDataType.Text);
    }

    [Test]
    public async Task Chunithm_Guess_Should_not_Be_Repeated()
    {
        await _driver.SetMessage(0, 0, "chu guess");
        await _driver.SetMessage(0, 0, "chu guess");
        _driver.Finish();
        await _driver.ProcAll();

        var m = await _driver.GetAllSend();

        Assert.AreEqual(m.Count, 2);
        AssertMessageContainsType(m[0], MessageDataType.Text, MessageDataType.Image);
        AssertMessageContainsText(m[1], "？");
    }

    [Test]
    public async Task Chu_Bind_Should_not_Be_Repeated()
    {
        await _driver.SetMessage(0, 0, "chu bind");
        await _driver.SetMessage(0, 0, "chu bind");
        _driver.Finish();
        await _driver.ProcAll();

        var m = await _driver.GetAllSend();

        Assert.AreEqual(m.Count, 2);
        AssertMessageContainsText(m[0], "diving");
        AssertMessageContainsText(m[1], "错误");
    }


    private static void AssertMessageContainsType(MessageToSend m, params MessageDataType[] types)
    {
        foreach (var t in types)
        {
            Assert.That(m.MessageChain.Messages.Any(x => x.Type == t), $"message `{m.MessageChain}` not contains type {t}");
        }
    }

    private static void AssertMessageContainsText(MessageToSend m, string text)
    {
        Assert.That(m.MessageChain.ToString().Contains(text, StringComparison.OrdinalIgnoreCase));
    }
}