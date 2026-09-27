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
    public void ResolveSummaryVersion_Should_Resolve_Alias_And_Exact_Version(string input, string? expected)
    {
        var method = typeof(MaiMaiDx.MaiMaiDx).GetMethod("ResolveSummaryVersion", BindingFlags.NonPublic | BindingFlags.Static);
        var actual = (string?)method!.Invoke(null, [input, new[] { "maimai MiLK", "MiLK PLUS" }]);

        Assert.That(actual, Is.EqualTo(expected));
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

    [Test]
    public async Task GetScores_Should_Pass_Username_Query_To_DivingFish()
    {
        var expected = CreateSongScore(42, 13.0, 100.5);
        var fetcher = new TestDivingFishDataFetcher(CreateSongDb(), [expected]);
        var message = new Message(null!, [])
        {
            Sender = new SenderInfo(1, "sender"),
            Command = "target".AsMemory()
        };

        var scores = await fetcher.GetScores(message, true);

        Assert.Multiple(() =>
        {
            Assert.That(fetcher.LastQqOnly, Is.False);
            Assert.That(scores[(expected.Id, expected.LevelIdx)], Is.SameAs(expected));
        });
    }

    [Test]
    public async Task GetScores_Should_Ignore_Command_When_Username_Not_Allowed()
    {
        var expected = CreateSongScore(45, 13.0, 100.5);
        var fetcher = new TestDivingFishDataFetcher(CreateSongDb(), [expected]);
        var message = new Message(null!, [])
        {
            Sender = new SenderInfo(1, "sender"),
            Command = "14+".AsMemory()
        };

        var scores = await fetcher.GetScores(message);

        Assert.Multiple(() =>
        {
            Assert.That(fetcher.LastQqOnly, Is.True, "命令参数不应被当作用户名");
            Assert.That(scores[(expected.Id, expected.LevelIdx)], Is.SameAs(expected));
        });
    }

    [Test]
    public async Task GetScores_Should_Use_Qq_Query_When_Command_Is_Empty()
    {
        var expected = CreateSongScore(43, 13.0, 100.5);
        var fetcher = new TestDivingFishDataFetcher(CreateSongDb(), [expected]);
        var message = new Message(null!, [])
        {
            Sender = new SenderInfo(1, "sender"),
            Command = "".AsMemory()
        };

        var scores = await fetcher.GetScores(message);

        Assert.Multiple(() =>
        {
            Assert.That(fetcher.LastQqOnly, Is.True);
            Assert.That(scores[(expected.Id, expected.LevelIdx)], Is.SameAs(expected));
        });
    }

    [Test]
    public async Task GetScores_OAuth_Username_Should_Use_Public_Target_Records()
    {
        var expected = CreateSongScore(43, 13.0, 100.5);
        var fetcher = new PublicDivingFishDataFetcher(CreateSongDb(), [expected], []);
        var message = new Message(null!, [])
        {
            Sender = new SenderInfo(1, "sender"),
            Command = "target".AsMemory()
        };

        var scores = await fetcher.GetScores(message, true);

        Assert.That(scores[(expected.Id, expected.LevelIdx)], Is.SameAs(expected));
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetVersusData_Username_Should_Not_Request_Full_Records(bool oauthEnabled)
    {
        var expected = CreateSongScore(44, 13.0, 100.5);
        var fetcher = new PublicVersusDivingFishDataFetcher(CreateSongDb(), [expected], [], oauthEnabled);
        var message = new Message(null!, [])
        {
            Sender = new SenderInfo(1, "sender"),
            Command = "target".AsMemory()
        };

        var data = await fetcher.GetVersusData(message, true);

        Assert.Multiple(() =>
        {
            Assert.That(data.Partial, Is.True);
            Assert.That(data.Nickname, Is.EqualTo("target"));
            Assert.That(data.Scores[(expected.Id, expected.LevelIdx)], Is.SameAs(expected));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task GetVersusData_Qq_DoesNotRequirePublicB50(bool mention)
    {
        var expected = CreateSongScore(44, 13, 95);
        var fetcher = new PrivateVersusDivingFishDataFetcher(CreateSongDb(), [expected]);
        var message = new Message(null!, mention ? [new MessageDataAt(2)] : [])
        {
            Sender = new SenderInfo(1, "sender"), Command = "".AsMemory()
        };
        var result = await fetcher.GetVersusData(message, false);
        Assert.That(result.Partial, Is.False);
        Assert.That(result.Nickname, Is.EqualTo("authorized"));
        Assert.That(result.Scores[(44, 0)], Is.SameAs(expected));
        Assert.That(fetcher.RequestedQq, Is.EqualTo(mention ? 2 : 1));
    }

    [Test]
    public void GetVersusData_EmptyUsernameDoesNotFallBackToSender()
    {
        var fetcher = new PublicVersusDivingFishDataFetcher(CreateSongDb(), [], [], true);
        var message = new Message(null!, []) { Sender = new SenderInfo(1, "sender") };
        Assert.ThrowsAsync<ArgumentException>(() => fetcher.GetVersusData(message, true));
    }

    [Test]
    public async Task GetVersusData_Qq_Should_Preserve_Complete_Records_Outside_B50()
    {
        var best = CreateSongScore(1, 13.0, 100.5);
        var outsideB50 = CreateSongScore(2, 13.0, 90);
        var fetcher = new VersusDataFetcher(CreateSongDb(), [best], [best, outsideB50]);
        var message = new Message(null!, []) { Sender = new SenderInfo(1, "sender") };

        var data = await fetcher.GetVersusData(message, false);

        Assert.Multiple(() =>
        {
            Assert.That(data.Partial, Is.False);
            Assert.That(data.Scores.Keys, Is.EquivalentTo(new[] { (1L, 0), (2L, 0) }));
            Assert.That(data.Scores[(2, 0)], Is.SameAs(outsideB50));
            Assert.That(fetcher.RatingMessage, Is.SameAs(message));
            Assert.That(fetcher.ScoresMessage, Is.SameAs(message));
        });
    }

    [Test]
    public async Task GetVersusData_Qq_Should_Mark_Unsupported_Full_Records_As_Partial()
    {
        var best = CreateSongScore(1, 13.0, 100.5);
        var fetcher = new VersusDataFetcher(CreateSongDb(), [best], [], new NotSupportedException());
        var message = new Message(null!, []) { Sender = new SenderInfo(1, "sender") };

        var data = await fetcher.GetVersusData(message, false);

        Assert.Multiple(() =>
        {
            Assert.That(data.Partial, Is.True);
            Assert.That(data.Scores.Keys, Is.EqualTo(new[] { (1L, 0) }));
            Assert.That(data.Scores[(1, 0)], Is.SameAs(best));
            Assert.That(data.Nickname, Is.EqualTo("target"));
        });
    }

    [TestCase(HttpStatusCode.BadRequest)]
    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.Forbidden)]
    public void GetVersusData_Qq_Should_Not_Fallback_On_Http_Error(HttpStatusCode status)
    {
        var error = new HttpRequestException("target inaccessible", null, status);
        var fetcher = new VersusDataFetcher(CreateSongDb(), [CreateSongScore(1, 13, 100)], [], error);
        var message = new Message(null!, []) { Sender = new SenderInfo(1, "sender") };

        var actual = Assert.ThrowsAsync<HttpRequestException>(() => fetcher.GetVersusData(message, false));

        Assert.That(actual, Is.SameAs(error));
    }

    [Test]
    public void GetVersusData_Should_Propagate_Missing_Configuration()
    {
        var error = new MissingConfigurationException("divingFish.devToken");
        var fetcher = new VersusDataFetcher(CreateSongDb(), [CreateSongScore(1, 13, 100)], [], error);
        var message = new Message(null!, []) { Sender = new SenderInfo(1, "sender") };

        var actual = Assert.ThrowsAsync<MissingConfigurationException>(() => fetcher.GetVersusData(message, false));

        Assert.That(actual, Is.SameAs(error));
    }

    [Test]
    public async Task GetRating_PublicResponse_PreservesServerAuthoritativeSplit()
    {
        var songDb = CreateSongDb();
        var publicOld = new List<SongScore> { CreateSongScore(9001, 13.0, 100.5) };
        var publicNew = new List<SongScore> { CreateSongScore(9002, 13.0, 100.5) };
        var fetcher = new PublicDivingFishDataFetcher(songDb, publicOld, publicNew);
        var message = new Message(null!, [])
        {
            Sender = new SenderInfo(1, "test")
        };

        var rating = await fetcher.GetRating(message);

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

        public override Task<DxRating> GetRating(Message message, bool allowUsername = false) =>
            throw new AssertionException("authorized records must not depend on public B50 visibility");

        protected override Task<DivingFishDxRatingResponse> FetchScores(Message message, bool qqOnly)
        {
            Assert.That(qqOnly, Is.True);
            var (username, qq) = Shared.Chunithm.DataFetcher.DataFetcher.AtOrSelf(message, qqOnly);
            Assert.That(username.IsEmpty, Is.True);
            RequestedQq = qq;
            return Task.FromResult(new DivingFishDxRatingResponse("authorized", records));
        }
    }

    private sealed class TestDivingFishDataFetcher(SongDb<MaiMaiSong> songDb, List<SongScore> records) : DivingFishDataFetcher(songDb)
    {
        protected override bool OAuthEnabled => false;

        public bool? LastQqOnly { get; private set; }

        protected override Task<DivingFishDxRatingResponse> FetchScores(Message message, bool qqOnly)
        {
            LastQqOnly = qqOnly;
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

    private sealed class PublicVersusDivingFishDataFetcher(
        SongDb<MaiMaiSong> songDb,
        List<SongScore> oldScores,
        List<SongScore> newScores,
        bool oauthEnabled) : DivingFishDataFetcher(songDb)
    {
        protected override bool OAuthEnabled => oauthEnabled;

        protected override Task<DivingFishDxRatingResponse> FetchScoresByUsername(ReadOnlyMemory<char> username)
        {
            return Task.FromResult(new DivingFishDxRatingResponse(
                username.ToString(),
                oldScores.Concat(newScores).ToList(),
                oldScores,
                newScores));
        }

        public override Task<Dictionary<(long Id, int LevelIdx), SongScore>> GetScores(Message message, bool allowUsername = false)
        {
            throw new AssertionException("username versus query must not request full records");
        }

        protected override Task<DivingFishDxRatingResponse> FetchScoresByQq(long qq)
        {
            throw new AssertionException("username versus query must not fall back to sender QQ");
        }

        protected override Task<DivingFishDxRatingResponse> FetchScores(Message message, bool qqOnly)
        {
            throw new AssertionException("username versus query must only use the public username endpoint");
        }
    }

    private sealed class VersusDataFetcher(
        SongDb<MaiMaiSong> songDb,
        List<SongScore> bestScores,
        List<SongScore> allScores,
        Exception? fullScoresError = null) : DataFetcher(songDb)
    {
        public Message? RatingMessage { get; private set; }
        public Message? ScoresMessage { get; private set; }

        public override Task<DxRating> GetRating(Message message, bool allowUsername = false)
        {
            RatingMessage = message;
            return Task.FromResult(new DxRating { Nickname = "target", OldScores = bestScores, NewScores = [] });
        }

        public override Task<Dictionary<(long Id, int LevelIdx), SongScore>> GetScores(Message message, bool allowUsername = false)
        {
            ScoresMessage = message;
            if (fullScoresError != null) throw fullScoresError;
            return Task.FromResult(allScores.ToDictionary(score => (score.Id, score.LevelIdx)));
        }
    }
}
