using System;
using System.IO;
using Marisa.Plugin.Shared.Configuration;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class ArcaeaTest
{
    [SetUp]
    public void Setup()
    {
        var configPath = Path.Join(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(), "Marisa.StartUp", "config.yaml");
        ConfigurationManager.SetConfigFilePath(configPath);
    }

    [Test]
    public void SongList_Should_Be_Generated()
    {
        Assert.DoesNotThrow(() => Arcaea.SongListGen());
    }
}