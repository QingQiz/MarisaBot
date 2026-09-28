using System.Net;
using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.DivingFish;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

public class DivingFishDataFetcher(SongDb<ChunithmSong> songDb) : DataFetcher(songDb), ICanReset
{
    private Dictionary<string, ChunithmSong>? _songTitleIndexer;

    protected virtual bool OAuthEnabled => DivingFishOAuth.IsConfigured;

    private Dictionary<string, ChunithmSong> SongTitleIndexer => _songTitleIndexer ??= GetSongList()
        .GroupBy(song => song.Title, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    public override List<ChunithmSong> GetSongList()
    {
        return LxnsDataFetcher.GetSharedSongList();
    }

    public override async Task<ChunithmRating> GetRating(ResolvedPlayer player)
    {
        if (OAuthEnabled)
        {
            try
            {
                // 公开 B30/N20：按账号名或 QQ 查，水鱼只返回已公开的成绩
                var raw = player.Username is { } username
                    ? await FetchScoresByUsername(username.AsMemory())
                    : await FetchScoresByQq(player.Qq);

                raw.DataSource = "DivingFish";
                raw.Records.Best = KnownSongs(raw.Records.Best).ToArray();
                var newBest = raw.Records.N20.Length > 0 ? raw.Records.N20 : raw.Records.Recent;
                raw.Records.Recent = KnownSongs(newBest).ToArray();
                return raw;
            }
            catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden)
            {
                // 公开成绩拿不到本人（未公开等）时，改用票据读完整记录再本地分组
                if (!player.IsSelf) throw;

                return await GroupBestAndRecent(await LoadRecords(player));
            }
        }

        return await GroupBestAndRecent(await LoadRecords(player));
    }

    /// <summary>票据/dev token 读到的完整记录：去掉已删曲目并标记来源（Recent 交给 GroupBestAndRecent 重新分组）。</summary>
    private async Task<ChunithmRating> LoadRecords(ResolvedPlayer player)
    {
        var json = await FetchScores(player);

        json.DataSource = "DivingFish";
        json.Records.Best = KnownSongs(json.Records.Best).ToArray();
        json.Records.Recent = NormalizeRecords(json.Records.Recent).ToArray();

        return json;
    }

    private async Task<ChunithmRating> GroupBestAndRecent(ChunithmRating raw)
    {
        var allScores = raw.Records.Best.Concat(raw.Records.Recent);

        var songList = GetSongList();
        var versionMap = songList.ToDictionary(s => s.Id, s => s.Version);
        var newest = await FetchLatestVersions();

        var div = allScores
            .GroupBy(x => newest.Contains(NormalizeVersion(versionMap.GetValueOrDefault(x.Id, ""))))
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

    public override async Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScores(ResolvedPlayer player)
    {
        var scores = await FetchScores(player);

        return KnownSongs(scores.Records.Best)
            .ToDictionary(x => (x.Id, (int)x.LevelIndex), x => x);
    }

    protected virtual async Task<ChunithmRating> FetchScoresByUsername(ReadOnlyMemory<char> username)
    {
        var response = await "https://www.diving-fish.com/api/chunithmprober/query/player"
            .AllowHttpStatus("400,403")
            .PostJsonAsync(new
            {
                username = username.ToString()
            });

        if (response.StatusCode is 400 or 403)
        {
            var body = await response.GetStringAsync();
            throw new HttpRequestException(ProberError.DivingFish(response.StatusCode, body),
                null, (HttpStatusCode)response.StatusCode);
        }

        return await response.GetJsonAsync<ChunithmRating>();
    }

    protected virtual async Task<ChunithmRating> FetchScoresByQq(long qq)
    {
        var response = await "https://www.diving-fish.com/api/chunithmprober/query/player"
            .AllowHttpStatus("400,403")
            .PostJsonAsync(new
            {
                qq
            });

        if (response.StatusCode is 400 or 403)
        {
            var body = await response.GetStringAsync();
            throw new HttpRequestException(ProberError.DivingFish(response.StatusCode, body),
                null, (HttpStatusCode)response.StatusCode);
        }

        return await response.GetJsonAsync<ChunithmRating>();
    }

    /// <summary>完整记录：配置了 OAuth 时用本人票据读（只有本人能读），否则用 dev token 按账号名或 QQ 查。</summary>
    protected virtual async Task<ChunithmRating> FetchScores(ResolvedPlayer player)
    {
        if (OAuthEnabled)
        {
            if (!player.IsSelf)
            {
                throw OAuthSelfOnly();
            }

            var response = await SendBearerWithOneRetry(player.Qq, "chunithm", token =>
                "https://www.diving-fish.com/api/chunithmprober/player/records"
                    .WithHeader("Authorization", $"Bearer {token}")
                    .AllowHttpStatus("400,401,403,429,503")
                    .GetAsync());

            if (IsOAuthError(response.StatusCode))
            {
                var body = await response.GetStringAsync();
                throw new HttpRequestException(ProberError.DivingFishOAuth(response.StatusCode, body),
                    null, (HttpStatusCode)response.StatusCode);
            }

            return await response.GetJsonAsync<ChunithmRating>();
        }

        var uri = player.Username is { } username
            ? $"https://www.diving-fish.com/api/chunithmprober/dev/player/records?username={username}"
            : $"https://www.diving-fish.com/api/chunithmprober/dev/player/records?qq={player.Qq}";

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

        return await devResponse.GetJsonAsync<ChunithmRating>();
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

    /// <summary>
    ///     bind 时实测票据是否可读：本地存有票据不代表服务端仍接受它（可能已被撤销），
    ///     只有成绩接口返回 2xx 才算授权有效；401 时丢票重拉一次。
    /// </summary>
    public override async Task<bool> TestOAuthToken(long qq)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = (await DivingFishTokenStore.GetValidToken(qq, "chunithm"))?.AccessToken;
            if (token == null) return false;

            var response = await "https://www.diving-fish.com/api/chunithmprober/player/records"
                .WithHeader("Authorization", $"Bearer {token}")
                .AllowHttpStatus("400,401,403,429,503")
                .GetAsync();

            if (response.StatusCode == (int)HttpStatusCode.Unauthorized)
            {
                DivingFishTokenStore.RemoveToken(qq, "chunithm");
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
        new("水鱼 OAuth 只能读取发送者本人的完整成绩；查询用户名或 @ 他人仅支持公开成绩");

    /// <summary>已删除的歌在查分器侧可能还留着记录，统计与展示前先剔掉。</summary>
    private IEnumerable<ChunithmScore> KnownSongs(IEnumerable<ChunithmScore> records) =>
        NormalizeRecords(records).Where(x => !DeletedSongs.Contains(x.Id));

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
        ResetLatestVersionsCache();
    }
}
