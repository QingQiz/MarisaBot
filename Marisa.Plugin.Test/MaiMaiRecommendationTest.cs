using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Marisa.Plugin.Shared.MaiMaiDx;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiRecommendationTest
{
    [Test]
    public void DifficultyCatalogInterpolatesAndFallsBackWithoutExtrapolation()
    {
        const string json = """
        {
          "1": {"charts": [{"li": 0, "ds": 14.0, "kind": "fitted_ds", "curve": [[10000, 14.1], [11000, 14.3]], "pooled": 0.2, "band_pct": 70, "score_rank": {"rank": 2, "of": 10}}]},
          "2": {"charts": [{"li": 0, "ds": 13.0, "kind": "band_pct", "curve": [[10000, 20], [11000, 40]], "pooled": null, "band_pct": 35, "score_rank": {"rank": 8, "of": 20}}]}
        }
        """;
        var catalog = RecommendationDifficultyCatalog.FromJson(json);

        Assert.Multiple(() =>
        {
            Assert.That(catalog.TryEvaluate(1, 0, 10500, out var fitted), Is.True);
            Assert.That(fitted.Value, Is.EqualTo(14.2).Within(0.000001));
            Assert.That(fitted.Personalized, Is.True);
            Assert.That(fitted.Rank, Is.EqualTo(2));

            Assert.That(catalog.TryEvaluate(1, 0, 9000, out var fittedFallback), Is.True);
            Assert.That(fittedFallback.Value, Is.EqualTo(14.2).Within(0.000001));
            Assert.That(fittedFallback.Personalized, Is.False);

            Assert.That(catalog.TryEvaluate(2, 0, 9000, out var percentileFallback), Is.True);
            Assert.That(percentileFallback.Value, Is.EqualTo(35));
            Assert.That(percentileFallback.Personalized, Is.False);
        });
    }

    [Test]
    public void QuickRecommendationsNeverExposeBoundaryAchievement()
    {
        var songs = new List<MaiMaiSong>
        {
            CreateSong(1, false, 14.0),
            CreateSong(2, false, 14.0),
            CreateSong(101, true, 14.0),
            CreateSong(102, true, 14.0)
        };
        var rating = new DxRating
        {
            Nickname = "tester",
            OldScores = [CreateScore(1, 14.0, 100.4998)],
            NewScores = [CreateScore(101, 14.0, 100.4998)]
        };

        var result = new MaiMaiRecommendationEngine(
            songs, RecommendationDifficultyCatalog.Empty, new Random(0)).BuildQuick(rating);

        Assert.That(result.Items, Is.Not.Empty);
        Assert.That(result.Items.Any(x => IsBoundary(x.TargetAchievement)), Is.False);
        Assert.That(result.Items.Where(x => x.Action == "upgrade").Select(x => x.TargetAchievement),
            Is.All.EqualTo(100.5));
    }

    [Test]
    public void PlanPrefersEasierFittedChartWhenEffortAndGainMatch()
    {
        var songs = new List<MaiMaiSong>
        {
            CreateSong(1, false, 14.0),
            CreateSong(2, false, 14.0)
        };
        var rating = new DxRating
        {
            Nickname = "tester",
            OldScores = [CreateScore(1, 14.0, 99.9), CreateScore(2, 14.0, 99.9)],
            NewScores = []
        };
        const string json = """
        {
          "1": {"charts": [{"li": 0, "ds": 14.0, "kind": "fitted_ds", "curve": [[0, 14.4], [20000, 14.4]], "pooled": 0.4, "band_pct": 80, "score_rank": {"rank": 1, "of": 2}}]},
          "2": {"charts": [{"li": 0, "ds": 14.0, "kind": "fitted_ds", "curve": [[0, 13.8], [20000, 13.8]], "pooled": -0.2, "band_pct": 20, "score_rank": {"rank": 2, "of": 2}}]}
        }
        """;
        var engine = new MaiMaiRecommendationEngine(
            songs, RecommendationDifficultyCatalog.FromJson(json), new Random(0));

        var result = engine.BuildPlan(rating, rating.Rating + 1);

        Assert.Multiple(() =>
        {
            Assert.That(result.Status, Is.EqualTo(MaiMaiRecommendationPlanStatus.Success));
            Assert.That(result.Data!.ProjectedRating, Is.GreaterThanOrEqualTo(result.Data.TargetRating));
            Assert.That(result.Data.Items, Has.Count.EqualTo(1));
            Assert.That(result.Data.Items[0].SongId, Is.EqualTo(2));
        });
    }

    [Test]
    public void PlanReportsAlreadyReachedAndUnreachableTargets()
    {
        var song = CreateSong(1, false, 14.0);
        var rating = new DxRating
        {
            Nickname = "tester",
            OldScores = [CreateScore(1, 14.0, 100.5)],
            NewScores = []
        };
        var engine = new MaiMaiRecommendationEngine([song], RecommendationDifficultyCatalog.Empty, new Random(0));

        Assert.Multiple(() =>
        {
            Assert.That(engine.BuildPlan(rating, rating.Rating).Status,
                Is.EqualTo(MaiMaiRecommendationPlanStatus.AlreadyReached));
            Assert.That(engine.BuildPlan(rating, rating.Rating + 1).Status,
                Is.EqualTo(MaiMaiRecommendationPlanStatus.Unreachable));
        });
    }

    private static bool IsBoundary(double value)
    {
        var ticks = (int)Math.Round(value * 10000, MidpointRounding.AwayFromZero);
        return ticks is 799999 or 969999 or 989999 or 999999 or 1004999;
    }

    private static MaiMaiSong CreateSong(long id, bool isNew, double constant)
    {
        dynamic song = new ExpandoObject();
        song.id = id.ToString();
        song.title = $"song-{id}";
        song.type = "DX";

        dynamic info = new ExpandoObject();
        info.title = song.title;
        info.artist = "artist";
        info.genre = "genre";
        info.bpm = 180;
        info.release_date = "2026-01-01";
        info.from = isNew ? "new" : "old";
        info.is_new = isNew;
        song.basic_info = info;

        song.ds = new[] { constant };
        song.level = new[] { constant.ToString("0.0") };

        dynamic chart = new ExpandoObject();
        chart.notes = new long[] { 100, 10, 10, 0 };
        chart.charter = "tester";
        song.charts = new[] { chart };
        return new MaiMaiSong(song);
    }

    private static SongScore CreateScore(long id, double constant, double achievement)
    {
        return new SongScore
        {
            Id = id,
            Type = "DX",
            Constant = constant,
            Achievement = achievement,
            LevelIdx = 0,
            Level = constant.ToString("0.0"),
            Title = $"song-{id}",
            Fc = "",
            Fs = ""
        };
    }
}
