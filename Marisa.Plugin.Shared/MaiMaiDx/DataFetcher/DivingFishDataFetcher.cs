using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

public class DivingFishDataFetcher : DataFetcher
{
    private const int OldScoreLimit = 35;
    private const int NewScoreLimit = 15;

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
        var raw = await FetchScores(message, false);

        var group = raw.Records
            .Where(x => SongDb.SongIndexer.ContainsKey(x.Id))
            .GroupBy(x => SongDb.SongIndexer[x.Id].Info.IsNew)
            .ToList();

        return new DxRating
        {
            Nickname = raw.Nickname,
            OldScores = group.FirstOrDefault(x => !x.Key)?
                            .OrderByDescending(x => x.Rating)
                            .ThenByDescending(x => x.Id)
                            .Take(OldScoreLimit)
                            .ToList()
                        ?? [],
            NewScores = group.FirstOrDefault(x => x.Key)?
                            .OrderByDescending(x => x.Rating)
                            .ThenByDescending(x => x.Id)
                            .Take(NewScoreLimit)
                            .ToList()
                        ?? []
        };
    }

    public override async Task<Dictionary<(long Id, int LevelIdx), SongScore>> GetScores(Message message)
    {
        var scores = await FetchScores(message, true);

        return scores.Records
            .ToDictionary(x => (x.Id, x.LevelIdx), x => x);
    }

    protected virtual async Task<DivingFishDxRatingResponse> FetchScores(Message message, bool qqOnly)
    {
        var (username, qq) = Chunithm.DataFetcher.DataFetcher.AtOrSelf(message, qqOnly);

        var uri = username.IsWhiteSpace()
            ? $"https://www.diving-fish.com/api/maimaidxprober/dev/player/records?qq={qq}"
            : $"https://www.diving-fish.com/api/maimaidxprober/dev/player/records?username={username}";

        var response = await uri
            .WithHeader("Developer-Token", ConfigurationManager.Configuration.DivingFish.DevToken)
            .AllowHttpStatus("400,403")
            .GetAsync();

        if (response.StatusCode is 400 or 403)
        {
            var rep = await response.GetJsonAsync<DivingFishErrorResponse>();
            var errorMessage = rep.Message ?? rep.Msg ?? "Unknown error";
            throw new HttpRequestException(HttpRequestError.Unknown, $"[DivingFish] {response.StatusCode}: {errorMessage}");
        }

        return await response.GetJsonAsync<DivingFishDxRatingResponse>();
    }

    private sealed record DivingFishErrorResponse(string? Message, string? Msg);

    protected sealed record DivingFishDxRatingResponse(string Nickname, List<SongScore> Records);

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
