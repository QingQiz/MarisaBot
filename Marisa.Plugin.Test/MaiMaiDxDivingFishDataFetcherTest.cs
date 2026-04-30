using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;
using Marisa.Plugin.Shared.Util.SongDb;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiDxDivingFishDataFetcherTest
{
    [Test]
    public void BuildVersionList_Should_Deduplicate_And_Keep_Chronological_Order()
    {
        var songs = new List<MaiMaiSong>
        {
            CreateSong(300, false, "maimai でらっくす FESTiVAL"),
            CreateSong(100, false, "maimai"),
            CreateSong(200, false, "maimai でらっくす"),
            CreateSong(400, false, "maimai でらっくす FESTiVAL"),
            CreateSong(250, false, "maimai でらっくす")
        };

        var method = typeof(MaiMaiDx.MaiMaiDx).GetMethod("BuildVersionList", BindingFlags.NonPublic | BindingFlags.Static);
        var versions = (IReadOnlyList<string>)method!.Invoke(null, [songs])!;

        Assert.That(versions, Is.EqualTo(new[]
        {
            "maimai",
            "maimai でらっくす",
            "maimai でらっくす FESTiVAL"
        }));
    }

    [Test]
    public async Task GetRating_Should_Keep_Top_35_Old_And_Top_15_New_By_IsNew()
    {
        var songDb = CreateSongDb();
        var oldRecords = Enumerable.Range(1, 40)
            .Select(i => CreateSongScore(i, 10.0 + i / 100.0, 100 + i))
            .ToList();
        var newRecords = Enumerable.Range(1001, 20)
            .Select(i => CreateSongScore(i, 11.0 + i / 1000.0, 200 + i))
            .ToList();
        var fetcher = new TestDivingFishDataFetcher(songDb, oldRecords.Concat(newRecords).ToList());
        var message = new Message(null!, [])
        {
            Sender = new SenderInfo(1, "test")
        };

        var rating = await fetcher.GetRating(message);

        Assert.Multiple(() =>
        {
            Assert.That(rating.Nickname, Is.EqualTo("tester"));
            Assert.That(rating.OldScores, Has.Count.EqualTo(35));
            Assert.That(rating.NewScores, Has.Count.EqualTo(15));
            Assert.That(rating.OldScores.Select(x => x.Id), Is.EquivalentTo(Enumerable.Range(6, 35).Select(i => (long)i)));
            Assert.That(rating.NewScores.Select(x => x.Id), Is.EquivalentTo(Enumerable.Range(1006, 15).Select(i => (long)i)));
            Assert.That(rating.OldScores, Is.Ordered.Descending.By(nameof(SongScore.Rating)).Then.Descending.By(nameof(SongScore.Id)));
            Assert.That(rating.NewScores, Is.Ordered.Descending.By(nameof(SongScore.Rating)).Then.Descending.By(nameof(SongScore.Id)));
        });
    }

    private static SongDb<MaiMaiSong> CreateSongDb()
    {
        return new SongDb<MaiMaiSong>("", "", () =>
        {
            var oldSongs = Enumerable.Range(1, 40).Select(i => CreateSong(i, false));
            var newSongs = Enumerable.Range(1001, 20).Select(i => CreateSong(i, true));
            return oldSongs.Concat(newSongs).ToList();
        });
    }

    private static MaiMaiSong CreateSong(long id, bool isNew, string version = "test")
    {
        dynamic song = new ExpandoObject();
        song.id = id.ToString();
        song.title = $"song-{id}";
        song.type = "SD";

        dynamic basicInfo = new ExpandoObject();
        basicInfo.title = $"song-{id}";
        basicInfo.artist = "artist";
        basicInfo.genre = "genre";
        basicInfo.bpm = 120;
        basicInfo.release_date = "2024-01-01";
        basicInfo.from = version;
        basicInfo.is_new = isNew;
        song.basic_info = basicInfo;

        song.ds = new[] { 13.0 };
        song.level = new[] { "13" };

        dynamic chart = new ExpandoObject();
        chart.notes = new long[] { 100, 10, 10, 0 };
        chart.charter = "-";
        song.charts = new[] { chart };

        return new MaiMaiSong(song);
    }

    private static SongScore CreateSongScore(long id, double constant, double achievement)
    {
        return new SongScore
        {
            Id = id,
            Type = "SD",
            Constant = constant,
            Achievement = achievement,
            LevelIdx = 0,
            Level = "13",
            Title = $"song-{id}",
            Fc = string.Empty,
            Fs = string.Empty,
            DxScore = 0
        };
    }

    private sealed class TestDivingFishDataFetcher(SongDb<MaiMaiSong> songDb, List<SongScore> records) : DivingFishDataFetcher(songDb)
    {
        protected override Task<DivingFishDxRatingResponse> FetchScores(Message message, bool qqOnly)
        {
            return Task.FromResult(new DivingFishDxRatingResponse("tester", records));
        }
    }
}
