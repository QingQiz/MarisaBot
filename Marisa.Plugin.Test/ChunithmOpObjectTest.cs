using System.Collections.Generic;
using System.Linq;
using Marisa.Plugin.Chunithm;
using Marisa.Plugin.Shared.Chunithm;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

[TestFixture]
public class ChunithmOpObjectTest
{
    [Test]
    public void OpObjectIsTheChartWithHighestConstant()
    {
        var song = SongOf(("BASIC", 3.0), ("ADVANCED", 6.0), ("EXPERT", 9.0), ("MASTER", 14.5), ("ULTIMA", 14.2));

        Assert.Multiple(() =>
        {
            Assert.That(song.OpObjectIndex(), Is.EqualTo(3));
            Assert.That(song.DiffNames[song.OpObjectIndex()], Is.EqualTo("MASTER"));
        });
    }

    [Test]
    public void OpObjectPrefersUltimaWhenItHasTheHigherConstant()
    {
        var song = SongOf(("EXPERT", 9.0), ("MASTER", 14.5), ("ULTIMA", 15.0));

        Assert.That(song.DiffNames[song.OpObjectIndex()], Is.EqualTo("ULTIMA"));
    }

    [Test]
    public void OpObjectIgnoresWorldsEnd()
    {
        var song = SongOf(("MASTER", 14.5), ("WORLD'S END", 15.5));

        Assert.That(song.DiffNames[song.OpObjectIndex()], Is.EqualTo("MASTER"));
    }

    [Test]
    public void OpObjectIgnoresUnknownChartNames()
    {
        var song = SongOf(("MASTER", 14.0), ("UNKNOWN", 15.0));

        Assert.That(song.DiffNames[song.OpObjectIndex()], Is.EqualTo("MASTER"));
    }

    [Test]
    public void OpObjectBreaksEqualConstantByHigherDifficulty()
    {
        var song = SongOf(("MASTER", 15.0), ("ULTIMA", 15.0));

        Assert.Multiple(() =>
        {
            Assert.That(song.OpObjectIndex(), Is.EqualTo(1));
            Assert.That(song.DiffNames[song.OpObjectIndex()], Is.EqualTo("ULTIMA"));
        });
    }

    [Test]
    public void OpObjectIsUnknownForWorldsEndOnlySong()
    {
        Assert.That(SongOf(("WORLD'S END", 15.5)).OpObjectIndex(), Is.EqualTo(-1));
    }

    [Test]
    public void AchievementFlagsFollowScoreAndFcField()
    {
        Assert.Multiple(() =>
        {
            // 1,010,000 即使查分器没给 fc 也按 AJC 算
            var byScore = Score(1_010_000, null);
            Assert.That(byScore.IsAllJusticeCritical, Is.True);
            Assert.That(byScore.IsAllJustice, Is.True, "AJC 属于 AJ");
            Assert.That(byScore.IsFullCombo, Is.False, "没有 fc 字段就不算 FC");

            var aj = Score(1_005_000, "alljustice");
            Assert.That(aj.IsAllJustice, Is.True);
            Assert.That(aj.IsAllJusticeCritical, Is.False);
            Assert.That(aj.IsFullCombo, Is.True, "AJ 属于 FC");

            var fc = Score(990_000, "fullcombo");
            Assert.That(fc.IsFullCombo, Is.True);
            Assert.That(fc.IsAllJustice, Is.False);
            Assert.That(fc.IsAllJusticeCritical, Is.False);

            var clear = Score(950_000, "");
            Assert.That(clear.IsFullCombo, Is.False);
            Assert.That(clear.IsAllJustice, Is.False);
            Assert.That(clear.IsAllJusticeCritical, Is.False);
        });
    }

    [Test]
    public void FilterChartsKeepsOnlyOpObjectChart()
    {
        var song = SongOf(("EXPERT", 9.0), ("MASTER", 14.5), ("ULTIMA", 14.2));
        var rows = ChartsOf(song);

        var filtered = rows.FilterCharts(ChunithmChartFilter.OverPower, Scores(song)).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(filtered, Has.Count.EqualTo(1));
            Assert.That(filtered[0].i, Is.EqualTo(1), "op 对象谱面是 MASTER");
        });
    }

    [Test]
    public void FilterChartsAppliesEveryAchievementFlag()
    {
        var song   = SongOf(("BASIC", 3.0), ("ADVANCED", 6.0), ("EXPERT", 9.0), ("MASTER", 14.5), ("ULTIMA", 15.0));
        var rows   = ChartsOf(song);
        var scores = new Dictionary<(long, int), ChunithmScore>
        {
            [(song.Id, 0)] = new() { Id = song.Id, Achievement = 1_000_000, Fc = "alljustice" },         // AJ
            [(song.Id, 1)] = new() { Id = song.Id, Achievement = 1_010_000, Fc = "alljusticecritical" }, // AJC
            [(song.Id, 2)] = new() { Id = song.Id, Achievement = 990_000, Fc = "fullcombo" },            // 仅 FC
            [(song.Id, 3)] = new() { Id = song.Id, Achievement = 950_000, Fc = "" }                      // 通关未 FC
            // 4 未游玩
        };

        int[] RowIds(ChunithmChartFilter filter) => rows.FilterCharts(filter, scores).Select(x => x.i).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(RowIds(ChunithmChartFilter.FullCombo), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(RowIds(ChunithmChartFilter.AllJustice), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(RowIds(ChunithmChartFilter.AllJusticeCritical), Is.EqualTo(new[] { 1 }));

            // 取反时没游玩的谱面也算「不是 FC/AJ/AJC」
            Assert.That(RowIds(ChunithmChartFilter.NotFullCombo), Is.EqualTo(new[] { 3, 4 }));
            Assert.That(RowIds(ChunithmChartFilter.NotAllJustice), Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(RowIds(ChunithmChartFilter.NotAllJusticeCritical), Is.EqualTo(new[] { 0, 2, 3, 4 }));

            // 正向和取反可以叠加，逐项合取
            Assert.That(RowIds(ChunithmChartFilter.FullCombo | ChunithmChartFilter.NotAllJustice), Is.EqualTo(new[] { 2 }));
            Assert.That(RowIds(ChunithmChartFilter.AllJustice | ChunithmChartFilter.NotAllJusticeCritical), Is.EqualTo(new[] { 0 }));
            Assert.That(RowIds(ChunithmChartFilter.AllJusticeCritical | ChunithmChartFilter.NotAllJustice), Is.Empty);
        });
    }

    [Test]
    public void FilterChartsAppliesEveryRankFlag()
    {
        var song = SongOf(("BASIC", 3.0), ("ADVANCED", 6.0), ("EXPERT", 9.0), ("MASTER", 14.5), ("ULTIMA", 15.0));
        var rows = ChartsOf(song);
        var scores = new Dictionary<(long, int), ChunithmScore>
        {
            [(song.Id, 0)] = new() { Id = song.Id, Achievement = 1_010_000, Fc = "alljusticecritical" }, // SSS+
            [(song.Id, 1)] = new() { Id = song.Id, Achievement = 1_008_000, Fc = "alljustice" },         // SSS
            [(song.Id, 2)] = new() { Id = song.Id, Achievement = 1_006_000, Fc = "fullcombo" },          // SS+
            [(song.Id, 3)] = new() { Id = song.Id, Achievement = 1_001_000, Fc = "alljustice" },         // SS
            [(song.Id, 4)] = new() { Id = song.Id, Achievement = 980_000, Fc = "" }                       // S
        };

        int[] RowIds(ChunithmChartFilter filter, Dictionary<(long, int), ChunithmScore>? scs = null) =>
            rows.FilterCharts(filter, scs ?? scores).Select(x => x.i).ToArray();

        // 最后一首没游玩
        var unplayed = new Dictionary<(long, int), ChunithmScore>(scores);
        unplayed.Remove((song.Id, 4));

        Assert.Multiple(() =>
        {
            Assert.That(RowIds(ChunithmChartFilter.Ss), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(RowIds(ChunithmChartFilter.SsPlus), Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(RowIds(ChunithmChartFilter.Sss), Is.EqualTo(new[] { 0, 1 }));
            Assert.That(RowIds(ChunithmChartFilter.SssPlus), Is.EqualTo(new[] { 0 }));

            // 正向 + 取反 = 仅该评级
            Assert.That(RowIds(ChunithmChartFilter.Ss | ChunithmChartFilter.NotSsPlus), Is.EqualTo(new[] { 3 }));
            Assert.That(RowIds(ChunithmChartFilter.Sss | ChunithmChartFilter.NotSssPlus), Is.EqualTo(new[] { 1 }));

            // 评级和 AJ 系列可以混用：SS+ 以上但不是 AJ，就是 SS+
            Assert.That(RowIds(ChunithmChartFilter.SsPlus | ChunithmChartFilter.NotAllJustice), Is.EqualTo(new[] { 2 }));

            // 取反把没游玩的谱面也算进来
            Assert.That(RowIds(ChunithmChartFilter.NotSs, unplayed), Is.EqualTo(new[] { 4 }));
            Assert.That(RowIds(ChunithmChartFilter.NotSssPlus, unplayed), Is.EqualTo(new[] { 1, 2, 3, 4 }));
        });
    }

    private static ChunithmSong SongOf(params (string Name, double Constant)[] charts)
    {
        var song = ChunithmVersusModelTest.Song(1, names: []);

        foreach (var (name, constant) in charts)
        {
            song.AddDifficulty(((int)constant).ToString(), constant, "-", name, 1000, "", "200");
        }

        return song;
    }

    private static List<(double constant, int i, ChunithmSong song)> ChartsOf(ChunithmSong song) =>
        song.Constants.Select((constant, i) => (constant, i, song)).ToList();

    /// <summary>0/1/2 分别是 EXPERT、MASTER、ULTIMA 的成绩：AJ / AJC / FC，其余谱面未游玩。</summary>
    private static Dictionary<(long, int), ChunithmScore> Scores(ChunithmSong song) => new()
    {
        [(song.Id, 0)] = new() { Id = song.Id, Achievement = 1_000_000, Fc = "alljustice" },
        [(song.Id, 1)] = new() { Id = song.Id, Achievement = 1_010_000, Fc = "alljusticecritical" },
        [(song.Id, 2)] = new() { Id = song.Id, Achievement = 990_000, Fc = "fullcombo" }
    };

    private static ChunithmScore Score(int achievement, string? fc) =>
        new() { Id = 1, Achievement = achievement, Fc = fc };
}
