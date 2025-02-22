using Flurl.Http;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json.Linq;

namespace Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

public class DivingFishDataFetcher : DataFetcher
{
    // TODO 下面的内容以后再来写吧！
    // private readonly Dictionary<int, List<DiffData?>> _diffDict;
    // private readonly List<Rank> _raRankList;

    public DivingFishDataFetcher(SongDb<MaiMaiSong> songDb) : base(songDb)
    {
        // _diffDict   = FetchDiffData().Result;
        // _raRankList = FetchRaRankList().Result.OrderByDescending(x => x.Ra).ToList();
    }

    public override async Task<DxRating> GetRating(Message message)
    {
        var (username, qq) = Chunithm.DataFetcher.DataFetcher.AtOrSelf(message, true);

        var rep = await "https://www.diving-fish.com/api/maimaidxprober/query/player".PostJsonAsync(
            username.IsWhiteSpace()
                ? new { qq, b50       = true }
                : new { username, b50 = true });
        return DxRating.FromJson(await rep.GetStringAsync());
    }

    public override async Task<Dictionary<(long Id, int LevelIdx), SongScore>> GetScores(Message message)
    {
        var (_, qq) = Chunithm.DataFetcher.DataFetcher.AtOrSelf(message, true);

        var response = await "https://www.diving-fish.com/api/maimaidxprober/query/plate".PostJsonAsync(new
        {
            qq, version = MaiMaiSong.Plates
        });

        var res = await response.GetStringAsync();

        return JObject.Parse(res).SelectToken("$.verlist")!
            .Select(x => x.ToObject<SongScore>())
            .ToDictionary(ss => (ss!.Id, ss.LevelIdx))!;
    }

    // public DiffData GetFitDiff(int songId, int levelIdx)
    // {
    //     return _diffDict[songId][levelIdx] ?? throw new KeyNotFoundException("No data found for this song and level.");
    // }
    //
    // public List<int> GetRaRank()
    // {
    //     return _raRankList.Select(x => x.Ra).ToList();
    // }
    //
    // private async Task<List<Rank>> FetchRaRankList()
    // {
    //     var json = await "https://www.diving-fish.com/api/maimaidxprober/rating_ranking".GetStringAsync();
    //
    //     return JArray.Parse(json).Select(x => x.ToObject<Rank>()).ToList()!;
    // }
    //
    // private async Task<Dictionary<int, List<DiffData?>>> FetchDiffData()
    // {
    //     var json = await "https://www.diving-fish.com/api/maimaidxprober/chart_stats".GetStringAsync();
    //
    //     return JObject.Parse(json).SelectToken("$.charts")!.ToObject<Dictionary<int, List<DiffData?>>>()!;
    // }
    //
    // private record Rank([JsonProperty("username")] string Username, [JsonProperty("ra")] int Ra);
    //
    // public record DiffData(
    //     [JsonProperty("cnt")] int PlayCount,
    //     [JsonProperty("fit_diff")] double FitDiff,
    //     [JsonProperty("avg")] double AvgAchievement,
    //     [JsonProperty("avg_dx")] double AvgDxScore,
    //     [JsonProperty("std_dev")] double Std,
    //     [JsonProperty("dist")] int[] RankCount,
    //     [JsonProperty("fc_dist")] int[] FcCount
    // );
}