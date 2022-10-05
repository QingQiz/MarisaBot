using System;
using System.IO;
using Marisa.Plugin.Shared.Configuration;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiDxTest
{
    private MaiMaiDx.MaiMaiDx? _maiMaiDx;

    [SetUp]
    public void Setup()
    {
        var configPath = Path.Join(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(), "Marisa.StartUp", "config.yaml");
        ConfigurationManager.SetConfigFilePath(configPath);

        _maiMaiDx = new MaiMaiDx.MaiMaiDx();
    }

    [Test]
    public void All_Song_Should_Have_Cover()
    {
        var x = _maiMaiDx!.SongsMissCover();
        
        Assert.IsEmpty(x);
    }
}