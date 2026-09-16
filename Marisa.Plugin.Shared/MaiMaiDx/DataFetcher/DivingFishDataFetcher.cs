using System.Net;
using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.DivingFish;
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

public class DivingFishDataFetcher : DataFetcher
{
    public const int OldScoreLimit = 35;
    public const int NewScoreLimit = 15;

    protected virtual bool OAuthEnabled => DivingFishOAuth.IsConfigured;

    public DivingFishDataFetcher(SongDb<MaiMaiSong> songDb) : base(songDb)
    {
    }

    public override async Task<DxRating> GetRating(Message message)
    {
        var (username, qq) = Chunithm.DataFetcher.DataFetcher.AtOrSelf(message, false);
        var isSelf = username.IsWhiteSpace() && qq == message.Sender.Id;

        if (OAuthEnabled)
        {
            try
            {
                var rating = username.IsWhiteSpace()
                    ? ToDxRating(await FetchScoresByQq(qq))
                    : ToDxRating(await FetchScoresByUsername(username));

                return rating;
            }
            catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden)
            {
                if (!isSelf) throw;

                return ToDxRating(await FetchScores(message, false));
            }
        }

        return ToDxRating(await FetchScores(message, false));
    }

    private DxRating ToDxRating(DivingFishDxRatingResponse raw)
    {
        if (raw.PublicOldScores != null && raw.PublicNewScores != null)
        {
            return new DxRating
            {
                Nickname = raw.Nickname,
                OldScores = raw.PublicOldScores
                    .Where(x => x.Id <= 100000)
                    .OrderByDescending(x => x.Rating)
                    .ThenByDescending(x => x.Id)
                    .Take(OldScoreLimit)
                    .ToList(),
                NewScores = raw.PublicNewScores
                    .Where(x => x.Id <= 100000)
                    .OrderByDescending(x => x.Rating)
                    .ThenByDescending(x => x.Id)
                    .Take(NewScoreLimit)
                    .ToList()
            };
        }

        var group = raw.Records
            .Where(x => x.Id <= 100000 && SongDb.SongIndexer.ContainsKey(x.Id))
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
        var (username, _) = Chunithm.DataFetcher.DataFetcher.AtOrSelf(message, false);
        var scores = username.IsWhiteSpace()
            ? await FetchScores(message, true)
            : OAuthEnabled
                ? await FetchScoresByUsername(username)
                : await FetchScores(message, false);

        return scores.Records
            .ToDictionary(x => (x.Id, x.LevelIdx), x => x);
    }

    public override async Task<(string? Nickname, Dictionary<int, SongScore> Scores)> GetSongScore(Message message, MaiMaiSong song)
    {
        var (username, qq) = Chunithm.DataFetcher.DataFetcher.AtOrSelf(message, true);
        var isSelf = username.IsWhiteSpace() && qq == message.Sender.Id;

        var body = new Dictionary<string, object> { ["music_id"] = new[] { song.Id } };
        IFlurlResponse response;

        if (OAuthEnabled)
        {
            var tokenOwner = isSelf ? message.Sender.Id : qq;
            if (!isSelf && await DivingFishTokenStore.GetValidToken(tokenOwner, "maimai") == null)
                throw OAuthSelfOnly();

            response = await SendBearerWithOneRetry(tokenOwner, "maimai", token =>
                "https://www.diving-fish.com/api/maimaidxprober/player/record"
                    .WithHeader("Authorization", $"Bearer {token}")
                    .AllowHttpStatus("400,401,403,429,503")
                    .PostJsonAsync(body));

            if (IsOAuthError(response.StatusCode))
            {
                var errBody = await response.GetStringAsync();
                throw new HttpRequestException(ProberError.DivingFishOAuth(response.StatusCode, errBody),
                    null, (HttpStatusCode)response.StatusCode);
            }
        }
        else
        {
            if (username.IsWhiteSpace()) body["qq"] = qq;
            else body["username"] = username.ToString();

            response = await "https://www.diving-fish.com/api/maimaidxprober/dev/player/record"
                .WithHeader("Developer-Token", ConfigurationManager.Configuration.DivingFish.DevToken)
                .AllowHttpStatus("400,401,403,410")
                .PostJsonAsync(body);

            if (response.StatusCode is 400 or 401 or 403 or 410)
            {
                var errBody = await response.GetStringAsync();
                throw new HttpRequestException(ProberError.DivingFish(response.StatusCode, errBody),
                    null, (HttpStatusCode)response.StatusCode);
            }
        }

        // 单曲接口返回 { "<music_id>": [ 各难度成绩 ] }，只含已游玩难度，且不含昵称
        var byMusic = await response.GetJsonAsync<Dictionary<string, List<SongScore>>>();

        var scores = byMusic.Values
            .SelectMany(x => x)
            .GroupBy(x => x.LevelIdx)
            .ToDictionary(g => g.Key, g => g.First());

        return (null, scores);
    }

    protected virtual async Task<DivingFishDxRatingResponse> FetchScoresByUsername(ReadOnlyMemory<char> username)
    {
        var response = await "https://www.diving-fish.com/api/maimaidxprober/query/player"
            .AllowHttpStatus("400,403")
            .PostJsonAsync(new
            {
                username = username.ToString(),
                b50 = true
            });

        if (response.StatusCode is 400 or 403)
        {
            var body = await response.GetStringAsync();
            throw new HttpRequestException(ProberError.DivingFish(response.StatusCode, body),
                null, (HttpStatusCode)response.StatusCode);
        }

        return ToFullResponse(await response.GetJsonAsync<DivingFishDxPublicResponse>());
    }

    protected virtual async Task<DivingFishDxRatingResponse> FetchScoresByQq(long qq)
    {
        var response = await "https://www.diving-fish.com/api/maimaidxprober/query/player"
            .AllowHttpStatus("400,403")
            .PostJsonAsync(new
            {
                qq,
                b50 = true
            });

        if (response.StatusCode is 400 or 403)
        {
            var body = await response.GetStringAsync();
            throw new HttpRequestException(ProberError.DivingFish(response.StatusCode, body),
                null, (HttpStatusCode)response.StatusCode);
        }

        return ToFullResponse(await response.GetJsonAsync<DivingFishDxPublicResponse>());
    }

    private static DivingFishDxRatingResponse ToFullResponse(DivingFishDxPublicResponse response)
    {
        var records = response.Charts.Sd.Concat(response.Charts.Dx).ToList();
        return new DivingFishDxRatingResponse(
            response.Nickname,
            records,
            response.Charts.Sd,
            response.Charts.Dx);
    }

    protected virtual async Task<DivingFishDxRatingResponse> FetchScores(Message message, bool qqOnly)
    {
        var (username, qq) = Chunithm.DataFetcher.DataFetcher.AtOrSelf(message, qqOnly);

        if (OAuthEnabled)
        {
            var isSelf = username.IsWhiteSpace() && qq == message.Sender.Id;
            var tokenOwner = username.IsWhiteSpace() ? qq : -1;
            if (tokenOwner < 0) throw OAuthSelfOnly();

            var response = await SendBearerWithOneRetry(tokenOwner, "maimai", token =>
                "https://www.diving-fish.com/api/maimaidxprober/player/records"
                    .WithHeader("Authorization", $"Bearer {token}")
                    .AllowHttpStatus("400,401,403,429,503")
                    .GetAsync());

            if (IsOAuthError(response.StatusCode))
            {
                var body = await response.GetStringAsync();
                throw new HttpRequestException(ProberError.DivingFishOAuth(response.StatusCode, body),
                    null, (HttpStatusCode)response.StatusCode);
            }

            return await response.GetJsonAsync<DivingFishDxRatingResponse>();
        }

        var uri = username.IsWhiteSpace()
            ? $"https://www.diving-fish.com/api/maimaidxprober/dev/player/records?qq={qq}"
            : $"https://www.diving-fish.com/api/maimaidxprober/dev/player/records?username={username}";

        var devResponse = await uri
            .WithHeader("Developer-Token", ConfigurationManager.Configuration.DivingFish.DevToken)
            .AllowHttpStatus("400,401,403,410")
            .GetAsync();

        if (devResponse.StatusCode is 400 or 401 or 403 or 410)
        {
            var body = await devResponse.GetStringAsync();
            throw new HttpRequestException(ProberError.DivingFish(devResponse.StatusCode, body),
                null, (HttpStatusCode)devResponse.StatusCode);
        }

        return await devResponse.GetJsonAsync<DivingFishDxRatingResponse>();
    }

    private static async Task<string> GetRequiredToken(long qq, string game)
    {
        var token = (await DivingFishTokenStore.GetValidToken(qq, game))?.AccessToken;
        if (token != null) return token;
        throw new HttpRequestException("未绑定水鱼查分器，请先使用 bind 命令完成绑定后再查询");
    }

    private static async Task<IFlurlResponse> SendBearerWithOneRetry(
        long qq,
        string game,
        Func<string, Task<IFlurlResponse>> send)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await GetRequiredToken(qq, game);

            var response = await send(token);
            if (response.StatusCode != (int)HttpStatusCode.Unauthorized) return response;

            DivingFishTokenStore.RemoveToken(qq, game);
            if (attempt == 1) return response;
        }

        throw new InvalidOperationException("DivingFish OAuth retry loop exited unexpectedly");
    }

    private static bool IsOAuthError(int statusCode) =>
        statusCode is 400 or 401 or 403 or 429 or 503;

    private static HttpRequestException OAuthSelfOnly() =>
        new("水鱼 OAuth 只能读取发送者本人的完整成绩；查询用户名或 @ 他人仅支持公开成绩");

    protected sealed record DivingFishDxRatingResponse(
        string Nickname,
        List<SongScore> Records,
        List<SongScore>? PublicOldScores = null,
        List<SongScore>? PublicNewScores = null);

    private sealed class DivingFishDxPublicResponse
    {
        [JsonProperty("nickname")]
        public string Nickname { get; set; } = "";

        [JsonProperty("charts")]
        public DivingFishDxCharts Charts { get; set; } = new();
    }

    private sealed class DivingFishDxCharts
    {
        [JsonProperty("sd")]
        public List<SongScore> Sd { get; set; } = [];

        [JsonProperty("dx")]
        public List<SongScore> Dx { get; set; } = [];
    }
}
