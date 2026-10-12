using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Reflection;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

[TestFixture]
public class ChunithmVersusCommandTest
{
    [TestCase("", 3)]
    [TestCase("BASIC", 0)]
    [TestCase("黄谱", 1)]
    [TestCase("EXPERT", 2)]
    [TestCase("紫谱", 3)]
    [TestCase("ULTIMA", 4)]
    [TestCase("黑谱", 4)]
    public void EmptyOrDifficultyOnlySelectsRandomStandardCharts(string input, int level)
    {
        var selection = Resolve(Songs(), input);
        Assert.Multiple(() =>
        {
            Assert.That(selection.Random, Is.True);
            Assert.That(selection.LevelIndex, Is.EqualTo(level));
        });
    }

    [TestCase("21", 3)]
    [TestCase("Night", 3)]
    [TestCase("night", 3)]
    [TestCase("ULTIMA Night", 4)]
    [TestCase("Night 黑谱", 4)]
    public void SongQueriesKeepIdentityAndExplicitDifficulty(string input, int level)
    {
        var selection = Resolve(Songs(), input);
        Assert.Multiple(() =>
        {
            Assert.That(selection.Random, Is.False);
            Assert.That(selection.LevelIndex, Is.EqualTo(level));
            Assert.That(selection.Songs.Select(x => x.Id), Is.EqualTo(new[] { 21L }));
        });
    }

    [Test]
    public void ExactSongTitleWinsOverDifficultyKeyword()
    {
        var db = Songs();
        db.SongList.Add(Song(22, "BASIC"));
        var selection = Resolve(db, "BASIC");
        Assert.That(selection.Random, Is.False);
        Assert.That(selection.Songs.Select(x => x.Id), Is.EqualTo(new[] { 22L }));
    }

    private static (List<ChunithmSong> Songs, int LevelIndex, bool Random) Resolve(SongDb<ChunithmSong> db, string input) =>
        ((List<ChunithmSong>, int, bool))typeof(Chunithm.Chunithm)
            .GetMethod("ResolveVersusQuery", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [db, input])!;

    private static SongDb<ChunithmSong> Songs()
    {
        var db = new SongDb<ChunithmSong>("unused.tsv", "unused.tsv", () => [Song(21, "Night")]);
        typeof(SongDb<ChunithmSong>).GetField("_songAlias", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(db, new Dictionary<ReadOnlyMemory<char>, List<ReadOnlyMemory<char>>>());
        return db;
    }

    internal static ChunithmSong Song(long id, string title)
    {
        var json = JsonConvert.SerializeObject(new
        {
            Id = id, Title = title, Artist = "artist", Genre = "VARIETY", Version = "CHUNITHM VERSE",
            Beatmaps = new[]
            {
                new { LevelName = "BASIC", LevelStr = "3", Constant = 3.0, Charter = "charter", MaxCombo = 100, Bpm = "120", ChartName = "" },
                new { LevelName = "MASTER", LevelStr = "14+", Constant = 14.8, Charter = "charter", MaxCombo = 1000, Bpm = "120", ChartName = "" },
                new { LevelName = "ULTIMA", LevelStr = "15", Constant = 15.0, Charter = "charter", MaxCombo = 1200, Bpm = "120", ChartName = "" }
            }
        });
        dynamic value = JsonConvert.DeserializeObject<ExpandoObject>(json)!;
        return new ChunithmSong(value);
    }
}
