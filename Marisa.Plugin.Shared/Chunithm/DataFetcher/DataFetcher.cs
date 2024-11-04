using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

public abstract class DataFetcher(SongDb<ChunithmSong> songDb)
{
    /// <summary>
    ///     中二节奏有一些如删的歌曲，即这些歌在游戏中已经删除，但在公众号中依然被保留，
    ///     这导致了op计算和rating计算不正确，
    ///     因此需要手动过滤掉
    /// </summary>
    protected readonly HashSet<long> DeletedSongs =
    [
        156, 343, 1046, 1049, 1050, 1051, 1054, 2007, 2008, 2014, 2016, 2020, 2021,
        2027, 2039, 2075, 2076, 2095, 2141, 2166, 2169, 2173, 2174, 2177, 2211, 2212, 2213
    ];

    protected SongDb<ChunithmSong> SongDb { get; } = songDb;

    public virtual List<ChunithmSong> GetSongList()
    {
        return SongDb.SongList;
    }

    public abstract Task<ChunithmRating> GetRating(Message message);

    public abstract Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScores(Message message);

    public static (ReadOnlyMemory<char>, long) AtOrSelf(Message message, bool qqOnly = false)
    {
        var username = "".AsMemory();
        var qq       = message.Sender.Id;

        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);
        if (at != null)
        {
            qq = (at as MessageDataAt)?.Target ?? qq;
            return (username, qq);
        }

        if (!qqOnly) username = message.Command;

        return (username, qq);
    }
}