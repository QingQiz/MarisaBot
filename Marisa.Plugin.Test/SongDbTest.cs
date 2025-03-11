using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Flurl.Http;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class SongDbTest
{
    private SongDb<MaiMaiSong> _songDb;

    [SetUp]
    public void SetUp()
    {
        var configPath = Path.Join(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(), "Marisa.StartUp", "config.yaml");

        ConfigurationManager.SetConfigFilePath(configPath);

        _songDb = new SongDb<MaiMaiSong>(
            ResourceManager.ResourcePath + "/aliases.tsv",
            ResourceManager.TempPath + "/MaiMaiSongAliasTemp.txt",
            () =>
            {
                var data = "https://www.diving-fish.com/api/maimaidxprober/music_data".GetJsonListAsync().Result;

                return data.Select(d => new MaiMaiSong(d)).ToList();
            },
            Dialog.TryAddHandler
        );
    }

    [Test]
    public void Song_Alias_Should_Be_Create()
    {
        var alias = (
            (Dictionary<ReadOnlyMemory<char>, List<ReadOnlyMemory<char>>>)
            _songDb.GetType()
                .GetProperty("SongAlias", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(_songDb, null)!
        ).MaxBy(x => x.Value.Count);

        Assert.Greater(alias.Value.Count, 1);
    }
}