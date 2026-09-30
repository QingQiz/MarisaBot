using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiVersusCommandTest
{
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("\t　")]
    public void EmptyQueryRequestsRandomInsteadOfCatalogSelection(string input)
    {
        var db = Songs();
        Assert.That(db.SearchSong("".AsMemory()), Has.Count.EqualTo(4));
        var query = Resolve(db, input);
        Assert.Multiple(() =>
        {
            Assert.That(query.Random, Is.True);
            Assert.That(query.Songs, Is.Empty);
            Assert.That(query.Scope, Is.Null);
            Assert.That(query.LevelIndex, Is.EqualTo(3));
        });
    }

    [TestCase("彩")]
    [TestCase("真")]
    [TestCase("14+")]
    public void ValidScopeTakesPrecedenceOverFuzzyAliases(string input)
    {
        var db = Songs();
        Assert.That(db.SearchSong(input.AsMemory()), Is.Not.Empty);
        Assert.That(db.SearchSongExact(input.AsMemory()), Is.Empty);
        var query = Resolve(db, input);
        Assert.That(query.Scope, Is.Not.Null);
        Assert.That(query.Songs, Is.Empty);
        Assert.That(query.Random, Is.False);
    }

    [TestCase("22", 22, 3)]
    [TestCase("id22", 22, 3)]
    [TestCase("In Chaos", 22, 3)]
    [TestCase("in chaos", 22, 3)]
    [TestCase("CHAOS", 22, 3)]
    [TestCase("紫谱 chaos", 22, 3)]
    [TestCase("chaos EXPERT", 22, 2)]
    [TestCase("白金", 23, 3)]
    [TestCase("紫谱 白金", 23, 3)]
    public void ExactSongAliasesIdsAndDifficultyAffixesStaySingleSong(string text, long id, int level)
    {
        var query = Resolve(Songs(), text);
        Assert.That(query.Scope, Is.Null);
        Assert.That(query.Random, Is.False);
        Assert.That(query.LevelIndex, Is.EqualTo(level));
        Assert.That(query.Songs.Select(song => song.Id), Is.EqualTo(new[] { id }));
    }

    [Test]
    public void ExactAliasCanWinOverScopeWithoutStealingLongerSongNames()
    {
        var db = Songs(("彩", "In Chaos"));
        Assert.That(Resolve(db, "彩").Songs.Single().Id, Is.EqualTo(22));
        Assert.That(Resolve(db, "彩虹").Songs.Single().Id, Is.EqualTo(25));
    }

    [Test]
    public void NumericTitleAndAliasStaySongsWhenNoSuchIdExists()
    {
        var db = Songs(("14", "In Chaos"));
        Assert.That(Resolve(db, "14").Songs.Single().Id, Is.EqualTo(22));
        db.SongList.Add(Song(1001, "13"));
        Assert.That(Resolve(db, "13").Songs.Single().Id, Is.EqualTo(1001));
    }

    [TestCase("MASTER", 3)]
    [TestCase("EXPERT", 2)]
    [TestCase("白谱", 4)]
    public void DifficultyAloneRequestsRandomNotAllCharts(string text, int expected)
    {
        var query = Resolve(Songs(), text);
        Assert.That(query.Random, Is.True);
        Assert.That(query.Scope, Is.Null);
        Assert.That(query.LevelIndex, Is.EqualTo(expected));
    }

    [Test]
    public void InvalidScopeDoesNotBecomeRandomAndFuzzySongsStillWork()
    {
        Assert.That(Resolve(Songs(), "彩代14.8-14.0").Random, Is.False);
        Assert.That(Resolve(Songs(), "不存在的歌曲").Songs, Is.Empty);
        Assert.That(Resolve(Songs(), "Chao").Songs.Single().Id, Is.EqualTo(22));
        Assert.That(Resolve(Songs(), "彩代紫谱").Scope!.LevelIdxes, Is.EqualTo(new[] { 3 }));
    }

    [Test]
    public void RandomCandidatesRequireBothPlayersSameDifficultyAndAnExistingChart()
    {
        var songs = Songs().SongList;
        var left = new Dictionary<(long, int), SongScore>
        {
            [(22, 3)] = new(), [(23, 3)] = new(), [(24, 2)] = new(), [(25, 4)] = new()
        };
        var right = new Dictionary<(long, int), SongScore>
        {
            [(22, 3)] = new(), [(24, 3)] = new(), [(25, 4)] = new()
        };
        var method = typeof(MaiMaiDx.MaiMaiDx).GetMethod("SharedVersusSongs", BindingFlags.NonPublic | BindingFlags.Static)!;
        var candidates = (List<MaiMaiSong>)method.Invoke(null, [songs, 3, left, right])!;
        Assert.That(candidates.Select(song => song.Id), Is.EqualTo(new[] { 22L }));
        Assert.That((List<MaiMaiSong>)method.Invoke(null, [songs, 4, left, right])!, Is.Empty);
        Assert.That((List<MaiMaiSong>)method.Invoke(null, [songs, 3, left, new Dictionary<(long, int), SongScore>()])!, Is.Empty);
    }

    [TestCase("n", 'n', true)]
    [TestCase("n 20", 'n', true)]
    [TestCase("N20 彩代", 'n', true)]
    [TestCase("b 彩代14+", 'b', true)]
    [TestCase("b彩代14+", 'b', true)]
    [TestCase("Neverland", 'n', false)]
    [TestCase("Bad Apple", 'b', false)]
    [TestCase("B.B.K.K.B.K.K.", 'b', false)]
    [TestCase("", 'n', false)]
    public void SubcommandLetterNeedsABoundary(string command, char name, bool expected)
    {
        var method = typeof(MaiMaiDx.MaiMaiDx).GetMethod("IsVersusSubcommand", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.That((bool)method.Invoke(null, [command.AsMemory(), name])!, Is.EqualTo(expected));
    }

    private static (List<MaiMaiSong> Songs, int LevelIndex, bool Random, PlateData.Query? Scope)
        Resolve(SongDb<MaiMaiSong> db, string input) =>
        ((List<MaiMaiSong>, int, bool, PlateData.Query?))typeof(MaiMaiDx.MaiMaiDx)
            .GetMethod("ResolveVersusQuery", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [db, input])!;

    private static SongDb<MaiMaiSong> Songs(params (string Alias, string Title)[] extra)
    {
        var catalog = new List<MaiMaiSong>
        {
            Song(22, "In Chaos"), Song(23, "白金ディスコ"), Song(24, "MASTERPIECE"), Song(25, "彩虹物语")
        };
        var db = new SongDb<MaiMaiSong>("unused.tsv", "unused.tsv", () => catalog);
        var aliases = catalog.Select(song => (Alias: song.Title, song.Title))
            .Concat(new[] { ("chaos", "In Chaos"), ("白金", "白金ディスコ"), ("真名别名", "In Chaos"), ("14plus", "In Chaos") })
            .Concat(extra).ToDictionary(x => x.Item1.AsMemory(), x => new List<ReadOnlyMemory<char>> { x.Item2.AsMemory() });
        typeof(SongDb<MaiMaiSong>).GetField("_songAlias", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(db, aliases);
        return db;
    }

    internal static MaiMaiSong Song(long id, string title)
    {
        dynamic data = new ExpandoObject();
        data.id = id.ToString(); data.title = title; data.type = "DX";
        dynamic info = new ExpandoObject();
        info.artist = "artist"; info.genre = "舞萌"; info.bpm = 120;
        info.from = "maimai でらっくす PRiSM PLUS"; info.is_new = false;
        info.title = title; info.release_date = "2026-01-01";
        data.basic_info = info;
        data.ds = new[] { 3.0, 7.0, 12.0, 14.0 };
        data.level = new[] { "3", "7", "12", "14" };
        data.charts = Enumerable.Range(0, 4).Select(_ =>
        {
            dynamic chart = new ExpandoObject();
            chart.notes = new long[] { 100, 10, 10, 10, 10 }; chart.charter = "-";
            return chart;
        }).ToArray();
        return new MaiMaiSong(data);
    }
}
