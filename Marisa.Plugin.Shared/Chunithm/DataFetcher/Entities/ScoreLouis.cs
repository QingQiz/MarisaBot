using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher.Entities;

public record BestScoreLouis
{
#pragma warning disable CS8618
    [JsonProperty("musicId")]
    public int MusicId { get; set; }
    [JsonProperty("levelIndex")]
    public int LevelIndex { get; set; }
    [JsonProperty("score")]
    public int Score { get; set; }
    [JsonProperty("judgeStatus")]
    public string JudgeStatus { get; set; }
    [JsonProperty("chainStatus")]
    public string ChainStatus { get; set; }
    [JsonProperty("lastModified")]
    public long LastModified { get; set; }
#pragma warning restore CS8618

    public ChunithmScore ToChunithmScore(SongDb<ChunithmSong> db)
    {
        var song = db.SongIndexer[MusicId];

        return new ChunithmScore
        {
            Id          = MusicId,
            LevelIndex  = LevelIndex,
            Achievement = Score,
            Constant    = (decimal)song.Constants[LevelIndex],
            // aj则aj，否则优先为fullchain
            Fc         = JudgeStatus.Contains("justice") ? "alljustice" : string.IsNullOrEmpty(ChainStatus) ? JudgeStatus : ChainStatus,
            Level      = song.Levels[LevelIndex],
            LevelLabel = ChunithmSong.LevelLabel[LevelIndex],
            Title      = song.Title
        };
    }
}

public record RecentScoreLouis
{
#pragma warning disable CS8618
    [JsonProperty("musicId")]
    public int MusicId { get; set; }
    // master, expert, advanced, basic...
    [JsonProperty("difficulty")]
    public string Difficulty { get; set; }
    [JsonProperty("score")]
    public int Score { get; set; }
    [JsonProperty("judgeStatus")]
    public string JudgeStatus { get; set; }
    [JsonProperty("chainStatus")]
    public string ChainStatus { get; set; }
#pragma warning restore CS8618

    public ChunithmScore ToChunithmScore(SongDb<ChunithmSong> db)
    {
        var song       = db.SongIndexer[MusicId];
        var levelIndex = ChunithmSong.LevelLabel.IndexOf(Difficulty.ToUpper());

        return new ChunithmScore
        {
            Id          = MusicId,
            LevelIndex  = levelIndex,
            Achievement = Score,
            Constant    = (decimal)song.Constants[levelIndex],
            Fc          = JudgeStatus.Contains("justice") ? "alljustice" : string.IsNullOrEmpty(ChainStatus) ? JudgeStatus : ChainStatus,
            Level       = song.Levels[levelIndex],
            LevelLabel  = ChunithmSong.LevelLabel[levelIndex],
            Title       = song.Title
        };
    }
}