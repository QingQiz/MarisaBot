using System.Net;
using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.DivingFish;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

public class DivingFishDataFetcher(SongDb<MaiMaiSong> songDb) : DataFetcher(songDb)
{
    public const int OldScoreLimit = 35;
    public const int NewScoreLimit = 15;

    /// <summary>没有可用 OAuth 授权时的提示。vs 的本地预检也用这句，保证预检与取数失败时口径一致。</summary>
    public const string NotBoundHint = "未绑定水鱼查分器，请先使用 bind 命令完成绑定后再查询";

    protected virtual bool OAuthEnabled => DivingFishOAuth.IsConfigured;

    public override async Task<DxRating> GetRating(ResolvedPlayer player)
    {
        if (OAuthEnabled)
        {
            try
            {
                return player.Username is { } username
                    ? ToDxRating(await FetchScoresByUsername(username.AsMemory()))
                    : ToDxRating(await FetchScoresByQq(player.Qq));
            }
            catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden)
            {
                // 公开 B50 拿不到本人（未公开等）时，改用完整成绩在本地算 35+15
                if (!player.IsSelf) throw;

                return ToDxRating(await FetchRecords(player));
            }
        }

        return ToDxRating(await FetchRecords(player));
    }

    private DxRating ToDxRating(DivingFishDxRatingResponse raw)
    {
        if (raw is { PublicOldScores: not null, PublicNewScores: not null })
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

    public override async Task<(string? Nickname, Dictionary<(long Id, int LevelIdx), SongScore> Scores)>
        GetScores(ResolvedPlayer player)
    {
        // 按账号名查询：OAuth 模式只有公开 B50 可用，dev token 模式由水鱼按账号名返回完整记录
        var response = player.Username is { } username && OAuthEnabled
            ? await FetchScoresByUsername(username.AsMemory())
            : await FetchRecords(player);

        return (response.Nickname, response.Records.ToDictionary(x => (x.Id, x.LevelIdx), x => x));
    }

    /// <summary>完整成绩记录：OAuth 票据按 QQ 读（不看 B50 是否公开）；未配置 OAuth 时用 dev token。</summary>
    private async Task<DivingFishDxRatingResponse> FetchRecords(ResolvedPlayer player) =>
        player.Username is { } username ? await FetchScores(username.AsMemory()) : await FetchScores(player.Qq);

    public override async Task<(string? Nickname, Dictionary<int, SongScore> Scores)> GetSongScore(ResolvedPlayer player, MaiMaiSong song)
    {
        var body = new Dictionary<string, object> { ["music_id"] = new[] { song.Id } };
        IFlurlResponse response;

        if (OAuthEnabled)
        {
            if (!player.IsSelf && await DivingFishTokenStore.GetValidToken(player.Qq, "maimai") == null)
                throw OAuthSelfOnly();

            response = await SendBearerWithOneRetry(player.Qq, "maimai", token =>
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
            if (player.Username is { } username) body["username"] = username;
            else body["qq"] = player.Qq;

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

    /// <summary>按 QQ 取完整成绩：OAuth 票据优先；未配置 OAuth 时退回 dev token（要求该 QQ 已注册水鱼且公开）。</summary>
    protected virtual async Task<DivingFishDxRatingResponse> FetchScores(long qq)
    {
        if (!OAuthEnabled) return await FetchScoresWithDevToken($"qq={qq}");

        var response = await SendBearerWithOneRetry(qq, "maimai", token =>
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

    /// <summary>按水鱼账号名取完整成绩：OAuth 票据只能读本人，所以只可能是 dev token 模式。</summary>
    protected virtual async Task<DivingFishDxRatingResponse> FetchScores(ReadOnlyMemory<char> username)
    {
        if (OAuthEnabled) throw OAuthSelfOnly();

        return await FetchScoresWithDevToken($"username={username}");
    }

    private static async Task<DivingFishDxRatingResponse> FetchScoresWithDevToken(string query)
    {
        var response = await $"https://www.diving-fish.com/api/maimaidxprober/dev/player/records?{query}"
            .WithHeader("Developer-Token", ConfigurationManager.Configuration.DivingFish.DevToken)
            .AllowHttpStatus("400,401,403,410")
            .GetAsync();

        if (response.StatusCode is 400 or 401 or 403 or 410)
        {
            var body = await response.GetStringAsync();
            throw new HttpRequestException(ProberError.DivingFish(response.StatusCode, body),
                null, (HttpStatusCode)response.StatusCode);
        }

        return await response.GetJsonAsync<DivingFishDxRatingResponse>();
    }

    private static async Task<string> GetRequiredToken(long qq, string game)
    {
        var token = (await DivingFishTokenStore.GetValidToken(qq, game))?.AccessToken;
        if (token != null) return token;
        throw new HttpRequestException(NotBoundHint);
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

    /// <summary>
    ///     bind 时实测票据是否可读：本地存有票据不代表服务端仍接受它（可能已被撤销），
    ///     只有成绩接口返回 2xx 才算授权有效；401 时丢票重拉一次。
    /// </summary>
    public override async Task<bool> TestOAuthToken(long qq)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = (await DivingFishTokenStore.GetValidToken(qq, "maimai"))?.AccessToken;
            if (token == null) return false;

            var response = await "https://www.diving-fish.com/api/maimaidxprober/player/record"
                .WithHeader("Authorization", $"Bearer {token}")
                .AllowHttpStatus("400,401,403,429,503")
                .PostJsonAsync(new Dictionary<string, object> { ["music_id"] = new[] { SongDb.SongList[0].Id } });

            if (response.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                DivingFishTokenStore.RemoveToken(qq, "maimai");
                continue;
            }

            if (response.StatusCode is 400 or 403) return false;
            if (response.StatusCode is 429 or 503) throw new HttpRequestException("水鱼服务器繁忙，请稍后再试");
            return true;
        }

        return false;
    }

    private static bool IsOAuthError(int statusCode) =>
        statusCode is 400 or 401 or 403 or 429 or 503;

    private static HttpRequestException OAuthSelfOnly() =>
        new("水鱼 OAuth 只能读取本人授权的成绩；@ 他人需要对方先完成绑定，按账号名查询只能读公开 B50");

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
