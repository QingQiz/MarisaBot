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
}