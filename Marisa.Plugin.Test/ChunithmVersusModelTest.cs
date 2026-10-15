using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Versus;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class ChunithmVersusModelTest
{
    [Test]
    public void StandardChartsUseNamesAndExcludeWeAtUltimaIndexAndWeOnlySongs()
    {
        var standard = Song(1);
        var shifted = Song(2, "shifted", names: ["BASIC", "ADVANCED", "EXPERT", "MASTER", "WORLD'S END", "WORLD'S END"]);
        var onlyWe = Song(3, "we", names: ["WORLD'S END"]);
        var charts = ChunithmVersusCatalog.StandardCharts([standard, shifted, onlyWe]);
        Assert.Multiple(() =>
        {
            Assert.That(charts.Where(chart => chart.Id == 1).Select(chart => chart.LevelIndex), Is.EqualTo(new[] { 0, 1, 2, 3, 4 }));
            Assert.That(charts.Where(chart => chart.Id == 2).Select(chart => chart.LevelIndex), Is.EqualTo(new[] { 0, 1, 2, 3 }));
            Assert.That(charts.Any(chart => chart.Id == 3), Is.False);
            Assert.That(ChunithmVersusCatalog.FindChart(shifted, 4), Is.Null);
        });
    }

    [Test]
    public void NormalizesIdsByUniqueTitleAndKeepsActualChartIndex()
    {
        var song = Song(7, "known", names: ["MASTER", "ULTIMA"]);
        var records = new[] { Score(999, 3, 1_009_001, title: "known"), Score(888, 5, 1_010_000, title: "known"), Score(777, 3, 1_000_000, title: "missing") };
        var normalized = ChunithmVersusCatalog.NormalizeScores(records, [song]);
        var chart = ChunithmVersusCatalog.FindChart(song, 3)!;
        Assert.Multiple(() =>
        {
            Assert.That(normalized.Keys, Is.EqualTo(new[] { (7L, 3) }));
            Assert.That(chart.ChartIndex, Is.Zero);
            Assert.That(chart.LevelIndex, Is.EqualTo(3));
            Assert.That(normalized[(7, 3)].Constant, Is.EqualTo(14m));
            Assert.That(records[0].Id, Is.EqualTo(999), "对战归一化不能修改原始记录");
        });
    }

    [Test]
    public void AmbiguousTitlesAndIdTitleConflictsDoNotBecomeDifferentSongs()
    {
        var catalog = new[] { Song(1, "same"), Song(2, "same"), Song(3, "other") };
        var records = new[] { Score(999, 3, 1_000_000, title: "same"), Score(3, 3, 1_000_000, title: "same") };
        Assert.That(ChunithmVersusCatalog.NormalizeScores(records, catalog), Is.Empty);
    }

    [Test]
    public void CatalogueMembershipDefinesCurrentScopeInsteadOfHistoricDeletionIds()
    {
        var catalog = new[] { Song(1), Song(156) };
        var normalized = ChunithmVersusCatalog.NormalizeScores([Score(1, 3, 1_000_000), Score(156, 3, 1_009_000)], catalog);
        Assert.Multiple(() =>
        {
            Assert.That(ChunithmVersusCatalog.StandardCharts(catalog).Select(chart => chart.Id).Distinct(), Is.EqualTo(new[] { 1L, 156L }));
            Assert.That(normalized.Keys, Is.EqualTo(new[] { (1L, 3), (156L, 3) }));
            Assert.That(ChunithmVersusCatalog.NormalizeScores([Score(156, 3, 1_009_000)], [Song(1)]), Is.Empty);
        });
    }

    [Test]
    public void CatalogDoesNotApplyDivingFishSpecificWorldsEndIdConvention()
    {
        var song = Song(8020);
        Assert.That(ChunithmVersusCatalog.FindChart(song, 0), Is.Not.Null);
        Assert.That(ChunithmVersusCatalog.NormalizeScores([Score(8020, 0, 0)], [song]).ContainsKey((8020, 0)), Is.True);
    }

    [TestCase(1_009_001, 1_009_000, "left")]
    [TestCase(1_009_000, 1_009_001, "right")]
    [TestCase(1_010_000, 1_010_000, "draw")]
    [TestCase(0, 0, "draw")]
    public void ComparesIntegerScoresWithoutFcOrChainTieBreak(int left, int right, string expected)
    {
        var chart = ChunithmVersusCatalog.FindChart(Song(1), 3)!;
        var a = Score(1, 3, left); a.Fc = "alljusticecritical"; a.FullChain = "fullchain2";
        var b = Score(1, 3, right); b.Fc = "";
        var row = Batch([chart], Player(1, a), Player(2, b)).GetPage(1).Rows.Single();
        Assert.That(row.Outcome, Is.EqualTo(expected));
    }

    [Test]
    public void ZeroIsPlayedAndMissingBothIsSeparateFromDraw()
    {
        var charts = new[] { ChunithmVersusCatalog.FindChart(Song(1), 3)!, ChunithmVersusCatalog.FindChart(Song(2), 3)! };
        var page = Batch(charts, Player(1, Score(1, 3, 0)), Player(2)).GetPage(1);
        Assert.Multiple(() =>
        {
            Assert.That(page.Rows[0].Outcome, Is.EqualTo("left"));
            Assert.That(page.Rows[0].Left.Score, Is.Zero);
            Assert.That(page.Rows[1].Outcome, Is.EqualTo("unplayed"));
            Assert.That(page.Rows[1].Left.Score, Is.Null);
            Assert.That(page.Summary.Unplayed, Is.EqualTo(1));
            Assert.That(page.Summary.Draws, Is.Zero);
        });
    }

    [TestCase(20, 1)]
    [TestCase(21, 2)]
    [TestCase(40, 2)]
    [TestCase(41, 3)]
    public void PagesIncludeEveryChartAndWholeScopeSummary(int count, int pages)
    {
        var charts = Enumerable.Range(1, count).Select(id => ChunithmVersusCatalog.FindChart(Song(id), 3)!).ToArray();
        var batch = Batch(charts, Player(1, Score(count, 3, 1)), Player(2));
        var data = Enumerable.Range(1, batch.PageCount).Select(batch.GetPage).ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(batch.PageCount, Is.EqualTo(pages));
            Assert.That(data.SelectMany(page => page.Rows).Select(row => row.Id), Is.EqualTo(Enumerable.Range(1, count).Select(id => (long)id)));
            Assert.That(data.All(page => page.Summary.LeftWins == 1 && page.Summary.Unplayed == count - 1), Is.True);
        });
    }

    [Test]
    public void ProjectionRecomputesRatingAndDoesNotInferAjcFromMaximumScore()
    {
        var chart = ChunithmVersusCatalog.FindChart(Song(1), 4)!;
        var score = Score(1, 4, 1_010_000);
        score.Constant = 1m;
        _ = score.Rating;
        score.Fc = "fullchain2";
        var data = ChunithmVersusBatch.GetSingle(chart, Player(1, score), Player(2));
        Assert.Multiple(() =>
        {
            Assert.That(data.LevelName, Is.EqualTo("ULTIMA"));
            Assert.That(data.Players[0].Score.Rating, Is.EqualTo(16.65m));
            Assert.That(data.Players[0].Score.FullCombo, Is.Empty);
            Assert.That(data.Players[0].Score.FullChain, Is.EqualTo("fullchain2"));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public void SameScoreRecordsRetainIndependentKnownMarksRegardlessOfOrder(bool reverse)
    {
        var first = Score(1, 3, 1_000_000); first.Fc = "alljustice"; first.FullChain = "fullchain"; first.Clear = "brave";
        var second = Score(1, 3, 1_000_000); second.Fc = "alljusticecritical"; second.FullChain = "fullchain2"; second.Clear = "absolute";
        var records = reverse ? new[] { second, first } : new[] { first, second };
        var score = ChunithmVersusCatalog.NormalizeScores(records, [Song(1)])[(1, 3)];
        var view = ChunithmVersusBatch.CreateScore(new System.Collections.Generic.Dictionary<(long, int), ChunithmScore> { [(1, 3)] = score },
            ChunithmVersusCatalog.FindChart(Song(1), 3)!);
        Assert.Multiple(() =>
        {
            Assert.That(view.FullCombo, Is.EqualTo("alljusticecritical"));
            Assert.That(view.FullChain, Is.EqualTo("fullchain2"));
            Assert.That(view.Clear, Is.EqualTo("absolute"));
        });
    }

    [Test]
    public void MissingClearAndUnplayedRecordsProjectEmptyWithoutInferringFromScore()
    {
        var chart = ChunithmVersusCatalog.FindChart(Song(1), 3)!;
        var view = ChunithmVersusBatch.GetSingle(chart, Player(1, Score(1, 3, 1_010_000)), Player(2));
        Assert.That(view.Players.Select(player => player.Score.Clear), Is.EqualTo(new[] { "", "" }));
    }

    [Test]
    public void MultiplayerUsesSharedRankingAndUnplayedPlayersNeverReceiveStamp()
    {
        var chart = ChunithmVersusCatalog.FindChart(Song(1), 3)!;
        var multi = new ChunithmVersusMulti("title", "", [chart],
            [Player(1, Score(1, 3, 1_009_001)), Player(2, Score(1, 3, 1_009_000)), Player(3)]);
        var single = multi.GetSingle(1, []);
        Assert.Multiple(() =>
        {
            Assert.That(single.Players.Select(player => player.Place), Is.EqualTo(new int?[] { 1, 2, null }));
            Assert.That(single.Players.Select(player => player.Stamp), Is.EqualTo(new[] { false, true, false }));
            Assert.That(multi.Totals.Select(total => total.Points), Is.EqualTo(new[] { 2d, 1d, 0d }));
            Assert.That(multi.ForRoom().Select(player => player.Qq), Is.EqualTo(new[] { 1L, 2L, 3L }));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RoomProjectionPreservesPlayerOrderIdentityAndMatchupTotals(bool allUnplayed)
    {
        var charts = Enumerable.Range(1, 3).Select(id => ChunithmVersusCatalog.FindChart(Song(id), 3)!).ToArray();
        var players = allUnplayed
            ? new[] { Player(30), Player(10), Player(20) }
            : new[]
            {
                Player(30, Score(1, 3, 1_000_000)),
                Player(10, Score(1, 3, 1_009_000), Score(2, 3, 1_009_000)),
                Player(20, Score(1, 3, 1_009_000))
            };
        players[0] = players[0] with { Name = "same" };
        players[1] = players[1] with { Name = "other" };
        players[2] = players[2] with { Name = "same" };
        var multi = new ChunithmVersusMulti("title", "", charts, players);

        Assert.That(multi.ForRoom(), Is.EqualTo(allUnplayed
            ? new VersusRoundPlayer[] { new(30, "same", 0, 0), new(10, "other", 0, 0), new(20, "same", 0, 0) }
            : new VersusRoundPlayer[] { new(30, "same", 0, 3), new(10, "other", 3.5, 4), new(20, "same", 1.5, 3) }));
    }

    internal static ChunithmSong Song(long id, string? title = null, string version = "CHUNITHM VERSE", string genre = "ORIGINAL", string[]? names = null)
    {
        dynamic song = new ExpandoObject();
        song.Id = id; song.Title = title ?? $"song-{id}"; song.Artist = "artist"; song.Genre = genre; song.Version = version;
        song.Beatmaps = (names ?? ChunithmSong.LevelLabel.Take(5).ToArray()).Select(name =>
        {
            dynamic chart = new ExpandoObject();
            var level = ChunithmVersusCatalog.DifficultyIndex(name);
            chart.Constant = level switch { 0 => 2.0, 1 => 5.0, 2 => 10.0, 3 => 14.0, 4 => 14.5, _ => 0.0 };
            chart.LevelStr = level == 4 ? "14+" : ((int)chart.Constant).ToString();
            chart.LevelName = name; chart.Charter = "-"; chart.ChartName = ""; chart.Bpm = "200"; chart.MaxCombo = 1000;
            return chart;
        }).ToArray();
        return new ChunithmSong(song);
    }

    internal static ChunithmScore Score(long id, int level, int score, string? title = null) => new()
    {
        Id = id, LevelIndex = level, Achievement = score, Title = title ?? $"song-{id}", LevelLabel = "", Fc = "", Constant = 1m, Level = "1"
    };

    internal static VersusPlayer<ChunithmScore> Player(long qq, params ChunithmScore[] scores) =>
        new(qq, $"player-{qq}", scores.ToDictionary(score => (score.Id, (int)score.LevelIndex)));

    private static ChunithmVersusBatch Batch(IReadOnlyList<ChunithmVersusChart> charts,
        VersusPlayer<ChunithmScore> a, VersusPlayer<ChunithmScore> b) => new("scope", "", "定数降序", charts, a, b);
}
