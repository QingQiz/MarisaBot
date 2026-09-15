using Flurl.Http;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

public abstract class DataFetcher(SongDb<ChunithmSong> songDb)
{
    /// <summary>
    ///     中二节奏有一些如删的歌曲，即这些歌在游戏中已经删除，但在公众号中依然被保留，
    ///     这导致了op计算和rating计算不正确，
    ///     因此需要手动过滤掉
    /// </summary>
    protected static readonly HashSet<long> DeletedSongs =
    [
        156, 343, 1046, 1049, 1050, 1051, 1054, 2007, 2008, 2014, 2016, 2020, 2021,
        2027, 2039, 2075, 2076, 2095, 2141, 2211, 2212, 2213, 921
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

    /// <summary>
    ///     从水鱼 latest_version 接口获取当前"新版本"集合（New Best 20 依据），
    ///     供各查分器统一使用，避免版本更新后硬编码失效。
    /// </summary>
    protected static async Task<HashSet<string>> FetchLatestVersions()
    {
        var response = await "https://www.diving-fish.com/api/chunithmprober/latest_version"
            .GetJsonAsync<LatestVersionResponse>();

        var newest = response.Versions
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Select(version => version.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (newest.Count == 0) throw new InvalidDataException("水鱼 latest_version 返回了空版本列表");

        return newest;
    }

    private sealed class LatestVersionResponse
    {
        [JsonProperty("version", Required = Required.Always)]
        public string[] Versions { get; set; } = [];
    }
}