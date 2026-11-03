using System;
using System.Collections.Generic;
using System.Linq;
using Marisa.Plugin.Shared.MaiMaiDx;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiKadaiTest
{
    [Test]
    public void PicksOneChartPerBandInIncreasingDifficulty()
    {
        // 已达成难度 1..10：热身 = [9, 9.4)，进阶 = [9.4, 9.8)，挑战 = [9.8, 10.3]
        var reached = Enumerable.Range(1, 10).Select(x => (double)x).ToList();
        var candidates = new[] { 5.0, 9.1, 9.3, 9.5, 9.7, 10.2, 12.0 }.Select((d, i) => Chart(i, d)).ToList();

        var result = KadaiPicker.Pick(reached, candidates, new Random(1))!;

        Assert.Multiple(() =>
        {
            Assert.That(result.ColdStart, Is.False);
            Assert.That(result.Charts[0].Difficulty, Is.InRange(9, 9.39));
            Assert.That(result.Charts[1].Difficulty, Is.InRange(9.4, 9.79));
            Assert.That(result.Charts[2].Difficulty, Is.EqualTo(10.2));
        });
    }

    [Test]
    public void PrefersUnplayedThenLowScoresWithinBand()
    {
        var reached = Enumerable.Range(1, 10).Select(x => (double)x).ToList();
        var candidates = new List<KadaiPicker.Chart>
        {
            Chart(1, 9.1, achievement: 99.0),
            Chart(2, 9.2),
            Chart(3, 9.3, achievement: 100.2),
            Chart(4, 9.6), Chart(5, 10.2),
        };

        for (var seed = 0; seed < 20; seed++)
        {
            Assert.That(KadaiPicker.Pick(reached, candidates, new Random(seed))!.Charts[0].SongId, Is.EqualTo(2));
        }

        candidates.RemoveAt(1);
        for (var seed = 0; seed < 20; seed++)
        {
            Assert.That(KadaiPicker.Pick(reached, candidates, new Random(seed))!.Charts[0].SongId, Is.EqualTo(1));
        }
    }

    [Test]
    public void AnchorSlidesDownWhenTopIsAlreadyCleared()
    {
        // 已达成到 10，但未完成的谱面都在 0.1..2.0：锚应落在第 10 难的未完成谱面 (1.1) 上
        var reached = Enumerable.Range(1, 10).Select(x => (double)x).ToList();
        var candidates = Enumerable.Range(1, 20).Select(i => Chart(i, i / 10.0)).ToList();

        var result = KadaiPicker.Pick(reached, candidates, new Random(1))!;

        Assert.Multiple(() =>
        {
            Assert.That(result.Charts[0].Difficulty, Is.InRange(0.1, 0.49));
            Assert.That(result.Charts[1].Difficulty, Is.InRange(0.5, 0.89));
            Assert.That(result.Charts[2].Difficulty, Is.InRange(0.9, 1.4));
        });
    }

    [Test]
    public void FallsBackToNearestChartWhenBandIsEmpty()
    {
        var reached = Enumerable.Range(1, 10).Select(x => (double)x).ToList();
        var candidates = new[] { 1.0, 2.0, 15.0 }.Select((d, i) => Chart(i, d)).ToList();

        var result = KadaiPicker.Pick(reached, candidates, new Random(1))!;

        Assert.That(result.Charts.Select(c => c.Difficulty), Is.EqualTo(new[] { 1.0, 2.0, 15.0 }));
    }

    [Test]
    public void ColdStartPicksEasiestByRateForFewAchievements()
    {
        var candidates = Enumerable.Range(0, 30).Select(i => Chart(i, i, rate: 1 - i / 30.0)).ToList();

        var result = KadaiPicker.Pick([3.0, 4.0], candidates, new Random(1))!;

        Assert.Multiple(() =>
        {
            Assert.That(result.ColdStart, Is.True);
            Assert.That(result.Charts[0].SongId, Is.InRange(0, 2));
            Assert.That(result.Charts[1].SongId, Is.InRange(3, 7));
            Assert.That(result.Charts[2].SongId, Is.InRange(10, 19));
        });
    }

    [Test]
    public void ReturnsNullWithFewerThanThreeCandidates()
    {
        Assert.That(KadaiPicker.Pick([1, 2, 3, 4, 5], [Chart(1, 3), Chart(2, 4)], new Random(1)), Is.Null);
    }

    [Test]
    public void CatalogReadsTableAndEstimatesMissingCharts()
    {
        var catalog = KadaiDifficultyCatalog.FromJson("""
            {"goals":["SSS","SSS+","AP","AP+","五星"],"rating_bin":500,"min_ds":11.0,
             "fit":{"SSS":[[10,0],[14,4]],"SSS+":[],"AP":[],"AP+":[],"五星":[]},
             "charts":{"100":[{"li":3,"d":[1.5,2,3,4,5],"p":[[0,1000,9000],[0,0,0],[0,0,0],[0,0,0],[0,0,0]]}]}}
            """);

        Assert.Multiple(() =>
        {
            Assert.That(catalog.TryGetDifficulty(100, 3, KadaiGoal.Ap, 13, null, out var d) ? d : double.NaN, Is.EqualTo(3));
            Assert.That(catalog.TryGetDifficulty(200, 3, KadaiGoal.Sss, 13, 12, out var est) ? est : double.NaN, Is.EqualTo(2).Within(1e-9));
            Assert.That(catalog.TryGetDifficulty(200, 3, KadaiGoal.Ap, 13, 12, out _), Is.False);
            Assert.That(catalog.TryGetDifficulty(200, 1, KadaiGoal.Sss, 7.5, 7.4, out _), Is.False);
            Assert.That(catalog.TryGetDifficulty(200, 3, KadaiGoal.Sss, 13, 9.9, out _), Is.False);
            Assert.That(catalog.TryGetDifficulty(200, 3, KadaiGoal.Sss, 13, 10, out var edge) ? edge : double.NaN, Is.EqualTo(0).Within(1e-9));
            Assert.That(catalog.TryGetDifficulty(200, 3, KadaiGoal.Sss, 13, null, out _), Is.False);
            Assert.That(catalog.RateAt(100, 3, KadaiGoal.Sss, 600), Is.EqualTo(0.1));
            Assert.That(catalog.RateAt(100, 3, KadaiGoal.Sss, 99999), Is.EqualTo(0.9));
            Assert.That(catalog.RateAt(200, 3, KadaiGoal.Sss, 600), Is.Null);
        });
    }

    [Test]
    public void GoalReachedRules()
    {
        Assert.Multiple(() =>
        {
            Assert.That(KadaiGoal.Sss.Reached(Score(100.0), 0), Is.True);
            Assert.That(KadaiGoal.SssPlus.Reached(Score(100.4999), 0), Is.False);
            Assert.That(KadaiGoal.Ap.Reached(Score(100.6, fc: "app"), 0), Is.True);
            Assert.That(KadaiGoal.ApPlus.Reached(Score(100.9, fc: "ap"), 0), Is.False);
            Assert.That(KadaiGoal.FiveStar.Reached(Score(100, dx: 970), 1000), Is.True);
            Assert.That(KadaiGoal.FiveStar.Reached(Score(100, dx: 969), 1000), Is.False);
            Assert.That(KadaiGoal.Sss.Reached(null, 0), Is.False);
        });
    }

    [Test]
    public void ConstantCapIsHighestBucketWithEnoughPlaysAndHits()
    {
        var played = new List<(double, bool)>();
        played.AddRange(Enumerable.Range(0, 20).Select(i => (13.7, i < 8)));   // 13.5 段 8/20
        played.AddRange(Enumerable.Range(0, 20).Select(i => (14.2, i < 3)));   // 14.0 段 3/20
        played.AddRange(Enumerable.Range(0, 20).Select(i => (14.6, i < 1)));   // 14.5 段 1/20 < 10%
        played.AddRange(Enumerable.Range(0, 5).Select(_ => (15.0, true)));     // 15.0 段只打过 5 首

        Assert.Multiple(() =>
        {
            Assert.That(KadaiPicker.ConstantCap(played), Is.EqualTo(14.0));
            Assert.That(KadaiPicker.ConstantCap(played.Where(x => x.Item1 < 14)), Is.EqualTo(13.5));
            Assert.That(KadaiPicker.ConstantCap([(13.0, false), (13.2, false)]), Is.Null);
            Assert.That(KadaiPicker.Bucket(14.5), Is.EqualTo(14.5));
            Assert.That(KadaiPicker.Bucket(14.4), Is.EqualTo(14.0));
        });
    }

    [TestCase("14+", true)]
    [TestCase("紫谱", true)]
    [TestCase("14+ 紫谱", true)]
    [TestCase("ap", false)]
    [TestCase("不存在的谱师名字xyz", false)]
    public void FilterParsesSelectorsButNotThresholds(string filter, bool ok)
    {
        Assert.That(PlateData.TryParseFilter(filter, [], [], out _), Is.EqualTo(ok));
    }

    private static KadaiPicker.Chart Chart(long id, double difficulty, double? achievement = null, double? rate = null) =>
        new(id, 3, difficulty, rate, achievement is { } a ? Score(a) : null);

    private static SongScore Score(double achievement, string fc = "", int dx = 0) => new()
    {
        Achievement = achievement, Fc = fc, Fs = "", DxScore = dx, Title = "t", Type = "DX"
    };
}
