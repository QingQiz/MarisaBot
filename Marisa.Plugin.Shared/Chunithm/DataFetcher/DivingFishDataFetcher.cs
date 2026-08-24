using System.Net;
using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.DivingFish;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

public class DivingFishDataFetcher(SongDb<ChunithmSong> songDb) : DataFetcher(songDb), ICanReset
{
    private Dictionary<string, ChunithmSong>? _songTitleIndexer;

    private Dictionary<string, ChunithmSong> SongTitleIndexer => _songTitleIndexer ??= GetSongList()
        .GroupBy(song => song.Title, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    public override List<ChunithmSong> GetSongList()
    {
        return LxnsDataFetcher.GetSharedSongList();
    }

    public override async Task<ChunithmRating> GetRating(Message message)
    {
        var (username, qq) = AtOrSelf(message, false);

        // OAuth 模式：查 b30+n20
        if (DivingFishOAuth.IsConfigured)
        {
            // 1. 优先走公开 /query/player（form-urlencoded，无需验证、不耗配额、服务端已截好 b30+n20）
            //    qq 或 username 都能查；400 user not exists（QQ 未绑定）/403 隐私 → 回落 OAuth
            try
            {
                var raw = username.IsWhiteSpace()
                    ? await FetchScoresByQq(qq)
                    : await FetchScoresByUsername(username);

                raw.DataSource = "DivingFish";
                raw.Records.Best = NormalizeRecords(raw.Records.Best).Where(x => !DeletedSongs.Contains(x.Id)).ToArray();
                raw.Records.Recent = NormalizeRecords(raw.Records.Recent).ToArray();
                return raw;
            }
            catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden)
            {
                // 2. 回落 OAuth：Bearer /player/records 全量 + 本地按版本分组截取 b30+n20
                var json = await FetchScores(message, false);
                json.DataSource = "DivingFish";
                json.Records.Best = NormalizeRecords(json.Records.Best).Where(x => !DeletedSongs.Contains(x.Id)).ToArray();
                json.Records.Recent = NormalizeRecords(json.Records.Recent).ToArray();
                return GroupBestAndRecent(json);
            }
        }

        // DevToken 模式（废弃端点，过渡期兼容）：完整成绩按版本分组截取
        var devJson = await FetchScores(message, false);
        devJson.DataSource = "DivingFish";
        devJson.Records.Best = NormalizeRecords(devJson.Records.Best).Where(x => !DeletedSongs.Contains(x.Id)).ToArray();
        devJson.Records.Recent = NormalizeRecords(devJson.Records.Recent).ToArray();

        return GroupBestAndRecent(devJson);
    }

    /// <summary>
    ///     把完整成绩按版本新旧分组：旧版本取 rating 前 30 作为 Best，新版本取前 20 作为 Recent。
    ///     水鱼 OAuth 的 /player/records 返回全量成绩，必须截取，否则前端会渲染全部记录。
    /// </summary>
    private static ChunithmRating GroupBestAndRecent(ChunithmRating raw)
    {
        var allScores = raw.Records.Best.Concat(raw.Records.Recent);

        var songList = LxnsDataFetcher.GetSharedSongList();
        var versionMap = songList.ToDictionary(s => s.Id, s => s.Version);

        var newest = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CHUNITHM LUMINOUS PLUS", "CHUNITHM VERSE"
        };

        var div = allScores
            .GroupBy(x => newest.Contains(versionMap.GetValueOrDefault(x.Id, "")))
            .ToList();

        return new ChunithmRating
        {
            DataSource = raw.DataSource,
            Username = raw.Username,
            Records = new Records
            {
                Best = div.FirstOrDefault(x => !x.Key)?
                           .OrderByDescending(x => x.Rating).Take(30).ToArray() ?? [],
                Recent = div.FirstOrDefault(x => x.Key)?
                             .OrderByDescending(x => x.Rating).Take(20).ToArray() ?? []
            }
        };
    }

    public override async Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScores(Message message)
    {
        var scores = await GetScoresCore(message, true);

        return scores.Records.Best
            .Where(x => !DeletedSongs.Contains(x.Id))
            .ToDictionary(x => (x.Id, (int)x.LevelIndex), x => x);
    }

    private async Task<ChunithmRating> GetScoresCore(Message message, bool qqOnly)
    {
        var json = await FetchScores(message, qqOnly);
        json.DataSource = "DivingFish";
        json.Records.Best = NormalizeRecords(json.Records.Best).Where(x => !DeletedSongs.Contains(x.Id)).ToArray();
        json.Records.Recent = NormalizeRecords(json.Records.Recent).ToArray();

        return json;
    }

    /// <summary>
    ///     公开端点 /query/player：按用户名查 b30+n20（form-urlencoded，无需验证）
    /// </summary>
    protected virtual async Task<ChunithmRating> FetchScoresByUsername(ReadOnlyMemory<char> username)
    {
        var response = await "https://www.diving-fish.com/api/chunithmprober/query/player"
            .AllowHttpStatus("400,403")
            .PostUrlEncodedAsync(new Dictionary<string, string>
            {
                ["username"] = username.ToString()
            });

        if (response.StatusCode is 400 or 403)
        {
            var body = await response.GetStringAsync();
            throw new HttpRequestException(ProberError.DivingFish(response.StatusCode, body),
                null, (HttpStatusCode)response.StatusCode);
        }

        return await response.GetJsonAsync<ChunithmRating>();
    }

    /// <summary>
    ///     公开端点 /query/player：按 QQ 号查 b30+n20（form-urlencoded，无需验证）
    /// </summary>
    protected virtual async Task<ChunithmRating> FetchScoresByQq(long qq)
    {
        var response = await "https://www.diving-fish.com/api/chunithmprober/query/player"
            .AllowHttpStatus("400,403")
            .PostUrlEncodedAsync(new Dictionary<string, string>
            {
                ["qq"] = qq.ToString()
            });

        if (response.StatusCode is 400 or 403)
        {
            var body = await response.GetStringAsync();
            throw new HttpRequestException(ProberError.DivingFish(response.StatusCode, body),
                null, (HttpStatusCode)response.StatusCode);
        }

        return await response.GetJsonAsync<ChunithmRating>();
    }

    protected virtual async Task<ChunithmRating> FetchScores(Message message, bool qqOnly)
    {
        var (username, qq) = AtOrSelf(message, qqOnly);

        // OAuth 模式：查询对象由 token 决定，URL 不带 qq/username
        if (DivingFishOAuth.IsConfigured)
        {
            var token = await GetTokenOrReply(message, qq, "chunithm");
            if (token == null) return new ChunithmRating();

            var response = await "https://www.diving-fish.com/api/chunithmprober/player/records"
                .WithHeader("Authorization", $"Bearer {token}")
                .AllowHttpStatus("400,401,403,429")
                .GetAsync();

            if (response.StatusCode is 400 or 401 or 403 or 429)
            {
                if (response.StatusCode == 401) DivingFishTokenStore.RemoveToken(qq, "chunithm");
                var body = await response.GetStringAsync();
                throw new HttpRequestException(ProberError.DivingFish(response.StatusCode, body));
            }

            return await response.GetJsonAsync<ChunithmRating>();
        }

        // DevToken 模式（废弃端点，过渡期兼容）
        var uri = username.IsWhiteSpace()
            ? $"https://www.diving-fish.com/api/chunithmprober/dev/player/records?qq={qq}"
            : $"https://www.diving-fish.com/api/chunithmprober/dev/player/records?username={username}";

        var devResponse = await uri
            .WithHeader("Developer-Token", ConfigurationManager.Configuration.DivingFish.DevToken)
            .AllowHttpStatus("400,401,403")
            .GetAsync();

        if (devResponse.StatusCode is 400 or 401 or 403)
        {
            var body = await devResponse.GetStringAsync();
            throw new HttpRequestException(ProberError.DivingFish(devResponse.StatusCode, body));
        }

        return await devResponse.GetJsonAsync<ChunithmRating>();
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

    private IEnumerable<ChunithmScore> NormalizeRecords(IEnumerable<ChunithmScore> records)
    {
        foreach (var record in records)
        {
            if (SongDb.SongIndexer.ContainsKey(record.Id))
            {
                yield return record;
                continue;
            }

            if (!SongTitleIndexer.TryGetValue(record.Title, out var matchedSong)) continue;

            record.Id = matchedSong.Id;
            yield return record;
        }
    }

    public void Reset()
    {
        _songTitleIndexer = null;
    }
}
