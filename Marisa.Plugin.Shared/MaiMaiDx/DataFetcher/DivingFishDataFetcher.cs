using Flurl.Http;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;
using Marisa.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

using MaiSongDb = SongDb<MaiMaiSong, MaiMaiDxGuess>;

public class DivingFishDataFetcher : DataFetcher
{
    private readonly Dictionary<int, List<DiffData?>> _diffDict;
    private readonly List<Rank> _raRankList;

    public DivingFishDataFetcher(MaiSongDb songDb) : base(songDb)
    {
        _diffDict   = FetchDiffData().Result;
        _raRankList = FetchRaRankList().Result.OrderByDescending(x => x.Ra).ToList();
    }

    public override async Task<DxRating> GetRating(Message message)
    {
        var (username, qq) = AtOrSelf(message);

        var rep = await "https://www.diving-fish.com/api/maimaidxprober/query/player".PostJsonAsync(
            string.IsNullOrEmpty(username)
                ? new { qq, b50       = true }
                : new { username, b50 = true });
        return DxRating.FromJson(await rep.GetStringAsync());
    }

    public override async Task<Dictionary<(long Id, int LevelIdx), SongScore>> GetScores(Message message)
    {
        var (_, qq) = AtOrSelf(message, true);

        var response = await "https://www.diving-fish.com/api/maimaidxprober/query/plate".PostJsonAsync(new
        {
            qq, version = MaiMaiSong.Plates
        });

        var res = await response.GetStringAsync();

        return JObject.Parse(res).SelectToken("$.verlist")!
            .Select(x => x.ToObject<SongScore>())
            .ToDictionary(ss => (ss!.Id, ss.LevelIdx))!;
    }

    public DiffData GetFitDiff(int songId, int levelIdx)
    {
        return _diffDict[songId][levelIdx] ?? throw new KeyNotFoundException("No data found for this song and level.");
    }

    public List<int> GetRaRank()
    {
        return _raRankList.Select(x => x.Ra).ToList();
    }

    private async Task<List<Rank>> FetchRaRankList()
    {
        var json = await "https://www.diving-fish.com/api/maimaidxprober/rating_ranking".GetStringAsync();

        return JArray.Parse(json).Select(x => x.ToObject<Rank>()).ToList()!;
    }

    private async Task<Dictionary<int, List<DiffData?>>> FetchDiffData()
    {
        var json = await "https://www.diving-fish.com/api/maimaidxprober/chart_stats".GetStringAsync();

        return JObject.Parse(json).SelectToken("$.charts")!.ToObject<Dictionary<int, List<DiffData?>>>()!;
    }

    private static (string, long) AtOrSelf(Message message, bool qqOnly = false)
    {
        var username = message.Command;
        var qq       = message.Sender.Id;

        if (qqOnly) username = "".AsMemory();

        if (!username.IsWhiteSpace()) return (username.ToString(), qq);

        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);
        if (at != null)
        {
            qq = (at as MessageDataAt)?.Target ?? qq;
        }

        return (username.ToString(), qq);
    }

    private record Rank([JsonProperty("username")] string Username, [JsonProperty("ra")] int Ra);

    public record DiffData(
        [JsonProperty("cnt")] int PlayCount,
        [JsonProperty("fit_diff")] double FitDiff,
        [JsonProperty("avg")] double AvgAchievement,
        [JsonProperty("avg_dx")] double AvgDxScore,
        [JsonProperty("std_dev")] double Std,
        [JsonProperty("dist")] int[] RankCount,
        [JsonProperty("fc_dist")] int[] FcCount
    );
}