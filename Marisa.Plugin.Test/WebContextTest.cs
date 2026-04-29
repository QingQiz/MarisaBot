using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Marisa.Configuration;
using NUnit.Framework;
using Marisa.Plugin.Shared.Util;

namespace Marisa.Plugin.Test;

public class WebContextTest
{
    private string _configPath = null!;
    private string _tempRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Join(Path.GetTempPath(), "Marisa.Plugin.Test", nameof(WebContextTest), Guid.NewGuid().ToString("N"));
        _configPath = CreateTestConfig(_tempRoot);
        ConfigurationManager.SetConfigFilePath(_configPath);
        WebContext.DumpOnPut = false;
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, true);
        }
    }

    [Test]
    [Repeat(20)]
    public void Test1()
    {
        var tasks = new Task[20];

        for (var i = 0; i < 20; i++)
        {
            tasks[i] = Task.Run(async () =>
            {
                var context = new WebContext();

                context.Put("key", "value");

                await W1(context.Id);
            });
        }

        Task.WaitAll(tasks);
    }

    [Test]
    public void Dump_Should_Write_History_To_Global_TempPath()
    {
        var context = new WebContext();

        WebContext.Dump(context.Id, "key", new { Value = "value" });

        var expected = Path.Join(ConfigurationManager.Configuration.TempPath, "WebContextHistory", $"key.{context.Id}");

        Assert.That(File.Exists(expected), Is.True);
        Assert.That(File.ReadAllText(expected), Does.Contain("value"));
    }

    private static async Task W1(Guid contextId)
    {
        _ = Task.Run(() => W2(contextId));
        await Task.Delay(100);
    }

    private static void W2(Guid contextId)
    {
        Assert.IsTrue(WebContext.Get(contextId, "key") is string);
    }

    private static string CreateTestConfig(string tempRoot)
    {
        var sourceConfigPath = Path.Join(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(), "Marisa.StartUp", "config.yaml");
        var escapedTempRoot = tempRoot.Replace("\\", "\\\\");
        var config = File.ReadAllText(sourceConfigPath);
        config = Regex.Replace(config, @"^tempPath:\s*.*$", $"tempPath:     {escapedTempRoot}", RegexOptions.Multiline);
        config = Regex.Replace(config, @"^databasePath:\s*.*$", "databasePath: bot.db", RegexOptions.Multiline);
        var configPath = Path.Join(tempRoot, "config.yaml");

        Directory.CreateDirectory(tempRoot);
        File.WriteAllText(configPath, config);

        return configPath;
    }
}
