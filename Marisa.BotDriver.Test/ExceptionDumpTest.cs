using System.Text.RegularExpressions;
using Marisa.Configuration;
using NUnit.Framework;

namespace Marisa.BotDriver.Test;

public class ExceptionDumpTest
{
    private string _configPath = null!;
    private string _tempRoot = null!;

    [SetUp]
    public void SetUp()
    {
        _tempRoot = Path.Join(Path.GetTempPath(), "Marisa.BotDriver.Test", Guid.NewGuid().ToString("N"));
        _configPath = CreateTestConfig(_tempRoot);
        ConfigurationManager.SetConfigFilePath(_configPath);
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
    public void Save_Should_Write_Exception_And_Related_Message_To_Temp_Root()
    {
        var path = ExceptionDump.Save(new InvalidOperationException("boom"), "test message", "unit-test");

        Assert.That(path, Is.Not.Null);
        Assert.That(path, Does.StartWith(Path.Join(_tempRoot, "exceptions")));
        Assert.That(File.Exists(path!), Is.True);

        var content = File.ReadAllText(path!);
        Assert.Multiple(() =>
        {
            Assert.That(content, Does.Contain("InvalidOperationException"));
            Assert.That(content, Does.Contain("boom"));
            Assert.That(content, Does.Contain("test message"));
            Assert.That(content, Does.Contain("unit-test"));
        });
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
