using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.Configuration;
using Marisa.Plugin.Shared.MaiMaiDx;
using Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;
using Marisa.Plugin.Shared.Util.SongDb;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class MaiMaiDxDivingFishDataFetcherTest
{
    [TestCase("白", "maimai MiLK")]
    [TestCase("雪", "MiLK PLUS")]
    [TestCase("maimai MiLK", "maimai MiLK")]
    [TestCase("不存在的版本", null)]
    public void ResolveSummaryVersions_Should_Resolve_Alias_And_Exact_Version(string input, string? expected)
    {
        var method = typeof(MaiMaiDx.MaiMaiDx).GetMethod("ResolveSummaryVersions", BindingFlags.NonPublic | BindingFlags.Static);
        var actual = (string[]?)method!.Invoke(null, [input, new[] { "maimai MiLK", "MiLK PLUS" }]);

        Assert.That(actual, Is.EqualTo(expected == null ? null : new[] { expected }));
    }

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
    public void BuildRating_Should_Split_OAuth_Scores_By_Release_And_Keep_Top_35_15()
    {
        var songDb = CreateSongDb();
        var fetcher = new LxnsDataFetcher(songDb);
        var scores = Enumerable.Range(1, 40)
            .Select(i => CreateSongScore(i, 10.0 + i / 100.0, 100 + i))
            .Concat(Enumerable.Range(1001, 20)
                .Select(i => CreateSongScore(i, 11.0 + i / 1000.0, 200 + i)))
            .ToDictionary(score => (score.Id, score.LevelIdx));
        var method = typeof(LxnsDataFetcher).GetMethod("BuildRating", BindingFlags.NonPublic | BindingFlags.Instance);

        var rating = (DxRating)method!.Invoke(fetcher, [scores, "tester"])!;

        Assert.Multiple(() =>
        {
            Assert.That(rating.Nickname, Is.EqualTo("tester"));
            Assert.That(rating.OldScores, Has.Count.EqualTo(35));
            Assert.That(rating.NewScores, Has.Count.EqualTo(15));
            Assert.That(rating.OldScores.Select(x => x.Id), Is.EquivalentTo(Enumerable.Range(6, 35).Select(i => (long)i)));
            Assert.That(rating.NewScores.Select(x => x.Id), Is.EquivalentTo(Enumerable.Range(1006, 15).Select(i => (long)i)));
        });
    }

    [Test]
    public void BuildVersionList_Should_Prefer_Version_Whose_Majority_Ids_Are_Smaller()
    {
        var songs = new List<MaiMaiSong>
        {
            CreateSong(1, false, "maimai でらっくす FESTiVAL"),
            CreateSong(300, false, "maimai でらっくす FESTiVAL"),
            CreateSong(310, false, "maimai でらっくす FESTiVAL"),
            CreateSong(100, false, "maimai"),
            CreateSong(110, false, "maimai"),
            CreateSong(120, false, "maimai"),
            CreateSong(200, false, "maimai でらっくす"),
            CreateSong(210, false, "maimai でらっくす"),
            CreateSong(220, false, "maimai でらっくす")
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

    /// <summary>构造取数目标；测试里被测的 fetcher 就是目标自己的 fetcher。</summary>
    private static ResolvedPlayer Target(DataFetcher fetcher, long qq = 1, string? username = null, bool isSelf = true) =>
        new(qq, username, isSelf, fetcher);

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
        var fetcher = new RecordingDivingFishDataFetcher(songDb, oldRecords.Concat(newRecords).ToList());

        var rating = await fetcher.GetRating(Target(fetcher));

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

    [Test]
    public async Task GetScores_Should_Query_Target_By_Username()
    {
        var expected = CreateSongScore(42, 13.0, 100.5);
        var fetcher  = new RecordingDivingFishDataFetcher(CreateSongDb(), [expected]);

        var (_, scores) = await fetcher.GetScores(Target(fetcher, username: "target"));

        Assert.Multiple(() =>
        {
            Assert.That(fetcher.LastUsername, Is.EqualTo("target"));
            Assert.That(fetcher.LastQq, Is.Null);
            Assert.That(scores[(expected.Id, expected.LevelIdx)], Is.SameAs(expected));
        });
    }

    [Test]
    public async Task GetScores_Should_Query_Target_By_Qq()
    {
        var expected = CreateSongScore(43, 13.0, 100.5);
        var fetcher  = new RecordingDivingFishDataFetcher(CreateSongDb(), [expected]);

        var (nickname, scores) = await fetcher.GetScores(Target(fetcher, qq: 7));

        Assert.Multiple(() =>
        {
            Assert.That(fetcher.LastQq, Is.EqualTo(7), "命令参数不会被当作用户名：目标由 ResolvePlayer 解析，见 MaiMaiResolvePlayerTest");
            Assert.That(fetcher.LastUsername, Is.Null);
            Assert.That(nickname, Is.EqualTo("tester"));
            Assert.That(scores[(expected.Id, expected.LevelIdx)], Is.SameAs(expected));
        });
    }

    [Test]
    public async Task GetScores_OAuth_Username_Should_Use_Public_Target_Records()
    {
        var expected = CreateSongScore(43, 13.0, 100.5);
        var fetcher  = new PublicDivingFishDataFetcher(CreateSongDb(), [expected], []);

        var (_, scores) = await fetcher.GetScores(Target(fetcher, username: "target"));

        Assert.That(scores[(expected.Id, expected.LevelIdx)], Is.SameAs(expected));
    }

    [Test]
    public async Task GetScores_UsesGivenQqAndKeepsRecordsOutsideB50()
    {
        var best       = CreateSongScore(1, 13.0, 100.5);
        var outsideB50 = CreateSongScore(2, 13.0, 90);
        var fetcher    = new PrivateVersusDivingFishDataFetcher(CreateSongDb(), [best, outsideB50]);

        var (nickname, scores) = await fetcher.GetScores(Target(fetcher, qq: 2, isSelf: false));

        Assert.Multiple(() =>
        {
            Assert.That(fetcher.RequestedQq, Is.EqualTo(2), "用目标的 QQ 取数，不查公开 B50");
            Assert.That(nickname, Is.EqualTo("authorized"));
            Assert.That(scores.Keys, Is.EquivalentTo(new[] { (1L, 0), (2L, 0) }));
            Assert.That(scores[(2, 0)], Is.SameAs(outsideB50));
        });
    }

    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.Forbidden)]
    public void GetScores_Should_Not_Fallback_On_Http_Error(HttpStatusCode status)
    {
        var error   = new HttpRequestException("target inaccessible", null, status);
        var fetcher = new ThrowingDivingFishDataFetcher(CreateSongDb(), error);

        var actual = Assert.ThrowsAsync<HttpRequestException>(() => fetcher.GetScores(Target(fetcher)));

        Assert.That(actual, Is.SameAs(error));
    }

    [Test]
    public void GetScores_Should_Propagate_Missing_Configuration()
    {
        var error   = new MissingConfigurationException("divingFish.devToken");
        var fetcher = new ThrowingDivingFishDataFetcher(CreateSongDb(), error);

        var actual = Assert.ThrowsAsync<MissingConfigurationException>(() => fetcher.GetScores(Target(fetcher)));

        Assert.That(actual, Is.SameAs(error));
    }

    [Test]
    public async Task GetRating_PublicResponse_PreservesServerAuthoritativeSplit()
    {
        var songDb = CreateSongDb();
        var publicOld = new List<SongScore> { CreateSongScore(9001, 13.0, 100.5) };
        var publicNew = new List<SongScore> { CreateSongScore(9002, 13.0, 100.5) };
        var fetcher = new PublicDivingFishDataFetcher(songDb, publicOld, publicNew);

        var rating = await fetcher.GetRating(Target(fetcher));

        Assert.Multiple(() =>
        {
            Assert.That(rating.OldScores.Select(x => x.Id), Is.EqualTo(new[] { 9001L }));
            Assert.That(rating.NewScores.Select(x => x.Id), Is.EqualTo(new[] { 9002L }));
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

    private sealed class PrivateVersusDivingFishDataFetcher(SongDb<MaiMaiSong> songDb, List<SongScore> records) : DivingFishDataFetcher(songDb)
    {
        public long RequestedQq { get; private set; }

        public override Task<DxRating> GetRating(ResolvedPlayer player) =>
            throw new AssertionException("完整成绩不走公开 B50");

        protected override Task<DivingFishDxRatingResponse> FetchScores(long qq)
        {
            RequestedQq = qq;
            return Task.FromResult(new DivingFishDxRatingResponse("authorized", records));
        }

        protected override Task<DivingFishDxRatingResponse> FetchScores(ReadOnlyMemory<char> username) =>
            throw new AssertionException("这条路径不应按账号名查询");
    }

    private sealed class ThrowingDivingFishDataFetcher(SongDb<MaiMaiSong> songDb, Exception error) : DivingFishDataFetcher(songDb)
    {
        protected override Task<DivingFishDxRatingResponse> FetchScores(long qq) => throw error;
    }

    private sealed class RecordingDivingFishDataFetcher(SongDb<MaiMaiSong> songDb, List<SongScore> records) : DivingFishDataFetcher(songDb)
    {
        protected override bool OAuthEnabled => false;

        public long? LastQq { get; private set; }
        public string? LastUsername { get; private set; }

        protected override Task<DivingFishDxRatingResponse> FetchScores(long qq)
        {
            LastQq = qq;
            return Task.FromResult(new DivingFishDxRatingResponse("tester", records));
        }

        protected override Task<DivingFishDxRatingResponse> FetchScores(ReadOnlyMemory<char> username)
        {
            LastUsername = username.ToString();
            return Task.FromResult(new DivingFishDxRatingResponse("tester", records));
        }
    }

    private sealed class PublicDivingFishDataFetcher(
        SongDb<MaiMaiSong> songDb,
        List<SongScore> oldScores,
        List<SongScore> newScores) : DivingFishDataFetcher(songDb)
    {
        protected override bool OAuthEnabled => true;

        protected override Task<DivingFishDxRatingResponse> FetchScoresByQq(long qq)
        {
            return Task.FromResult(new DivingFishDxRatingResponse(
                "public",
                oldScores.Concat(newScores).ToList(),
                oldScores,
                newScores));
        }

        protected override Task<DivingFishDxRatingResponse> FetchScoresByUsername(ReadOnlyMemory<char> username)
        {
            return Task.FromResult(new DivingFishDxRatingResponse(
                username.ToString(),
                oldScores.Concat(newScores).ToList(),
                oldScores,
                newScores));
        }
    }

}
