using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Chunithm.DataFetcher;
using Marisa.Plugin.Shared.Util.SongDb;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class ChunithmLxnsDataFetcherTest
{
    [Test]
    public void BuildRating_Should_Split_OAuth_Scores_By_Current_Versions()
    {
        var songs = new[]
        {
            CreateSong(1, "old", "CHUNITHM A"),
            CreateSong(2, "new-one", "CHUNITHM B"),
            CreateSong(3, "new-two", "CHUNITHM C")
        };
        var fetcher = new TestLxnsDataFetcher(new SongDb<ChunithmSong>("", "", () => songs.ToList()), songs);
        var scores = new Dictionary<(long Id, int LevelIdx), ChunithmScore>
        {
            [(1, 0)] = CreateScore(1, "old", 1000000),
            [(2, 0)] = CreateScore(2, "new-one", 1000000),
            [(3, 0)] = CreateScore(3, "new-two", 1000000)
        };
        var method = typeof(LxnsDataFetcher).GetMethod("BuildRating", BindingFlags.NonPublic | BindingFlags.Instance);
        var newest = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CHUNITHM B", "CHUNITHM C" };

        var rating = (ChunithmRating)method!.Invoke(fetcher, [scores, "tester", newest])!;

        Assert.Multiple(() =>
        {
            Assert.That(rating.Username, Is.EqualTo("tester"));
            Assert.That(rating.Records.Best.Select(x => x.Id), Is.EqualTo(new[] { 1L }));
            Assert.That(rating.Records.Recent.Select(x => x.Id), Is.EquivalentTo(new[] { 2L, 3L }));
        });
    }

    private static ChunithmSong CreateSong(long id, string title, string version)
    {
        dynamic song = new ExpandoObject();
        song.Id = id;
        song.Title = title;
        song.Artist = "artist";
        song.Genre = "genre";
        song.Version = version;

        dynamic beatmap = new ExpandoObject();
        beatmap.Constant = 14.0;
        beatmap.Charter = "-";
        beatmap.LevelStr = "14";
        beatmap.LevelName = "MASTER";
        beatmap.ChartName = "";
        beatmap.Bpm = "200";
        beatmap.MaxCombo = 1000;
        song.Beatmaps = new[] { beatmap };
        return new ChunithmSong(song);
    }

    private static ChunithmScore CreateScore(long id, string title, int achievement) => new()
    {
        Id = id,
        Title = title,
        LevelIndex = 0,
        Achievement = achievement,
        Constant = 14.0m,
        Level = "14",
        LevelLabel = "MASTER",
        Fc = string.Empty
    };

    private sealed class TestLxnsDataFetcher(
        SongDb<ChunithmSong> songDb,
        IReadOnlyList<ChunithmSong> songs) : LxnsDataFetcher(songDb)
    {
        public override List<ChunithmSong> GetSongList() => songs.ToList();
    }
}
