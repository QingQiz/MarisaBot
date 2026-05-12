using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Flurl.Http;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Chunithm.DataFetcher;
using Marisa.Plugin.Shared.Util.SongDb;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class ChunithmDivingFishDataFetcherTest
{
    [TearDown]
    public void TearDown()
    {
        new DivingFishDataFetcher(CreateSongDb()).Reset();
    }

    [Test]
    public void GetSongList_Should_Return_Songs()
    {
        var fetcher = new DivingFishDataFetcher(CreateSongDb());
        var songs = fetcher.GetSongList();

        Assert.That(songs, Is.Not.Null);
        Assert.That(songs, Is.Not.Empty);

        var first = songs[0];
        Assert.That(first.Id, Is.GreaterThan(0));
        Assert.That(first.Title, Is.Not.Empty);
        Assert.That(first.Artist, Is.Not.Empty);
        Assert.That(first.Genre, Is.Not.Empty);
        Assert.That(first.Version, Is.Not.Empty);
        Assert.That(first.Version, Does.StartWith("CHUNITHM")); // 版本号格式
        Assert.That(first.Constants, Is.Not.Empty);
        Assert.That(first.Levels, Is.Not.Empty);
        Assert.That(first.DiffNames, Is.Not.Empty);
        Assert.That(first.Charters, Is.Not.Empty);
        Assert.That(first.Constants.Count, Is.EqualTo(first.Levels.Count));
        Assert.That(first.Constants.Count, Is.EqualTo(first.DiffNames.Count));

        // 验证难度级别格式（7, 7+, 11, 13+ 等）
        Assert.That(first.Levels, Has.All.Match(@"^\d+\+?$"));

        // 验证定数范围（1.0 ~ 15.4）
        Assert.That(first.Constants, Has.All.InRange(1.0, 15.4));

        // 验证所有歌曲的 Title 和 Version 都不为空
        Assert.That(songs, Has.All.Matches<ChunithmSong>(s => !string.IsNullOrWhiteSpace(s.Title)));
        Assert.That(songs, Has.All.Matches<ChunithmSong>(s => !string.IsNullOrWhiteSpace(s.Version)));
    }

    [Test]
    public void Parsed_Fields_Should_Match_Raw_Response()
    {
        var raw = "https://maimai.lxns.net/api/v0/chunithm/song/list?notes=true"
            .GetJsonAsync().Result;

        var versionMap = new Dictionary<int, string>();
        foreach (var v in raw.versions)
        {
            versionMap[(int)v.version] = (string)v.title;
        }

        var fetcher = new DivingFishDataFetcher(CreateSongDb());
        var songs = fetcher.GetSongList();

        var rawSongs = ((IEnumerable<dynamic>)raw.songs).ToList();

        // 抽样验证：第1首、第500首、最后1首
        var sampleIndices = new[] { 0, 500, rawSongs.Count - 1 };

        foreach (var i in sampleIndices)
        {
            if (i >= rawSongs.Count) continue;

            var rawSong = rawSongs[i];
            var parsed = songs.FirstOrDefault(s => s.Id == (long)rawSong.id);

            if (parsed == null) continue; // 已删除歌曲会被过滤

            // Title
            Assert.That(parsed.Title, Is.EqualTo((string)rawSong.title));

            // Artist
            Assert.That(parsed.Artist, Is.EqualTo((string)rawSong.artist));

            // Genre
            Assert.That(parsed.Genre, Is.EqualTo((string)rawSong.genre));

            // Version (mapped from version ID)
            var expectedVersion = versionMap.GetValueOrDefault((int)rawSong.version, "");
            Assert.That(parsed.Version, Is.EqualTo(expectedVersion));

            // Difficulties: each difficulty in raw has level + level_value + note_designer
            if (rawSong.difficulties == null) continue;

            var rawDiffs = ((IEnumerable<dynamic>)rawSong.difficulties)
                .OrderBy(d => (int)d.difficulty).ToList();

            Assert.That(rawDiffs, Is.Not.Empty);
            Assert.That(parsed.Constants.Count, Is.EqualTo(rawDiffs.Count));
            Assert.That(parsed.Levels.Count, Is.EqualTo(rawDiffs.Count));
            Assert.That(parsed.Charters.Count, Is.EqualTo(rawDiffs.Count));

            for (var j = 0; j < rawDiffs.Count; j++)
            {
                Assert.That(parsed.Constants[j], Is.EqualTo((double)rawDiffs[j].level_value));
                Assert.That(parsed.Levels[j], Is.EqualTo((string)rawDiffs[j].level));
                Assert.That(parsed.Charters[j], Is.EqualTo((string)rawDiffs[j].note_designer));
            }
        }
    }

    [Test]
    public void GetSongList_Should_Return_Consistent_Results()
    {
        var db = CreateSongDb();
        var fetcher1 = new DivingFishDataFetcher(db);
        var songs1 = fetcher1.GetSongList();

        var fetcher2 = new DivingFishDataFetcher(db);
        var songs2 = fetcher2.GetSongList();

        Assert.That(songs1.Count, Is.EqualTo(songs2.Count));
        Assert.That(songs1[0].Id, Is.EqualTo(songs2[0].Id));
    }

    [Test]
    public async Task GetRating_And_GetScores_Should_Skip_Unmatched_Songs()
    {
        var songDb = CreateSongDb();
        var fetcher = new TestDivingFishDataFetcher(songDb, new ChunithmRating
        {
            Username = "tester",
            Records = new Records
            {
                Best =
                [
                    CreateScore(999, "known-song", 0, 1_009_000, 14.9m),
                    CreateScore(998, "missing-best", 0, 1_000_000, 14.0m)
                ],
                Recent =
                [
                    CreateScore(997, "missing-recent", 0, 1_000_000, 14.0m)
                ]
            }
        });
        var message = new Message(null!, [])
        {
            Sender = new SenderInfo(1, "test")
        };

        var rating = await fetcher.GetRating(message);
        var scores = await fetcher.GetScores(message);

        Assert.Multiple(() =>
        {
            Assert.That(rating.Records.Best.Select(x => x.Id), Is.EqualTo(new[] { 1L }));
            Assert.That(rating.Records.Recent, Is.Empty);
            Assert.That(scores.Keys, Is.EqualTo(new[] { (1L, 0) }));
        });
    }

    private static SongDb<ChunithmSong> CreateSongDb()
    {
        return new SongDb<ChunithmSong>("", "", () => [CreateSong(1, "known-song")]);
    }

    private static ChunithmSong CreateSong(long id, string title)
    {
        dynamic song = new ExpandoObject();
        song.Id = id;
        song.Title = title;
        song.Artist = "artist";
        song.Genre = "genre";
        song.Version = "CHUNITHM LUMINOUS";

        dynamic beatmap = new ExpandoObject();
        beatmap.Constant = 14.9;
        beatmap.Charter = "-";
        beatmap.LevelStr = "14+";
        beatmap.LevelName = "MASTER";
        beatmap.ChartName = "";
        beatmap.Bpm = "200";
        beatmap.MaxCombo = 1000;

        song.Beatmaps = new[] { beatmap };
        return new ChunithmSong(song);
    }

    private static ChunithmScore CreateScore(long id, string title, int levelIndex, int achievement, decimal constant)
    {
        return new ChunithmScore
        {
            Id = id,
            Title = title,
            LevelIndex = levelIndex,
            Achievement = achievement,
            Constant = constant,
            Level = "14+",
            LevelLabel = "MASTER",
            Fc = string.Empty
        };
    }

    private sealed class TestDivingFishDataFetcher(SongDb<ChunithmSong> songDb, ChunithmRating rating) : DivingFishDataFetcher(songDb)
    {
        public override List<ChunithmSong> GetSongList()
        {
            return SongDb.SongList;
        }

        protected override Task<ChunithmRating> FetchScores(Message message, bool qqOnly)
        {
            return Task.FromResult(new ChunithmRating
            {
                Username = rating.Username,
                Records = new Records
                {
                    Best = rating.Records.Best.Select(CloneScore).ToArray(),
                    Recent = rating.Records.Recent.Select(CloneScore).ToArray()
                }
            });
        }

        private static ChunithmScore CloneScore(ChunithmScore score)
        {
            return new ChunithmScore
            {
                Id = score.Id,
                Title = score.Title,
                LevelIndex = score.LevelIndex,
                Achievement = score.Achievement,
                Constant = score.Constant,
                Level = score.Level,
                LevelLabel = score.LevelLabel,
                Fc = score.Fc
            };
        }
    }
}
