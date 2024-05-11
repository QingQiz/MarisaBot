using Marisa.BotDriver.Entity.Message;
using Marisa.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

using MaiSongDb = SongDb<MaiMaiSong, MaiMaiDxGuess>;

public abstract class DataFetcher
{
    protected DataFetcher(MaiSongDb songDb)
    {
        SongDb = songDb;
    }

    protected MaiSongDb SongDb { get; }

    public virtual List<MaiMaiSong> GetSongList()
    {
        return SongDb.SongList;
    }

    public abstract Task<DxRating> GetRating(Message message);

    public abstract Task<Dictionary<(long Id, int LevelIdx), SongScore>> GetScores(Message message);
}