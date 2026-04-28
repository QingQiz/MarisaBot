using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Osu;
using Marisa.Plugin.Shared.Osu.Entity.Score;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class OsuTest
{
        private const string MinimalOsuScorePayload = """
                [
                    {
                        "accuracy": 0.9875,
                        "best_id": 12345,
                        "created_at": "2026-04-28T00:00:00+00:00",
                        "id": 67890,
                        "max_combo": 1000,
                        "mode": "mania",
                        "mode_int": 3,
                        "mods": ["HD"],
                        "passed": true,
                        "perfect": false,
                        "pp": 456.78,
                        "rank": "A",
                        "replay": false,
                        "statistics": {
                            "count_100": 1,
                            "count_300": 2,
                            "count_50": 3,
                            "count_geki": 4,
                            "count_katu": 5,
                            "count_miss": 6
                        },
                        "user_id": 13579,
                        "current_user_attributes": {
                            "pin": null
                        },
                        "beatmap": {
                            "beatmapset_id": 24680,
                            "difficulty_rating": 5.12,
                            "id": 112233,
                            "mode": "mania",
                            "status": "ranked",
                            "total_length": 120,
                            "user_id": 9988,
                            "version": "Insane",
                            "accuracy": 8,
                            "ar": 9,
                            "bpm": 180,
                            "convert": false,
                            "count_circles": 0,
                            "count_sliders": 0,
                            "count_spinners": 0,
                            "cs": 4,
                            "deleted_at": null,
                            "drain": 7,
                            "hit_length": 100,
                            "is_scoreable": true,
                            "last_updated": "2026-04-28T00:00:00+00:00",
                            "mode_int": 3,
                            "passcount": 100,
                            "playcount": 1000,
                            "ranked": 1,
                            "url": "https://osu.ppy.sh/beatmaps/112233",
                            "checksum": "checksum"
                        },
                        "beatmapset": {
                            "artist": "artist",
                            "artist_unicode": "artist",
                            "covers": {
                                "cover": "https://example.com/cover.png",
                                "cover@2x": "https://example.com/cover@2x.png",
                                "card": "https://example.com/card.png",
                                "card@2x": "https://example.com/card@2x.png",
                                "list": "https://example.com/list.png",
                                "list@2x": "https://example.com/list@2x.png",
                                "slimcover": "https://example.com/slimcover.png",
                                "slimcover@2x": "https://example.com/slimcover@2x.png"
                            },
                            "creator": "mapper",
                            "favourite_count": 10,
                            "hype": null,
                            "id": 24680,
                            "nsfw": false,
                            "offset": 0,
                            "play_count": 1234,
                            "preview_url": "https://example.com/preview.mp3",
                            "source": "source",
                            "spotlight": false,
                            "status": "ranked",
                            "title": "title",
                            "title_unicode": "title",
                            "track_id": null,
                            "user_id": 9988,
                            "video": false
                        },
                        "user": {
                            "avatar_url": "https://example.com/avatar.png",
                            "country_code": "CN",
                            "default_group": "default",
                            "id": 13579,
                            "is_active": true,
                            "is_bot": false,
                            "is_deleted": false,
                            "is_online": false,
                            "is_supporter": false,
                            "last_visit": null,
                            "pm_friends_only": false,
                            "profile_colour": null,
                            "username": "test-user"
                        }
                    }
                ]
                """;

    [SetUp]
    public void Setup()
    {
        var configPath = Path.Join(Directory.GetParent(Environment.CurrentDirectory)!.Parent!.Parent!.Parent!.ToString(), "Marisa.StartUp", "config.yaml");
        ConfigurationManager.SetConfigFilePath(configPath);
    }

    [Test]
    [TestCase(1794551, "1794551 25-ji, Night Code de x Hatsune Miku - Jishou Mushoku.osz")]
    public async Task Beatmap_Should_Be_Downloaded(long beatmapId, string filename)
    {
        if (File.Exists(filename))
        {
            File.Delete(filename);
        }

        Assert.IsFalse(File.Exists(filename));

        await OsuApi.DownloadBeatmap(beatmapId, Environment.CurrentDirectory);

        Assert.IsTrue(File.Exists(filename));
    }

    [Test]
    [TestCase(1931476, 4001513)]
    public void Beatmap_Should_Be_Got_By_Id(long beatmapsetId, long beatmapId)
    {
        Assert.DoesNotThrow(() => OsuApi.GetBeatmapPathByBeatmapId(beatmapsetId, beatmapId));
    }

    [Test]
    [TestCase(4001513)]
    public async Task BeatmapCover_Should_Be_Got(long beatmapId)
    {
        var scores = await OsuApi.GetScores(16265882, OsuApi.OsuScoreType.Best, OsuApi.GetModeName(3), 0, 1);
        var score  = scores.First(x => x.Beatmap.Id == beatmapId);
        var cover  = score.Beatmap.TryGetCover();
        Assert.NotNull(cover);
        Assert.IsTrue(File.Exists(cover));
    }

    [Test]
    public void Score_Should_Fall_Back_To_Legacy_Total_Score_When_Score_Field_Is_Missing()
    {
        var payload = MinimalOsuScorePayload.Replace("\"rank\": \"A\",", "\"rank\": \"A\",\n            \"legacy_total_score\": 1234567,");

        var score = OsuScore.FromJson(payload)!.Single();

        Assert.That(score.Score, Is.EqualTo(1234567));
        Assert.That(score.ToJson(), Does.Contain("\"score\":1234567"));
    }

    [Test]
    public void Score_Should_Fall_Back_To_Classic_Total_Score_For_Lazer_Scores()
    {
        var payload = MinimalOsuScorePayload.Replace("\"rank\": \"A\",", "\"rank\": \"A\",\n            \"classic_total_score\": 7654321,\n            \"legacy_total_score\": 0,");

        var score = OsuScore.FromJson(payload)!.Single();

        Assert.That(score.Score, Is.EqualTo(7654321));
    }

    [Test]
    public void Score_Should_Support_New_Osu_Api_Mod_And_Mode_Fields()
    {
        var payload = MinimalOsuScorePayload
            .Replace("\"mode\": \"mania\",\n            \"mode_int\": 3,", "\"mode\": \"mania\",\n            \"ruleset_id\": 3,")
            .Replace("\"mods\": [\"HD\"],", "\"mods\": [{\"acronym\":\"HD\"}, {\"acronym\":\"DT\", \"settings\": {\"speed_change\": 1.5}}],")
            .Replace("\"perfect\": false,", "\"legacy_perfect\": true,")
            .Replace("\"replay\": false,", "\"has_replay\": true,");

        var score = OsuScore.FromJson(payload)!.Single();

        Assert.Multiple(() =>
        {
            Assert.That(score.ModeInt, Is.EqualTo(3));
            Assert.That(score.Mods, Is.EqualTo(new[] { "HD", "DT" }));
            Assert.That(score.Perfect, Is.True);
            Assert.That(score.Replay, Is.True);
        });
    }

    [Test]
    public void Score_Should_Support_New_Osu_Api_Statistics_Fields()
    {
        var payload = MinimalOsuScorePayload
            .Replace("\"count_100\": 1,", "\"ok\": 101,")
            .Replace("\"count_300\": 2,", "\"great\": 302,")
            .Replace("\"count_50\": 3,", "\"meh\": 53,")
            .Replace("\"count_geki\": 4,", "\"perfect\": 404,")
            .Replace("\"count_katu\": 5,", "\"good\": 205,")
            .Replace("\"count_miss\": 6", "\"miss\": 16");

        var score = OsuScore.FromJson(payload)!.Single();

        Assert.Multiple(() =>
        {
            Assert.That(score.Statistics.Count100, Is.EqualTo(101));
            Assert.That(score.Statistics.Count300, Is.EqualTo(302));
            Assert.That(score.Statistics.Count50, Is.EqualTo(53));
            Assert.That(score.Statistics.Count300P, Is.EqualTo(404));
            Assert.That(score.Statistics.Count200, Is.EqualTo(205));
            Assert.That(score.Statistics.CountMiss, Is.EqualTo(16));
        });
    }

    [Test]
    public async Task OsuApi_Should_Fetch_Qingqiz_Best_Score_When_Client_Credentials_Configured()
    {
        try
        {
            _ = ConfigurationManager.Configuration.Osu.ClientId;
            _ = ConfigurationManager.Configuration.Osu.ClientSecret;
        }
        catch (MissingConfigurationException)
        {
            Assert.Ignore("osu.clientId or osu.clientSecret is not configured.");
        }

        var user = await OsuApi.GetUserInfoByName("qingqiz", 3);
        var scores = await OsuApi.GetScores(user.Id, OsuApi.OsuScoreType.Best, OsuApi.GetModeName(3), 0, 1);

        Assert.Multiple(() =>
        {
            Assert.That(user.Username, Is.EqualTo("qingqiz").IgnoreCase);
            Assert.That(scores, Is.Not.Null.And.Not.Empty);
            Assert.That(scores[0].Score, Is.GreaterThan(0));
            Assert.That(scores[0].Beatmap, Is.Not.Null);
            Assert.That(scores[0].Beatmapset, Is.Not.Null);
            Assert.That(scores[0].Mods, Is.Not.Null);
        });
    }
    //
    // [Test]
    // public async Task T()
    // {
    //     var result = await OsuApi.Request("https://osu.ppy.sh/api/v2/users/16265882/scores/best?include_fails=0&mode=mania&limit=1&offset=0").GetStringAsync();
    //     
    //     await File.WriteAllTextAsync(@"C:\Users\sofee\Desktop\score.json", result);
    //
    //     result = await OsuApi.Request("https://osu.ppy.sh/api/v2/beatmaps/4001513").GetStringAsync();
    //     await File.WriteAllTextAsync(@"C:\Users\sofee\Desktop\beatmap.json", result);
    // }
}
