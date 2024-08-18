using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

public abstract class DataFetcher(SongDb<ChunithmSong> songDb)
{
    protected SongDb<ChunithmSong> SongDb { get; } = songDb;

    public virtual List<ChunithmSong> GetSongList()
    {
        return SongDb.SongList;
    }

    public abstract Task<ChunithmRating> GetRating(Message message);

    public abstract Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScores(Message message);
}