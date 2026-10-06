using System;
using System.Collections.Generic;
using System.Linq;
using Marisa.Plugin.Shared.MaiMaiDx;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiVersusMultiTest
{
    [Test]
    public void EveryPairOnAChartIsOneMatchup()
    {
        var cells = Round([Chart(1)], Player(1, (1, 100.5)), Player(2, (1, 99.0)), Player(3)).Rows.Single().Cells;

        Assert.Multiple(() =>
        {
            Assert.That(cells.Select(c => c.Points), Is.EqualTo(new[] { 2.0, 1.0, 0.0 }));
            Assert.That(cells.Select(c => c.Matchups), Is.EqualTo(new[] { 2, 2, 2 }));
            Assert.That(cells.Select(c => c.Place), Is.EqualTo(new int?[] { 1, 2, null }));
        });
    }

    [Test]
    public void TiesSplitTheMatchupAndUnplayedPairsDoNotCount()
    {
        var round = Round([Chart(1), Chart(2)], Player(1, (1, 100.0), (2, 99.0)), Player(2, (1, 100.0)), Player(3));

        Assert.That(round.Totals, Is.EqualTo(new MaiVersusMulti.Total[] { new(3.5, 4), new(1.5, 3), new(0, 3) }));
    }

    [Test]
    public void TwoPlayerPointsEqualHeadToHeadWins()
    {
        var charts = Enumerable.Range(1, 6).Select(id => Chart(id)).ToArray();
        var a      = Player(1, (1, 100.0), (2, 99.0), (3, 98.0), (5, 97.0));
        var b      = Player(2, (1, 99.0), (2, 99.0), (4, 98.0), (5, 98.0));
        var multi  = Round(charts, a, b);
        var batch  = new MaiVersusBatch("", "", "", charts, new(a.Name, a.Scores), new(b.Name, b.Scores));

        Assert.Multiple(() =>
        {
            Assert.That(multi.Totals[0].Points, Is.EqualTo(batch.Summary.LeftWins + batch.Summary.Draws * 0.5));
            Assert.That(multi.Totals[1].Points, Is.EqualTo(batch.Summary.RightWins + batch.Summary.Draws * 0.5));
        });
    }

    [Test]
    public void SingleLeaderboardStampsOnlyTheLowestPlayedScore()
    {
        var single = Round([Chart(1)], Player(1, (1, 99.0)), Player(2), Player(3, (1, 100.5)), Player(4, (1, 97.0)))
            .GetSingle(0, []);

        Assert.Multiple(() =>
        {
            Assert.That(single.Players.Select(p => p.Name), Is.EqualTo(new[] { "p3", "p1", "p4", "p2" }));
            Assert.That(single.Players.Select(p => p.Stamp), Is.EqualTo(new[] { false, false, true, false }));
        });
    }

    [Test]
    public void TiedForLowestAreAllStamped()
    {
        var single = Round([Chart(1)], Player(1, (1, 100.0)), Player(2, (1, 98.0)), Player(3, (1, 98.0))).GetSingle(0, []);

        Assert.That(single.Players.Select(p => p.Stamp), Is.EqualTo(new[] { false, true, true }));
    }

    [Test]
    public void NoStampWithoutSomeoneBehind()
    {
        var tie  = Round([Chart(1)], Player(1, (1, 100.0)), Player(2, (1, 100.0))).GetSingle(0, []);
        var solo = Round([Chart(1)], Player(1, (1, 100.0)), Player(2)).GetSingle(0, []);

        Assert.That(tie.Players.Concat(solo.Players).Any(p => p.Stamp), Is.False);
    }

    [Test]
    public void PageColumnsFollowRoundPlaces()
    {
        var round = Round([Chart(1), Chart(2)],
            Player(1, (1, 98.0)), Player(2, (1, 100.0), (2, 100.0)), Player(3, (1, 99.0), (2, 99.0)));
        var page = round.GetPage(1, 3, []);

        Assert.Multiple(() =>
        {
            Assert.That(page.Players.Select(p => (p.Name, p.Place, p.Points)),
                Is.EqualTo(new[] { ("p2", 1, 4.0), ("p3", 2, 2.0), ("p1", 3, 0.0) }));
            Assert.That(page.Rows[0].Cells.Select(c => c.Score.Achievement), Is.EqualTo(new double?[] { 100.0, 99.0, 98.0 }));
        });
    }

    [Test]
    public void PicksOnlyChartsAtLeastTwoPlayersPlayed()
    {
        var charts = Enumerable.Range(1, 5).Select(id => Chart(id)).ToArray();
        var scores = Scores(Plays(1, 2, 3, 4, 5), Plays(1, 2), Plays(3));

        var picked = MaiVersusMulti.PickCharts(charts, scores, 10, new Random(1));

        Assert.That(picked.Select(x => x.Song.Id), Is.EquivalentTo(new long[] { 1, 2, 3 }));
    }

    [Test]
    public void KeepsFullCoverageWhenThePoolIsLargeEnough()
    {
        var charts = Enumerable.Range(1, 30).Select(id => Chart(id)).ToArray();
        var full   = Enumerable.Range(1, 12).ToArray();
        var scores = Scores(Plays(Enumerable.Range(1, 30).ToArray()), Plays(Enumerable.Range(1, 30).ToArray()), Plays(full));
        var random = new Random(7);

        for (var i = 0; i < 200; i++)
        {
            Assert.That(MaiVersusMulti.PickCharts(charts, scores, 5, random).Select(x => (int)x.Song.Id), Is.SubsetOf(full));
        }
    }

    [Test]
    public void LowersTheThresholdAndDoublesWeightPerExtraPlayer()
    {
        var charts = Enumerable.Range(1, 24).Select(id => Chart(id)).ToArray();
        var all    = Enumerable.Range(1, 24).ToArray();
        var scores = Scores(Plays(all), Plays(all), Plays(1, 2, 3, 4));
        var random = new Random(42);
        var counts = new int[25];

        for (var i = 0; i < 6000; i++)
        {
            counts[MaiVersusMulti.PickCharts(charts, scores, 1, random).Single().Song.Id]++;
        }

        var full    = counts[1..5].Average();
        var partial = counts[5..25].Average();
        Assert.Multiple(() =>
        {
            Assert.That(counts[5..25].All(c => c > 0), Is.True);
            Assert.That(full / partial, Is.InRange(1.7, 2.3));
        });
    }

    [Test]
    public void ReturnsWhatIsAvailableSortedByConstant()
    {
        var charts = new[] { Chart(1, 13.0), Chart(2, 14.5), Chart(3, 12.0) };
        var scores = Scores(Plays(1, 2, 3), Plays(1, 2, 3));

        var picked = MaiVersusMulti.PickCharts(charts, scores, 20, new Random(3));

        Assert.That(picked.Select(x => x.Song.Id), Is.EqualTo(new long[] { 2, 1, 3 }));
    }

    internal static (double Constant, int LevelIdx, MaiMaiSong Song) Chart(long id, double constant = 14.0) =>
        (constant, 3, MaiMaiVersusCommandTest.Song(id, $"song-{id}"));

    internal static MaiVersusMulti.Player Player(long qq, params (long Id, double Achievement)[] plays) =>
        new(qq, $"p{qq}", plays.ToDictionary(x => (x.Id, 3), x => new SongScore { Achievement = x.Achievement, DxScore = 100 }));

    internal static MaiVersusMulti Round(
        IReadOnlyList<(double Constant, int LevelIdx, MaiMaiSong Song)> charts, params MaiVersusMulti.Player[] players) =>
        new("测试", "", charts, players);

    private static IReadOnlyDictionary<(long Id, int LevelIdx), SongScore> Plays(params int[] ids) =>
        ids.ToDictionary(id => ((long)id, 3), _ => new SongScore { Achievement = 100.0 });

    private static IReadOnlyList<IReadOnlyDictionary<(long Id, int LevelIdx), SongScore>> Scores(
        params IReadOnlyDictionary<(long Id, int LevelIdx), SongScore>[] scores) => scores;
}
