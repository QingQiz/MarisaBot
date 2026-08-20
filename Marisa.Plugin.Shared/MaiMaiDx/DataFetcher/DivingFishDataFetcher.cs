using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.DivingFish;
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

public class DivingFishDataFetcher : DataFetcher
{
    public const int OldScoreLimit = 35;
    public const int NewScoreLimit = 15;

    public DivingFishDataFetcher(SongDb<MaiMaiSong> songDb) : base(songDb)
    {
    }

    public override async Task<DxRating> GetRating(Message message)
    {
        var (username, qq) = Chunithm.DataFetcher.DataFetcher.AtOrSelf(message, false);

        // OAuth 模式：指定了 username 时走公开 /query/player（b50，无需验证）
        if (DivingFishOAuth.IsConfigured && !username.IsWhiteSpace())
        {
            var raw = await FetchScoresByUsername(username);

            return new DxRating
            {
                Nickname = raw.Nickname,
                OldScores = raw.Records
                    .Where(x => x.Id <= 100000 && SongDb.SongIndexer.ContainsKey(x.Id))
                    .Where(x => !SongDb.SongIndexer[x.Id].Info.IsNew)
                    .OrderByDescending(x => x.Rating)
                    .ThenByDescending(x => x.Id)
                    .Take(OldScoreLimit)
                    .ToList(),
                NewScores = raw.Records
                    .Where(x => x.Id <= 100000 && SongDb.SongIndexer.ContainsKey(x.Id))
                    .Where(x => SongDb.SongIndexer[x.Id].Info.IsNew)
                    .OrderByDescending(x => x.Rating)
                    .ThenByDescending(x => x.Id)
                    .Take(NewScoreLimit)
                    .ToList()
            };
        }

        var raw2 = await FetchScores(message, false);

        var group = raw2.Records
            .Where(x => x.Id <= 100000 && SongDb.SongIndexer.ContainsKey(x.Id))
            .GroupBy(x => SongDb.SongIndexer[x.Id].Info.IsNew)
            .ToList();

        return new DxRating
        {
            Nickname = raw2.Nickname,
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

    public override async Task<(string? Nickname, Dictionary<int, SongScore> Scores)> GetSongScore(Message message, MaiMaiSong song)
    {
        // OAuth 模式：查询对象由 token 决定，body 只带 music_id；DevToken 模式：附带 qq/username
        var (username, qq) = Chunithm.DataFetcher.DataFetcher.AtOrSelf(message, false);

        var body = new Dictionary<string, object> { ["music_id"] = new[] { song.Id } };
        if (!DivingFishOAuth.IsConfigured && username.IsWhiteSpace()) body["qq"] = qq;
        else if (!DivingFishOAuth.IsConfigured) body["username"] = username;

        var req = "https://www.diving-fish.com/api/maimaidxprober/player/record"
            .AllowHttpStatus("400,401,403,429");

        if (DivingFishOAuth.IsConfigured)
        {
            var token = await GetTokenOrReply(message, qq, "maimai");
            if (token == null) return (null, new Dictionary<int, SongScore>());
            req = req.WithHeader("Authorization", $"Bearer {token}");
        }
        else
        {
            req = req.WithHeader("Developer-Token", ConfigurationManager.Configuration.DivingFish.DevToken);
        }

        var response = await req.PostJsonAsync(body);

        if (response.StatusCode is 400 or 401 or 403 or 429)
        {
            if (response.StatusCode == 401 && DivingFishOAuth.IsConfigured)
            {
                DivingFishTokenStore.RemoveToken(qq, "maimai");
            }
            var errBody = await response.GetStringAsync();
            throw new HttpRequestException(HttpRequestError.Unknown, ProberError.DivingFish(response.StatusCode, errBody));
        }

        // 单曲接口返回 { "<music_id>": [ 各难度成绩 ] }，只含已游玩难度，且不含昵称
        var byMusic = await response.GetJsonAsync<Dictionary<string, List<SongScore>>>();

        var scores = byMusic.Values
            .SelectMany(x => x)
            .GroupBy(x => x.LevelIdx)
            .ToDictionary(g => g.Key, g => g.First());

        return (null, scores);
    }

    /// <summary>
    ///     公开端点 /query/player：按用户名查 b50（无需验证，用户隐私决定可否查询）
    /// </summary>
    protected virtual async Task<DivingFishDxRatingResponse> FetchScoresByUsername(ReadOnlyMemory<char> username)
    {
        var response = await "https://www.diving-fish.com/api/maimaidxprober/query/player"
            .AllowHttpStatus("400,403")
            .PostJsonAsync(new
            {
                username = username.ToString(),
                b50 = "1"
            });

        if (response.StatusCode is 400 or 403)
        {
            var body = await response.GetStringAsync();
            throw new HttpRequestException(HttpRequestError.Unknown, ProberError.DivingFish(response.StatusCode, body));
        }

        return await response.GetJsonAsync<DivingFishDxRatingResponse>();
    }

    protected virtual async Task<DivingFishDxRatingResponse> FetchScores(Message message, bool qqOnly)
    {
        var (username, qq) = Chunithm.DataFetcher.DataFetcher.AtOrSelf(message, qqOnly);

        // OAuth 模式：查询对象由 token 决定，URL 不带 qq/username
        if (DivingFishOAuth.IsConfigured)
        {
            var token = await GetTokenOrReply(message, qq, "maimai");
            if (token == null) return new DivingFishDxRatingResponse("", []);

            var response = await "https://www.diving-fish.com/api/maimaidxprober/player/records"
                .WithHeader("Authorization", $"Bearer {token}")
                .AllowHttpStatus("400,401,403,429")
                .GetAsync();

            if (response.StatusCode is 400 or 401 or 403 or 429)
            {
                if (response.StatusCode == 401) DivingFishTokenStore.RemoveToken(qq, "maimai");
                var body = await response.GetStringAsync();
                throw new HttpRequestException(HttpRequestError.Unknown, ProberError.DivingFish(response.StatusCode, body));
            }

            return await response.GetJsonAsync<DivingFishDxRatingResponse>();
        }

        // DevToken 模式（废弃端点，过渡期兼容）
        var uri = username.IsWhiteSpace()
            ? $"https://www.diving-fish.com/api/maimaidxprober/dev/player/records?qq={qq}"
            : $"https://www.diving-fish.com/api/maimaidxprober/dev/player/records?username={username}";

        var devResponse = await uri
            .WithHeader("Developer-Token", ConfigurationManager.Configuration.DivingFish.DevToken)
            .AllowHttpStatus("400,401,403")
            .GetAsync();

        if (devResponse.StatusCode is 400 or 401 or 403)
        {
            var body = await devResponse.GetStringAsync();
            throw new HttpRequestException(HttpRequestError.Unknown, ProberError.DivingFish(devResponse.StatusCode, body));
        }

        return await devResponse.GetJsonAsync<DivingFishDxRatingResponse>();
    }

    /// <summary>
    ///     获取 OAuth token：
    ///     已绑定 → 换票成功返回 token；
    ///     未绑定 → 回复绑定链接并返回 null；
    ///     换票失败（限流/网络/凭据错误）→ 回复错误并返回 null（不引导绑定）
    /// </summary>
    private static async Task<string?> GetTokenOrReply(Message message, long qq, string game)
    {
        string? token;
        try
        {
            // 返回 null = 未绑定；抛异常 = 换票失败（限流/网络/凭据错误）
            token = (await DivingFishTokenStore.GetValidToken(qq, game))?.AccessToken;
        }
        catch (Exception e)
        {
            message.Reply($"水鱼查分暂不可用：{e.Message}");
            return null;
        }

        if (token != null) return token;

        string url;
        try
        {
            url = await DivingFishOAuth.StartBinding(qq.ToString(), game);
        }
        catch (Exception e)
        {
            message.Reply($"绑定链接生成失败: {e.Message}");
            return null;
        }

        message.Reply($"未绑定水鱼账号，请先完成绑定：\n{url}\n\n链接 10 分钟内有效，完成授权后重新发送查询指令");
        return null;
    }

    protected sealed record DivingFishDxRatingResponse(string Nickname, List<SongScore> Records);
}
