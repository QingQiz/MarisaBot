using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Marisa.Plugin.Shared.MaiMaiDx;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiVersusBatchTest
{
    [TestCase(true, true, "draw")]
    [TestCase(true, false, "left")]
    [TestCase(false, true, "right")]
    [TestCase(false, false, "unplayed")]
    public void MissingRecordsCountAsUnplayed(bool leftPlayed, bool rightPlayed, string outcome)
    {
        var song = CreateSong(1);
        var batch = CreateBatch(
            [(14.0, 3, song)],
            PlayerWithState(leftPlayed, 1),
            PlayerWithState(rightPlayed, 1));
        var row = batch.GetPage(1).Rows.Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.Left.State, Is.EqualTo(leftPlayed ? "played" : "unplayed"));
            Assert.That(row.Right.State, Is.EqualTo(rightPlayed ? "played" : "unplayed"));
            Assert.That(row.Outcome, Is.EqualTo(outcome));
            Assert.That(batch.Summary.LeftWins, Is.EqualTo(outcome == "left" ? 1 : 0));
            Assert.That(batch.Summary.RightWins, Is.EqualTo(outcome == "right" ? 1 : 0));
            Assert.That(batch.Summary.Draws, Is.EqualTo(outcome == "draw" ? 1 : 0));
            Assert.That(batch.Summary.Unplayed, Is.EqualTo(outcome == "unplayed" ? 1 : 0));
            if (!leftPlayed) Assert.That(row.Left.Achievement, Is.Null);
            if (!rightPlayed) Assert.That(row.Right.Achievement, Is.Null);
        });
    }

    [TestCase(100.0001, 100.0000, "left")]
    [TestCase(100.0000, 100.0001, "right")]
    [TestCase(100.0000, 100.0000, "draw")]
    [TestCase(0, 0, "draw")]
    public void ComparesAchievementWithoutRoundingOrDxScoreTieBreak(double left, double right, string outcome)
    {
        var song = CreateSong(1);
        var leftScore = CreateScore(1, 3, left);
        var rightScore = CreateScore(1, 3, right);
        leftScore.DxScore = 0;
        rightScore.DxScore = 150;
        var row = CreateBatch([(14.0, 3, song)], Player(leftScore), Player(rightScore)).GetPage(1).Rows.Single();

        Assert.That(row.Outcome, Is.EqualTo(outcome));
    }

    [Test]
    public void ZeroAchievementIsAPlayedRecordAndBeatsMissingCompleteRecord()
    {
        var song = CreateSong(1);
        var row = CreateBatch([(14.0, 3, song)], Player(CreateScore(1, 3, 0)), Player())
            .GetPage(1).Rows.Single();

        Assert.Multiple(() =>
        {
            Assert.That(row.Left.State, Is.EqualTo("played"));
            Assert.That(row.Left.Achievement, Is.Zero);
            Assert.That(row.Outcome, Is.EqualTo("left"));
        });
    }

    [TestCase(1, 1)]
    [TestCase(20, 1)]
    [TestCase(21, 2)]
    [TestCase(40, 2)]
    [TestCase(41, 3)]
    public void PaginatesEveryTwentyChartsWithoutDroppingOrRepeatingRows(int count, int expectedPages)
    {
        var charts = Enumerable.Range(1, count).Select(id => (14.0, 3, CreateSong(id))).ToArray();
        var batch = CreateBatch(charts, Player(), Player());
        var pages = Enumerable.Range(1, batch.PageCount).Select(batch.GetPage).ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(batch.PageSize, Is.EqualTo(20));
            Assert.That(batch.PageCount, Is.EqualTo(expectedPages));
            Assert.That(batch.TotalCharts, Is.EqualTo(count));
            Assert.That(pages.SelectMany(page => page.Rows).Select(row => row.Id),
                Is.EqualTo(Enumerable.Range(1, count).Select(id => (long)id)));
            Assert.That(pages.Take(pages.Length - 1).All(page => page.Rows.Count == 20), Is.True);
            Assert.That(pages.Last().Rows.Count, Is.EqualTo((count - 1) % 20 + 1));
            Assert.That(pages.Select(page => page.Page), Is.EqualTo(Enumerable.Range(1, expectedPages)));
            Assert.That(pages.All(page => page.TotalCharts == count && page.PageSize == 20), Is.True);
        });
    }

    [Test]
    public void EveryPageIncludesSummaryOfTheWholeScope()
    {
        var charts = Enumerable.Range(1, 21).Select(id => (14.0, 3, CreateSong(id))).ToArray();
        var leftScores = Enumerable.Range(1, 21).Select(id => CreateScore(id, 3, 100)).ToArray();
        var rightScores = Enumerable.Range(1, 20).Select(id => CreateScore(id, 3, 100)).ToArray();
        var batch = CreateBatch(charts, Player(leftScores), Player(rightScores));

        Assert.Multiple(() =>
        {
            Assert.That(batch.GetPage(1).Rows.All(row => row.Outcome == "draw"), Is.True);
            Assert.That(batch.GetPage(2).Rows.Single().Outcome, Is.EqualTo("left"));
            Assert.That(batch.Summary.Draws, Is.EqualTo(20));
            Assert.That(batch.Summary.LeftWins, Is.EqualTo(1));
            Assert.That(batch.GetPage(1).Summary, Is.EqualTo(batch.Summary));
            Assert.That(batch.GetPage(2).Summary, Is.EqualTo(batch.Summary));
        });
    }

    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(3)]
    public void RejectsPagesOutsideScope(int page)
    {
        var charts = Enumerable.Range(1, 21).Select(id => (14.0, 3, CreateSong(id))).ToArray();
        var batch = CreateBatch(charts, Player(), Player());

        Assert.Throws<ArgumentOutOfRangeException>(() => batch.GetPage(page));
    }

    [Test]
    public void ProjectionUsesCatalogRatingAndCarriesScopeAndChartMetadata()
    {
        var song = CreateSong(42);
        var score = CreateScore(42, 3, 100.5);
        score.Constant = 1.0;
        score.Fc = "app";
        score.Fs = "fsdp";
        var page = CreateBatch([(14.0, 3, song)], Player(score), Player()).GetPage(1);
        var row = page.Rows.Single();

        Assert.Multiple(() =>
        {
            Assert.That(page.Scope, Is.EqualTo("彩代 紫"));
            Assert.That(page.SortLabel, Is.EqualTo("定数降序"));
            Assert.That(page.Version, Is.EqualTo("maimai でらっくす BUDDiES"));
            Assert.That(page.Players.Select(player => player.Name), Is.EqualTo(new[] { "player", "player" }));
            Assert.That(row.Id, Is.EqualTo(42));
            Assert.That(row.Title, Is.EqualTo("song-42"));
            Assert.That(row.Type, Is.EqualTo("DX"));
            Assert.That(row.LevelIndex, Is.EqualTo(3));
            Assert.That(row.Level, Is.EqualTo("14"));
            Assert.That(row.Constant, Is.EqualTo(14));
            Assert.That(row.MaxDx, Is.EqualTo(600));
            Assert.That(row.Left.Rating, Is.EqualTo(315));
            Assert.That(row.Left.Rank, Is.EqualTo("sssp"));
            Assert.That(row.Left.Fc, Is.EqualTo("app"));
            Assert.That(row.Left.Fs, Is.EqualTo("fsdp"));
        });
    }

    private static MaiVersusBatch CreateBatch(
        IReadOnlyList<(double Constant, int LevelIdx, MaiMaiSong Song)> charts,
        MaiVersusBatch.Player left,
        MaiVersusBatch.Player right)
    {
        return new MaiVersusBatch("彩代 紫", "maimai でらっくす BUDDiES", "定数降序", charts, left, right);
    }

    private static MaiVersusBatch.Player PlayerWithState(bool played, long id)
    {
        SongScore[] scores = played ? [CreateScore(id, 3, 100)] : [];
        return new MaiVersusBatch.Player("player", scores.ToDictionary(score => (score.Id, score.LevelIdx)));
    }

    private static MaiVersusBatch.Player Player(params SongScore[] scores)
    {
        return new MaiVersusBatch.Player("player", scores.ToDictionary(score => (score.Id, score.LevelIdx)));
    }

    private static SongScore CreateScore(long id, int levelIdx, double achievement)
    {
        return new SongScore { Id = id, LevelIdx = levelIdx, Achievement = achievement };
    }

    private static MaiMaiSong CreateSong(long id)
    {
        dynamic song = new ExpandoObject();
        song.id = id.ToString();
        song.title = $"song-{id}";
        song.type = "DX";
        dynamic basicInfo = new ExpandoObject();
        basicInfo.title = song.title;
        basicInfo.artist = "artist";
        basicInfo.genre = "genre";
        basicInfo.bpm = 120;
        basicInfo.release_date = "2024-01-01";
        basicInfo.from = "maimai でらっくす BUDDiES";
        basicInfo.is_new = false;
        song.basic_info = basicInfo;
        song.ds = new[] { 3.0, 7.0, 12.0, 14.0, 14.5 };
        song.level = new[] { "3", "7", "12", "14", "14+" };
        song.charts = Enumerable.Range(0, 5).Select(_ =>
        {
            dynamic chart = new ExpandoObject();
            chart.notes = new long[] { 100, 20, 30, 40, 10 };
            chart.charter = "-";
            return chart;
        }).ToArray();
        return new MaiMaiSong(song);
    }
}
