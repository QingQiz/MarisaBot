using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiFilteredBestTest
{
    [Test]
    public void SplitsOldAndNewWhenScopeHasBoth()
    {
        var songs = Enumerable.Range(1, 60).Select(id => Song(id, isNew: id > 40, constant: 12 + id * 0.01)).ToList();
        var scores = songs.ToDictionary(song => (song.Id, 3), song => Score(song, 3, fc: "ap"));
        var notAp = Song(99, isNew: false, constant: 15);
        songs.Add(notAp);
        scores[(99, 3)] = Score(notAp, 3, fc: "fcp");

        var b50 = Build("ap50", songs, scores);

        Assert.Multiple(() =>
        {
            Assert.That(b50.Merged, Is.False);
            Assert.That(b50.OldScores.Select(s => s.Id), Is.EqualTo(Enumerable.Range(6, 35).Reverse().Select(id => (long)id)));
            Assert.That(b50.NewScores.Select(s => s.Id), Is.EqualTo(Enumerable.Range(46, 15).Reverse().Select(id => (long)id)));
            Assert.That(b50.Title, Is.EqualTo("ap50"));
        });
    }

    [Test]
    public void MergesWhenScopeHasOnlyOneSide()
    {
        var songs = Enumerable.Range(1, 60).Select(id => Song(id, isNew: false, constant: 12 + id * 0.01)).ToList();
        var scores = songs.ToDictionary(song => (song.Id, 3), song => Score(song, 3, fc: "ap"));

        var b50 = Build("ap50", songs, scores);

        Assert.Multiple(() =>
        {
            Assert.That(b50.Merged, Is.True);
            Assert.That(b50.OldScores.Select(s => s.Id), Is.EqualTo(Enumerable.Range(11, 50).Reverse().Select(id => (long)id)));
            Assert.That(b50.NewScores, Is.Empty);
        });
    }

    [Test]
    public void SplitKeepsEmptyNewSideInsteadOfFillingWithOld()
    {
        var songs = Enumerable.Range(1, 60).Select(id => Song(id, isNew: id > 40, constant: 12 + id * 0.01)).ToList();
        var scores = songs.Where(song => !song.Info.IsNew).ToDictionary(song => (song.Id, 3), song => Score(song, 3, fc: "ap"));

        var b50 = Build("ap50", songs, scores);

        Assert.Multiple(() =>
        {
            Assert.That(b50.Merged, Is.False);
            Assert.That(b50.OldScores, Has.Count.EqualTo(35));
            Assert.That(b50.NewScores, Is.Empty);
        });
    }

    [Test]
    public void ThresholdIsAtLeastAndRespectsScope()
    {
        var songs = Enumerable.Range(1, 4).Select(id => Song(id, isNew: false, constant: 13)).ToList();
        var scores = new Dictionary<(long, int), SongScore>
        {
            [(1, 3)] = Score(songs[0], 3, fc: "app"),
            [(2, 3)] = Score(songs[1], 3, fc: "ap"),
            [(3, 3)] = Score(songs[2], 3, fc: "fcp"),
            [(4, 4)] = Score(songs[3], 4, fc: "ap")
        };

        Assert.Multiple(() =>
        {
            Assert.That(Build("ap50", songs, scores).OldScores.Select(s => s.Id), Is.EquivalentTo(new long[] { 1, 2, 4 }));
            Assert.That(Build("紫谱ap50", songs, scores).OldScores.Select(s => s.Id), Is.EquivalentTo(new long[] { 1, 2 }));
            Assert.That(Build("ap+50", songs, scores).OldScores.Select(s => s.Id), Is.EqualTo(new long[] { 1 }));
            Assert.That(Build("紫谱50", songs, scores).OldScores.Select(s => s.Id), Is.EquivalentTo(new long[] { 1, 2, 3 }));
        });
    }

    [Test]
    public void SixStarsNeedNinetyNinePercentOfMaxDx()
    {
        var songs = new List<MaiMaiSong> { Song(1, isNew: false, constant: 13), Song(2, isNew: false, constant: 13) };
        var maxDx = songs[0].Charts[3].Notes.Sum() * 3;
        var scores = new Dictionary<(long, int), SongScore>
        {
            [(1, 3)] = Score(songs[0], 3, dxScore: (int)Math.Ceiling(maxDx * 0.99)),
            [(2, 3)] = Score(songs[1], 3, dxScore: (int)Math.Ceiling(maxDx * 0.99) - 1)
        };

        Assert.Multiple(() =>
        {
            Assert.That(Build("六星50", songs, scores).OldScores.Select(s => s.Id), Is.EqualTo(new long[] { 1 }));
            Assert.That(Build("五星50", songs, scores).OldScores.Select(s => s.Id), Is.EquivalentTo(new long[] { 1, 2 }));
        });
    }

    [TestCase("ap50", true)]
    [TestCase("14+ 六星b50", true)]
    [TestCase("b50", false)]
    [TestCase("B50", false)]
    [TestCase("b 50", false)]
    [TestCase("b50 someone", false)]
    [TestCase("info In Chaos 50", false)]
    [TestCase("ap 完成表", false)]
    public void TriggerLeavesPlainB50Alone(string command, bool expected)
    {
        var plugin = (MaiMaiDx.MaiMaiDx)RuntimeHelpers.GetUninitializedObject(typeof(MaiMaiDx.MaiMaiDx));
        var catalog = new List<MaiMaiSong> { Song(1, isNew: false, constant: 13) };
        typeof(MaiMaiDx.MaiMaiDx).GetField("<SongDb>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(plugin, new SongDb<MaiMaiSong>("unused.tsv", "unused.tsv", () => catalog));
        var parse = typeof(MaiMaiDx.MaiMaiDx).GetMethod("TryParseFilteredBest", BindingFlags.NonPublic | BindingFlags.Instance)!;

        Assert.That(parse.Invoke(plugin, [command.AsMemory(), null]), Is.EqualTo(expected));
    }

    private static DxRating Build(string command, IReadOnlyList<MaiMaiSong> songs, Dictionary<(long, int), SongScore> scores)
    {
        Assert.That(PlateData.TryParseBest(command, [], [], out var query), Is.True, command);
        var charts = PlateData.SelectCharts(query!, songs);
        return (DxRating)typeof(MaiMaiDx.MaiMaiDx)
            .GetMethod("BuildFilteredBest", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [query, charts, scores, "player", command])!;
    }

    private static SongScore Score(MaiMaiSong song, int levelIdx, string fc = "", int dxScore = 0) => new()
    {
        Id = song.Id, LevelIdx = levelIdx, Constant = song.Constants[levelIdx],
        Achievement = 100.5, Fc = fc, Fs = "", DxScore = dxScore, Title = song.Title, Type = song.Type
    };

    private static MaiMaiSong Song(long id, bool isNew, double constant)
    {
        dynamic data = new ExpandoObject();
        data.id = id.ToString(); data.title = $"song-{id}"; data.type = "DX";
        dynamic info = new ExpandoObject();
        info.artist = "artist"; info.genre = "舞萌"; info.bpm = 120;
        info.from = "maimai でらっくす PRiSM PLUS"; info.is_new = isNew;
        info.title = $"song-{id}"; info.release_date = "2026-01-01";
        data.basic_info = info;
        data.ds = new[] { 3.0, 7.0, 10.0, constant, constant };
        data.level = new[] { "3", "7", "10", "13", "13" };
        data.charts = Enumerable.Range(0, 5).Select(_ =>
        {
            dynamic chart = new ExpandoObject();
            chart.notes = new long[] { 100, 10, 10, 10, 10 }; chart.charter = "-";
            return chart;
        }).ToArray();
        return new MaiMaiSong(data);
    }
}
