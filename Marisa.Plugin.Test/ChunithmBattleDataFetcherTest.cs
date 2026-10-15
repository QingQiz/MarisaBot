using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Flurl.Http.Testing;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Chunithm.DataFetcher;
using Marisa.Plugin.Shared.DivingFish;
using Marisa.Plugin.Shared.Lxns;
using Marisa.Plugin.Shared.Util.SongDb;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

[NonParallelizable]
public class ChunithmBattleDataFetcherTest
{
    private const long Self = 81001, Opponent = 81002;
    private string _testRoot = null!;
    private string _restoreConfig = null!;
    private HttpTest _http = null!;

    [SetUp]
    public void SetUp()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), nameof(ChunithmBattleDataFetcherTest), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);
        _restoreConfig = Path.GetFullPath(Path.Combine(TestContext.CurrentContext.TestDirectory, "../../../..", "Marisa.StartUp/config.yaml"));
        var path = Path.Combine(_testRoot, "config.yaml");
        File.WriteAllText(path, $"tempPath: '{_testRoot}'\ndatabasePath: battle-test.db\n");
        ConfigurationManager.SetConfigFilePath(path);
        _http = new HttpTest();
    }

    [TearDown]
    public void TearDown()
    {
        _http.Dispose();
        ConfigurationManager.SetConfigFilePath(_restoreConfig);
        Directory.Delete(_testRoot, true);
    }

    [Test]
    public async Task DivingFishReadsOpponentsOwnChunithmAuthorizationAndKeepsMoreThanB30()
    {
        AuthorizeFish(Self, "sender-ticket");
        AuthorizeFish(Opponent, "opponent-ticket");
        var songs = Enumerable.Range(1, 35).Select(id => ChunithmVersusModelTest.Song(id)).ToList();
        var fetcher = new OAuthFishFetcher(new SongDb<ChunithmSong>("", "", () => songs));
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records").RespondWithJson(new
        {
            nickname = "opponent", rating = 0,
            records = new
            {
                best = songs.Select(song => new { mid = song.Id, title = song.Title, level_index = 3, level_label = "MASTER", score = 1_009_000, fc = "alljusticecritical", full_chain = "fullchain2" }).ToArray(),
                r10 = Array.Empty<object>()
            }
        });

        var data = await fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher));
        Assert.Multiple(() =>
        {
            Assert.That(data.Nickname, Is.EqualTo("opponent"));
            Assert.That(data.Scores, Has.Count.EqualTo(35));
            Assert.That(data.Scores[(35, 3)].FullCombo, Is.EqualTo("alljusticecritical"));
            Assert.That(data.Scores[(35, 3)].FullChain, Is.EqualTo("fullchain"));
            Assert.That(_http.CallLog.Single().Request.Headers.First(header => header.Name == "Authorization").Value, Is.EqualTo("Bearer opponent-ticket"));
        });
    }

    [Test]
    public void DivingFishWithoutTargetAuthorizationFailsWithoutPublicFallback()
    {
        AuthorizeFish(Self, "sender-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        var error = Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(() => fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher)));
        Assert.That(error!.Message, Does.Contain("未绑定水鱼"));
        Assert.That(_http.CallLog, Is.Empty);
    }

    [Test]
    public void OrdinaryDivingFishCompleteQueryKeepsExistingSelfOnlyPolicy()
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(() => fetcher.GetScores(new ResolvedPlayer(Opponent, null, false, fetcher)));
        Assert.That(_http.CallLog, Is.Empty);
    }

    [Test]
    public void DivingFishServiceFailureDoesNotBecomeAnEmptyOrPublicScoreSet()
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records").RespondWith("forbidden", (int)HttpStatusCode.Forbidden);
        Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(() => fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher)));
        Assert.That(_http.CallLog, Has.Count.EqualTo(1));
    }

    [Test]
    public void DivingFishSimplifiedResponseWithoutScoreIsRejectedInsteadOfPlayedZero()
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records").RespondWithJson(new
        {
            nickname = "opponent", rating = 0,
            records = new { best = new[] { new { mid = 1, title = "song-1", level_index = 3, fc = "alljustice" } }, r10 = Array.Empty<object>() }
        });
        Assert.ThrowsAsync<InvalidDataException>(() => fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher)));
    }

    [TestCase("{}")]
    [TestCase("{\"records\":null}")]
    [TestCase("{\"records\":{}}")]
    [TestCase("{\"records\":{\"best\":null}}")]
    [TestCase("{\"records\":{\"best\":{}}}")]
    [TestCase("{\"records\":{\"b30\":[]}}")]
    [TestCase("{\"records\":{\"best\":[null]}}")]
    public void DivingFishRequiresExplicitCompleteBestArray(string wire)
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records").RespondWith(wire);
        Assert.ThrowsAsync<InvalidDataException>(() => fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher)));
        Assert.That(_http.CallLog, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DivingFishExplicitEmptyBestIsValidWithoutRatingOrPublicBestFields()
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records")
            .RespondWith("{\"nickname\":\"opponent\",\"records\":{\"best\":[]}}");
        var data = await fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher));
        Assert.That(data.Scores, Is.Empty);
        Assert.That(data.Nickname, Is.EqualTo("opponent"));
    }

    [TestCase("{\"mid\":1,\"title\":\"song-1\",\"score\":0}")]
    [TestCase("{\"mid\":1,\"title\":\"song-1\",\"score\":0,\"level_label\":\"\"}")]
    [TestCase("{\"mid\":1,\"title\":\"song-1\",\"score\":0,\"level_index\":4,\"level_label\":\"MASTER\"}")]
    [TestCase("{\"mid\":1,\"title\":\"song-1\",\"score\":0,\"level_index\":5,\"level_label\":\"MASTER\"}")]
    [TestCase("{\"mid\":1,\"title\":\"song-1\",\"score\":0,\"level_index\":3.5}")]
    [TestCase("{\"mid\":1,\"title\":\"song-1\",\"score\":0,\"level_index\":null}")]
    [TestCase("{\"mid\":1,\"title\":\"song-1\",\"score\":0,\"level_index\":3,\"level_label\":\"UNKNOWN\"}")]
    public void DivingFishMissingOrConflictingDifficultyDoesNotBecomeBasicOrStandard(string scoreWire)
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records")
            .RespondWith("{\"records\":{\"best\":[" + scoreWire + "]}}");
        Assert.ThrowsAsync<InvalidDataException>(() => fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher)));
    }

    [TestCase("{\"mid\":1,\"title\":\"song-1\",\"score\":0,\"level_index\":0}", 0)]
    [TestCase("{\"mid\":1,\"title\":\"song-1\",\"score\":0,\"level_label\":\"MASTER\"}", 3)]
    [TestCase("{\"mid\":1,\"title\":\"song-1\",\"score\":0,\"level_index\":4,\"level_label\":\"ULTIMA\"}", 4)]
    public async Task DivingFishExplicitDifficultyAndZeroScoreAreValid(string scoreWire, int expectedLevel)
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records")
            .RespondWith("{\"records\":{\"best\":[" + scoreWire + "]}}");
        var data = await fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher));
        Assert.That(data.Scores[(1, expectedLevel)].Achievement, Is.Zero);
    }

    [Test]
    public async Task DivingFishConsistentWorldsEndIsExcluded()
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records")
            .RespondWith("{\"records\":{\"best\":[{\"mid\":1,\"score\":0,\"level_index\":5,\"level_label\":\"WORLD'S END\"}]}}");
        var data = await fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher));
        Assert.That(data.Scores, Is.Empty);
    }

    [TestCase("World's End")]
    [TestCase("WORLD'S END")]
    public async Task DivingFishStandardAndWorldsEndRecordsKeepOnlyStandard(string weLabel)
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records").RespondWithJson(new
        {
            records = new
            {
                best = new[]
                {
                    new { mid = 1, title = "song-1", score = 1_009_000, level_index = 3, level_label = "MASTER" },
                    new { mid = 1, title = "song-1", score = 1_010_000, level_index = 5, level_label = weLabel }
                }
            }
        });
        var data = await fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher));
        Assert.That(data.Scores.Keys, Is.EqualTo(new[] { (1L, 3) }));
        Assert.That(data.Scores[(1, 3)].Achievement, Is.EqualTo(1_009_000));
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DivingFishWorldsEndIdWithBasicMetadataNeverMatchesSameTitleStandard(bool includeStandard)
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        var records = new[]
        {
            new { mid = 1, title = "song-1", score = 1_009_000, level_index = 3, level_label = "MASTER" },
            new { mid = 8020, title = "song-1", score = 1_010_000, level_index = 0, level_label = "Basic" }
        }.Skip(includeStandard ? 0 : 1).ToArray();
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records")
            .RespondWithJson(new { records = new { best = records } });
        var data = await fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher));
        Assert.That(data.Scores.Keys, Is.EqualTo(includeStandard ? new[] { (1L, 3) } : Array.Empty<(long, int)>()));
        Assert.That(data.Scores.ContainsKey((1, 0)), Is.False);
    }

    [Test]
    public async Task LxnsReadsOpponentsPersonalScoresAndPreservesComboAndChainSeparately()
    {
        LxnsTokenStore.SaveToken(Self, "sender-ticket", "fake-refresh", 3600);
        LxnsTokenStore.SaveToken(Opponent, "opponent-ticket", "fake-refresh", 3600);
        var fetcher = new LocalLxnsFetcher(Db());
        _http.ForCallsTo("https://maimai.lxns.net/api/v0/user/chunithm/player/scores").RespondWithJson(new
        {
            data = new[] { new { id = 999, song_name = "song-1", level_index = 3, score = 1_010_000, full_combo = "alljusticecritical", full_chain = "fullchain2" } }
        });
        _http.ForCallsTo("https://maimai.lxns.net/api/v0/user/chunithm/player").RespondWithJson(new { data = new { name = "opponent" } });
        var data = await fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher));
        Assert.Multiple(() =>
        {
            Assert.That(data.Nickname, Is.EqualTo("opponent"));
            Assert.That(data.Scores[(1, 3)].Achievement, Is.EqualTo(1_010_000));
            Assert.That(data.Scores[(1, 3)].FullCombo, Is.EqualTo("alljusticecritical"));
            Assert.That(data.Scores[(1, 3)].FullChain, Is.EqualTo("fullchain"));
            Assert.That(_http.CallLog.All(call => call.Request.Headers.First(header => header.Name == "Authorization").Value == "Bearer opponent-ticket"), Is.True);
        });
    }

    [Test]
    public void LxnsSimplifiedResponseWithoutScoreIsRejectedInsteadOfPlayedZero()
    {
        LxnsTokenStore.SaveToken(Opponent, "opponent-ticket", "fake-refresh", 3600);
        var fetcher = new LocalLxnsFetcher(Db());
        _http.ForCallsTo("https://maimai.lxns.net/api/v0/user/chunithm/player/scores").RespondWithJson(new
        {
            data = new[] { new { id = 1, song_name = "song-1", level_index = 3, full_combo = "alljustice" } }
        });
        Assert.ThrowsAsync<InvalidDataException>(() => fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher)));
        Assert.That(_http.CallLog, Has.Count.EqualTo(1));
    }

    [TestCase(4, "MASTER")]
    [TestCase(5, "MASTER")]
    public void LxnsConflictingDifficultyIsRejected(int index, string label)
    {
        LxnsTokenStore.SaveToken(Opponent, "opponent-ticket", "fake-refresh", 3600);
        var fetcher = new LocalLxnsFetcher(Db());
        _http.ForCallsTo("https://maimai.lxns.net/api/v0/user/chunithm/player/scores").RespondWithJson(new
        {
            data = new[] { new { id = 1, song_name = "song-1", score = 0, level_index = index, level_label = label } }
        });
        _http.ForCallsTo("https://maimai.lxns.net/api/v0/user/chunithm/player").RespondWithJson(new { data = new { name = "opponent" } });
        Assert.ThrowsAsync<InvalidDataException>(() => fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher)));
    }

    [TestCase("fullchain", "fullchain2")]
    [TestCase("fullchain2", "fullchain")]
    public async Task DivingFishLegacyChainIsSeparatedAndMappedFromOfficialIconNaming(string raw, string expected)
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records").RespondWithJson(new
        {
            records = new { best = new[] { new { mid = 1, title = "song-1", score = 1_009_999, level_index = 3, fc = raw, clear = "catastrophy" } } }
        });
        var data = await fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher));
        Assert.Multiple(() =>
        {
            Assert.That(data.Scores[(1, 3)].FullCombo, Is.Empty, "链记录不能虚构个人 COMBO");
            Assert.That(data.Scores[(1, 3)].FullChain, Is.EqualTo(expected));
            Assert.That(data.Scores[(1, 3)].Clear, Is.Null, "水鱼没有保存 clear，不能采用模拟额外字段");
        });
    }

    [TestCase(1_010_000, "alljusticecritical")]
    [TestCase(1_009_999, "")]
    public async Task DivingFishValidatedStandardMaximumIsAjcAndKeepsLegacyChain(int points, string combo)
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records").RespondWithJson(new
        {
            records = new { best = new[] { new { mid = 1, title = "song-1", score = points, level_index = 3, fc = "fullchain" } } }
        });
        var data = await fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher));
        Assert.That(data.Scores[(1, 3)].FullCombo, Is.EqualTo(combo));
        Assert.That(data.Scores[(1, 3)].FullChain, Is.EqualTo("fullchain2"));
    }

    [TestCase(1_010_001)]
    [TestCase(-1)]
    public void DivingFishOutOfRangeScoreIsRejected(int points)
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records").RespondWithJson(new
        {
            records = new { best = new[] { new { mid = 1, title = "song-1", score = points, level_index = 3, fc = "alljustice" } } }
        });
        Assert.ThrowsAsync<InvalidDataException>(() => fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher)));
    }

    [TestCase("fullchain", "fullchain2", "brave")]
    [TestCase("fullchain2", "fullchain", "failed")]
    [TestCase("future-chain", "future-chain", "future-clear")]
    public async Task LxnsCompletionFieldsAreIndependentAndUnknownValuesAreRetained(string rawChain, string expectedChain, string clear)
    {
        LxnsTokenStore.SaveToken(Opponent, "opponent-ticket", "fake-refresh", 3600);
        var fetcher = new LocalLxnsFetcher(Db());
        _http.ForCallsTo("https://maimai.lxns.net/api/v0/user/chunithm/player/scores").RespondWithJson(new
        {
            data = new[] { new { id = 1, song_name = "song-1", level_index = 3, score = 1_010_000, full_combo = "future-combo", full_chain = rawChain, clear } }
        });
        _http.ForCallsTo("https://maimai.lxns.net/api/v0/user/chunithm/player").RespondWithJson(new { data = new { name = "opponent" } });
        var data = await fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher));
        Assert.Multiple(() =>
        {
            Assert.That(data.Scores[(1, 3)].FullCombo, Is.EqualTo("future-combo"), "落雪遵循显式字段，不凭分数升 AJC");
            Assert.That(data.Scores[(1, 3)].FullChain, Is.EqualTo(expectedChain));
            Assert.That(data.Scores[(1, 3)].Clear, Is.EqualTo(clear));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task LxnsDuplicateRecordsKeepBestScoreAndIndependentBestMarks(bool reverse)
    {
        LxnsTokenStore.SaveToken(Opponent, "opponent-ticket", "fake-refresh", 3600);
        var fetcher = new LocalLxnsFetcher(Db());
        var records = new[]
        {
            new { id = 1, song_name = "song-1", level_index = 3, score = 1_009_000, full_combo = "future-combo", full_chain = "future-chain", clear = "future-clear" },
            new { id = 1, song_name = "song-1", level_index = 3, score = 1_000_000, full_combo = "alljusticecritical", full_chain = "fullchain", clear = "catastrophy" }
        };
        _http.ForCallsTo("https://maimai.lxns.net/api/v0/user/chunithm/player/scores").RespondWithJson(new { data = reverse ? records.Reverse().ToArray() : records });
        _http.ForCallsTo("https://maimai.lxns.net/api/v0/user/chunithm/player").RespondWithJson(new { data = new { name = "opponent" } });
        var data = await fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher));
        Assert.Multiple(() =>
        {
            Assert.That(data.Scores[(1, 3)].Achievement, Is.EqualTo(1_009_000));
            Assert.That(data.Scores[(1, 3)].FullCombo, Is.EqualTo("alljusticecritical"));
            Assert.That(data.Scores[(1, 3)].FullChain, Is.EqualTo("fullchain2"));
            Assert.That(data.Scores[(1, 3)].Clear, Is.EqualTo("catastrophy"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task BattleScoresUseNationalLevelsAndConstants(bool lxns)
    {
        var national = ChunithmVersusModelTest.Song(1, names: ["BASIC", "ADVANCED", "EXPERT", "MASTER"]);
        national.Levels[3] = "13+";
        national.Constants[3] = 13.8;
        var local = new SongDb<ChunithmSong>("", "", () => [ChunithmVersusModelTest.Song(1), ChunithmVersusModelTest.Song(2)]);
        DataFetcher fetcher;
        if (lxns)
        {
            LxnsTokenStore.SaveToken(Opponent, "opponent-ticket", "fake-refresh", 3600);
            fetcher = new LocalLxnsFetcher(local, [national]);
            _http.ForCallsTo("https://maimai.lxns.net/api/v0/user/chunithm/player/scores").RespondWithJson(new
            {
                data = new[] { (1, 3), (1, 4), (2, 3) }.Select(x => new
                {
                    id = x.Item1, song_name = $"song-{x.Item1}", level_index = x.Item2, score = 1_009_000, full_combo = "alljustice", full_chain = "fullchain2"
                }).ToArray()
            });
            _http.ForCallsTo("https://maimai.lxns.net/api/v0/user/chunithm/player").RespondWithJson(new { data = new { name = "opponent" } });
        }
        else
        {
            AuthorizeFish(Opponent, "opponent-ticket");
            fetcher = new OAuthFishFetcher(local, [national]);
            _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records").RespondWithJson(new
            {
                records = new
                {
                    best = new[] { (1, 3), (1, 4), (2, 3) }.Select(x => new
                    {
                        mid = x.Item1, title = $"song-{x.Item1}", level_index = x.Item2, score = 1_009_000, fc = "alljustice", full_chain = "fullchain2"
                    }).ToArray()
                }
            });
        }

        var data = await fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher));
        Assert.Multiple(() =>
        {
            Assert.That(data.Scores.Keys, Is.EqualTo(new[] { (1L, 3) }));
            Assert.That(data.Scores[(1, 3)].Level, Is.EqualTo("13+"));
            Assert.That(data.Scores[(1, 3)].Constant, Is.EqualTo(13.8m));
            Assert.That(data.Scores[(1, 3)].Rating, Is.EqualTo(15.95m));
        });
    }

    private static SongDb<ChunithmSong> Db() => new("", "", () => [ChunithmVersusModelTest.Song(1)]);
    private static void AuthorizeFish(long qq, string token) => DivingFishTokenStore.SaveAuthorization(qq, "chunithm", $"synthetic-{qq}",
        new DivingFishToken { AccessToken = token, Scope = "chunithm.records.read", ExpiresAt = DateTime.UtcNow.AddHours(1) });
    private sealed class OAuthFishFetcher(SongDb<ChunithmSong> db, List<ChunithmSong>? songs = null) : DivingFishDataFetcher(db)
    {
        protected override bool OAuthEnabled => true;
        public override List<ChunithmSong> GetSongList() => songs ?? SongDb.SongList;
    }
    private sealed class LocalLxnsFetcher(SongDb<ChunithmSong> db, List<ChunithmSong>? songs = null) : LxnsDataFetcher(db)
    {
        public override List<ChunithmSong> GetSongList() => songs ?? SongDb.SongList;
    }
}
