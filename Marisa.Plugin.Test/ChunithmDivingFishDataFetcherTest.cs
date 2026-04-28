using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageSender;
using Marisa.Plugin.Shared.Chunithm;
using Marisa.Plugin.Shared.Chunithm.DataFetcher;
using Marisa.Plugin.Shared.Util.SongDb;
using NUnit.Framework;

namespace Marisa.Plugin.Test;

public class ChunithmDivingFishDataFetcherTest
{
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
        beatmap.Constant = 14.9m;
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
