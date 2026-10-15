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
        ChunithmCnCatalog.Shared.Invalidate();
        _http = new HttpTest();
    }

    [TearDown]
    public void TearDown()
    {
        _http.Dispose();
        ChunithmCnCatalog.Shared.Invalidate();
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

        var data = await FetchBattleData(fetcher, songs);
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
        var error = Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(() => FetchBattleData(fetcher));
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
        Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(() => FetchBattleData(fetcher));
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
        Assert.ThrowsAsync<InvalidDataException>(() => FetchBattleData(fetcher));
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
        Assert.ThrowsAsync<InvalidDataException>(() => FetchBattleData(fetcher));
        Assert.That(_http.CallLog, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task DivingFishExplicitEmptyBestIsValidWithoutRatingOrPublicBestFields()
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records")
            .RespondWith("{\"nickname\":\"opponent\",\"records\":{\"best\":[]}}");
        var data = await FetchBattleData(fetcher);
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
        Assert.ThrowsAsync<InvalidDataException>(() => FetchBattleData(fetcher));
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
        var data = await FetchBattleData(fetcher);
        Assert.That(data.Scores[(1, expectedLevel)].Achievement, Is.Zero);
    }

    [Test]
    public async Task DivingFishConsistentWorldsEndIsExcluded()
    {
        AuthorizeFish(Opponent, "opponent-ticket");
        var fetcher = new OAuthFishFetcher(Db());
        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records")
            .RespondWith("{\"records\":{\"best\":[{\"mid\":1,\"score\":0,\"level_index\":5,\"level_label\":\"WORLD'S END\"}]}}");
        var data = await FetchBattleData(fetcher);
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
        var data = await FetchBattleData(fetcher);
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
        var data = await FetchBattleData(fetcher);
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
        var data = await FetchBattleData(fetcher);
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
        Assert.ThrowsAsync<InvalidDataException>(() => FetchBattleData(fetcher));
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
        Assert.ThrowsAsync<InvalidDataException>(() => FetchBattleData(fetcher));
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
        var data = await FetchBattleData(fetcher);
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
        var data = await FetchBattleData(fetcher);
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
        Assert.ThrowsAsync<InvalidDataException>(() => FetchBattleData(fetcher));
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
        var data = await FetchBattleData(fetcher);
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
        var data = await FetchBattleData(fetcher);
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
    public async Task BattleScoresUseFixedNationalConstantsAndExcludeUnavailableChartsWithoutChangingLocalData(bool lxns)
    {
        var localSong = ChunithmVersusModelTest.Song(1);
        var localJapanOnly = ChunithmVersusModelTest.Song(2);
        var nationalSong = ChunithmVersusModelTest.Song(1, names: ["BASIC", "ADVANCED", "EXPERT", "MASTER"]);
        nationalSong.Levels[3] = "13+";
        nationalSong.Constants[3] = 13.8;
        var nationalNewSong = ChunithmVersusModelTest.Song(3);
        var nationalUnplayedSong = ChunithmVersusModelTest.Song(4);
        var localDb = new SongDb<ChunithmSong>("", "", () => [localSong, localJapanOnly]);
        var fetcher = AuthorizeFetcher(lxns, localDb);
        RespondBattleScores(lxns, [(1, 3, 1_009_000), (1, 4, 1_010_000), (2, 3, 1_009_000), (3, 3, 0)]);

        var data = await FetchBattleData(fetcher, [nationalSong, nationalNewSong, nationalUnplayedSong]);
        Assert.Multiple(() =>
        {
            Assert.That(data.Scores.Keys, Is.EquivalentTo(new[] { (1L, 3), (3L, 3) }));
            Assert.That(data.Scores[(1, 3)].Level, Is.EqualTo("13+"));
            Assert.That(data.Scores[(1, 3)].Constant, Is.EqualTo(13.8m));
            Assert.That(data.Scores[(1, 3)].Rating, Is.EqualTo(15.95m));
            Assert.That(data.Scores[(3, 3)].Achievement, Is.Zero, "真实的 0 分仍是已游玩");
            Assert.That(data.Scores.ContainsKey((4, 3)), Is.False, "国服存在但没成绩的谱面不应伪造记录");
            Assert.That(localSong.Levels[3], Is.EqualTo("14"));
            Assert.That(localSong.Constants[3], Is.EqualTo(14.0));
            Assert.That(ChunithmVersusCatalog.FindChart(localSong, 4), Is.Not.Null);
            Assert.That(_http.CallLog.Any(call => call.Request.Url.Path.EndsWith("song/list")), Is.False,
                "传入的本次目录不能在玩家取数时再次刷新");
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DefaultBattleFetchUsesAnonymousLxnsNationalCatalogInsteadOfLocalJapaneseCatalog(bool lxns)
    {
        var localSong = ChunithmVersusModelTest.Song(1);
        var fetcher = AuthorizeFetcher(lxns, new SongDb<ChunithmSong>("", "", () => [localSong]));
        RespondBattleScores(lxns, [(1, 3, 1_009_000), (1, 4, 1_010_000)]);
        _http.ForCallsTo("https://maimai.lxns.net/api/v0/chunithm/song/list*").RespondWithJson(new
        {
            versions = new[] { new { version = 21000, title = "CHUNITHM LUMINOUS" } },
            songs = new[]
            {
                new
                {
                    id = 1, title = "song-1", artist = "artist", genre = "ORIGINAL", version = 21000, bpm = 200,
                    difficulties = new[] { new { difficulty = 3, level = "13+", level_value = 13.8, note_designer = "charter" } }
                }
            }
        });

        var data = await fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher));
        var catalogCall = _http.CallLog.Single(call => call.Request.Url.Path.EndsWith("song/list"));
        Assert.Multiple(() =>
        {
            Assert.That(data.Scores.Keys, Is.EqualTo(new[] { (1L, 3) }));
            Assert.That(data.Scores[(1, 3)].Constant, Is.EqualTo(13.8m));
            Assert.That(data.Scores[(1, 3)].Level, Is.EqualTo("13+"));
            Assert.That(catalogCall.Request.Headers.Any(header => header.Name == "Authorization"), Is.False);
            Assert.That(localSong.Constants[3], Is.EqualTo(14.0));
            Assert.That(localSong.DiffNames, Does.Contain("ULTIMA"));
        });
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DefaultBattleFetchRejectsMissingAuthorizationBeforeRequestingNationalCatalog(bool lxns)
    {
        DataFetcher fetcher = lxns ? new LocalLxnsFetcher(Db()) : new OAuthFishFetcher(Db());
        Assert.ThrowsAsync<System.Net.Http.HttpRequestException>(() =>
            fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher)));
        Assert.That(_http.CallLog, Is.Empty);
    }

    private DataFetcher AuthorizeFetcher(bool lxns, SongDb<ChunithmSong> db)
    {
        if (lxns)
        {
            LxnsTokenStore.SaveToken(Opponent, "opponent-ticket", "fake-refresh", 3600);
            return new LocalLxnsFetcher(db);
        }

        AuthorizeFish(Opponent, "opponent-ticket");
        return new OAuthFishFetcher(db);
    }

    private void RespondBattleScores(bool lxns, (int Id, int Level, int Score)[] records)
    {
        if (lxns)
        {
            _http.ForCallsTo("https://maimai.lxns.net/api/v0/user/chunithm/player/scores").RespondWithJson(new
            {
                data = records.Select(record => new
                {
                    id = record.Id, song_name = $"song-{record.Id}", level_index = record.Level,
                    score = record.Score, full_combo = "alljustice", full_chain = "fullchain2"
                }).ToArray()
            });
            _http.ForCallsTo("https://maimai.lxns.net/api/v0/user/chunithm/player")
                .RespondWithJson(new { data = new { name = "opponent" } });
            return;
        }

        _http.ForCallsTo("https://www.diving-fish.com/api/chunithmprober/player/records").RespondWithJson(new
        {
            records = new
            {
                best = records.Select(record => new
                {
                    mid = record.Id, title = $"song-{record.Id}", level_index = record.Level,
                    score = record.Score, fc = "alljustice", full_chain = "fullchain2"
                }).ToArray()
            }
        });
    }

    private static Task<ChunithmBattleData> FetchBattleData(DataFetcher fetcher, IReadOnlyList<ChunithmSong>? catalog = null) =>
        fetcher.GetBattleData(new ResolvedPlayer(Opponent, null, false, fetcher), catalog ?? Db().SongList);

    private static SongDb<ChunithmSong> Db() => new("", "", () => [ChunithmVersusModelTest.Song(1)]);
    private static void AuthorizeFish(long qq, string token) => DivingFishTokenStore.SaveAuthorization(qq, "chunithm", $"synthetic-{qq}",
        new DivingFishToken { AccessToken = token, Scope = "chunithm.records.read", ExpiresAt = DateTime.UtcNow.AddHours(1) });
    private sealed class OAuthFishFetcher(SongDb<ChunithmSong> db) : DivingFishDataFetcher(db)
    {
        protected override bool OAuthEnabled => true;
    }
    private sealed class LocalLxnsFetcher(SongDb<ChunithmSong> db) : LxnsDataFetcher(db)
    {
        public override System.Collections.Generic.List<ChunithmSong> GetSongList() => SongDb.SongList;
    }
}
