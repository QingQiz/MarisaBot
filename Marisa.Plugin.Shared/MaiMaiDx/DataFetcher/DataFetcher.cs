using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

public abstract class DataFetcher(SongDb<MaiMaiSong> songDb)
{
    protected SongDb<MaiMaiSong> SongDb { get; } = songDb;

    public virtual List<MaiMaiSong> GetSongList()
    {
        return SongDb.SongList;
    }

    public abstract Task<DxRating> GetRating(Message message);

    public abstract Task<Dictionary<(long Id, int LevelIdx), SongScore>> GetScores(Message message);

    /// <summary>
    ///     获取某一首歌各难度的个人成绩（单曲成绩卡用）。返回 (昵称, 按难度索引的成绩)；昵称拿不到时为 null。
    ///     默认实现回退为「拉取整个成绩表再筛选」；具体查分器可覆写为各自的「单曲成绩接口」以避免全量拉取。
    /// </summary>
    public virtual async Task<(string? Nickname, Dictionary<int, SongScore> Scores)> GetSongScore(Message message, MaiMaiSong song)
    {
        var rating = await GetRating(message);
        var scores = (await GetScores(message))
            .Where(kv => kv.Key.Id == song.Id)
            .ToDictionary(kv => kv.Key.LevelIdx, kv => kv.Value);
        return (rating.Nickname, scores);
    }
}