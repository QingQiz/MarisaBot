using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using Flurl.Http.Testing;
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

    [Test]
    public void SharedSongListAttachesWorldsEndByOriginId()
    {
        using var reset = ChunithmVersusModelTest.UseNationalSongs(null);
        using var http = new HttpTest();
        http.RespondWithJson(new
        {
            versions = new[] { new { version = 10000, title = "CHUNITHM" } },
            songs = new[]
            {
                Song(1, "song-1", Enumerable.Range(0, 4).Select(d => (object)new { difficulty = d, level = "14", level_value = 14.0, note_designer = "c", notes = new { total = 1000 } }).ToArray()),
                Song(8020, "song-1", [WorldsEnd(1, "狂")]),
                Song(8030, "we-only", [WorldsEnd(99, "改")])
            }
        });

        var songs = LxnsDataFetcher.GetSharedSongList();
        var song = songs.Single(s => s.Id == 1);
        var weOnly = songs.Single(s => s.Id == 99);
        Assert.Multiple(() =>
        {
            Assert.That(song.DiffNames, Is.EqualTo(new[] { "BASIC", "ADVANCED", "EXPERT", "MASTER", "WORLD'S END" }));
            Assert.That((song.Levels[4], song.ChartName[4], song.MaxCombo[4]), Is.EqualTo(("狂", "8020", 3000L)));
            Assert.That((weOnly.Title, weOnly.Version), Is.EqualTo(("we-only", "CHUNITHM")));
            Assert.That(weOnly.DiffNames, Is.EqualTo(new[] { "WORLD'S END" }));
            Assert.That(ChunithmVersusCatalog.WorldsEndCharts(songs).Keys, Is.EquivalentTo(new long[] { 8020, 8030 }));
            Assert.That(ChunithmVersusCatalog.StandardCharts(songs).Select(chart => chart.Id).Distinct(), Is.EqualTo(new long[] { 1 }));
        });
        return;

        object Song(long id, string title, object[] difficulties) =>
            new { id, title, artist = "artist", genre = "原创", version = 10000, bpm = 200, difficulties };

        object WorldsEnd(long origin, string kanji) =>
            new { difficulty = 5, level = "0", level_value = 0, note_designer = "we", origin_id = origin, kanji, notes = new { total = 3000 } };
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
