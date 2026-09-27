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
    ///     allowUsername 决定命令文本能否被当作查分器账号名查询。命令自带的参数（如汇总的等级、定数）
    ///     不能当用户名，必须保持默认的 false；只有"参数就是账号名"的命令才传 true。
    /// </summary>
    public abstract Task<DxRating> GetRating(Message message, bool allowUsername = false);

    /// <inheritdoc cref="GetRating"/>
    public abstract Task<Dictionary<(long Id, int LevelIdx), SongScore>> GetScores(Message message, bool allowUsername = false);

    /// <summary>Partial 数据中缺失的谱面不可判为未游玩。</summary>
    public virtual async Task<(string? Nickname, Dictionary<(long Id, int LevelIdx), SongScore> Scores, bool Partial)>
        GetVersusData(Message message, bool publicOnly)
    {
        // vs 的对手支持"用户名"查询，这里显式放行
        var rating = await GetRating(message, true);
        if (publicOnly)
        {
            return (rating.Nickname, rating.OldScores.Concat(rating.NewScores)
                .ToDictionary(x => (x.Id, x.LevelIdx), x => x), true);
        }

        try
        {
            return (rating.Nickname, await GetScores(message, true), false);
        }
        catch (NotSupportedException)
        {
            return (rating.Nickname, rating.OldScores.Concat(rating.NewScores)
                .ToDictionary(x => (x.Id, x.LevelIdx), x => x), true);
        }
    }

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

    /// <summary>
    ///     bind 时实测 OAuth 授权是否可用（本地票据能否被服务端接受）。仅支持 OAuth 的查分器覆写。
    /// </summary>
    public virtual Task<bool> TestOAuthToken(long qq) => Task.FromResult(false);
}
