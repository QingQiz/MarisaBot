using Marisa.BotDriver.Entity.Message;
using Marisa.EntityFrameworkCore.Entity.Plugin.Chunithm;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

using ChunithmSongDb = SongDb<ChunithmSong, ChunithmGuess>;

public abstract class DataFetcher
{
    protected DataFetcher(ChunithmSongDb songDb)
    {
        SongDb = songDb;
    }

    protected ChunithmSongDb SongDb { get; }

    public virtual List<ChunithmSong> GetSongList()
    {
        return SongDb.SongList;
    }

    public abstract Task<ChunithmRating> GetRating(Message message);

    public abstract Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScores(Message message);
}