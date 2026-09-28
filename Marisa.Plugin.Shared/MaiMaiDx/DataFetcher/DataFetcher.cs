using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

public abstract class DataFetcher(SongDb<MaiMaiSong> songDb)
{
    protected SongDb<MaiMaiSong> SongDb { get; } = songDb;

    public virtual List<MaiMaiSong> GetSongList()
    {
        return SongDb.SongList;
    }

    /// <summary>
    ///     顶峰成绩（旧 35 + 新 15）。各查分器自己的 B50 接口，支持按账号名查询和 @ 他人；
    ///     关注的是「服务器认定的最好成绩」，与完整成绩是两套数据，不要合并。
    /// </summary>
    public abstract Task<DxRating> GetRating(ResolvedPlayer player);

    /// <summary>
    ///     完整成绩：昵称 + 按 (歌曲 id, 难度序号) 索引的全量成绩。本人/@ 他人用对方自己的凭据读取；
    ///     按账号名查询时只有水鱼可用（公开 B50）。昵称拿不到时为 null。
    /// </summary>
    public abstract Task<(string? Nickname, Dictionary<(long Id, int LevelIdx), SongScore> Scores)>
        GetScores(ResolvedPlayer player);

    /// <summary>
    ///     获取某一首歌各难度的个人成绩（单曲成绩卡用）。返回 (昵称, 按难度索引的成绩)；昵称拿不到时为 null。
    ///     默认实现回退为「拉取整个成绩表再筛选」；具体查分器可覆写为各自的「单曲成绩接口」以避免全量拉取。
    /// </summary>
    public virtual async Task<(string? Nickname, Dictionary<int, SongScore> Scores)> GetSongScore(ResolvedPlayer player, MaiMaiSong song)
    {
        var (nickname, scores) = await GetScores(player);

        return (nickname, scores
            .Where(kv => kv.Key.Id == song.Id)
            .ToDictionary(kv => kv.Key.LevelIdx, kv => kv.Value));
    }

    /// <summary>
    ///     bind 时实测 OAuth 授权是否可用（本地票据能否被服务端接受）。仅支持 OAuth 的查分器覆写。
    /// </summary>
    public virtual Task<bool> TestOAuthToken(long qq) => Task.FromResult(false);
}
