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

    protected virtual bool OAuthEnabled => DivingFishOAuth.IsConfigured;

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

        var isSelf = username.IsWhiteSpace() && qq == message.Sender.Id;

        if (OAuthEnabled)
        {
            try
            {
                var raw = username.IsWhiteSpace()
                    ? await FetchScoresByQq(qq)
                    : await FetchScoresByUsername(username);

                raw.DataSource = "DivingFish";
                raw.Records.Best = NormalizeRecords(raw.Records.Best).Where(x => !DeletedSongs.Contains(x.Id)).ToArray();
                var newBest = raw.Records.N20.Length > 0 ? raw.Records.N20 : raw.Records.Recent;
                raw.Records.Recent = NormalizeRecords(newBest).Where(x => !DeletedSongs.Contains(x.Id)).ToArray();
                return raw;
            }
            catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden)
            {
                if (!isSelf) throw;

                var json = await FetchScores(message, false);
                json.DataSource = "DivingFish";
                json.Records.Best = NormalizeRecords(json.Records.Best).Where(x => !DeletedSongs.Contains(x.Id)).ToArray();
                json.Records.Recent = NormalizeRecords(json.Records.Recent).ToArray();
                return await GroupBestAndRecent(json);
            }
        }

        var devJson = await FetchScores(message, false);
        devJson.DataSource = "DivingFish";
        devJson.Records.Best = NormalizeRecords(devJson.Records.Best).Where(x => !DeletedSongs.Contains(x.Id)).ToArray();
        devJson.Records.Recent = NormalizeRecords(devJson.Records.Recent).ToArray();

        return await GroupBestAndRecent(devJson);
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

    protected virtual async Task<ChunithmRating> FetchScores(Message message, bool qqOnly)
    {
        var (username, qq) = AtOrSelf(message, qqOnly);
        var isSelf = username.IsWhiteSpace() && qq == message.Sender.Id;

        if (OAuthEnabled)
        {
            if (!isSelf)
            {
                throw OAuthSelfOnly();
            }

            var response = await SendBearerWithOneRetry(message, "chunithm", token =>
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

        var uri = username.IsWhiteSpace()
            ? $"https://www.diving-fish.com/api/chunithmprober/dev/player/records?qq={qq}"
            : $"https://www.diving-fish.com/api/chunithmprober/dev/player/records?username={username}";

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

    private static async Task<string> GetRequiredToken(Message message, string game)
    {
        var token = (await DivingFishTokenStore.GetValidToken(message.Sender.Id, game))?.AccessToken;
        if (token != null) return token;
        throw new HttpRequestException("未绑定水鱼查分器，请先使用 bind 命令完成绑定后再查询");
    }

    private static async Task<IFlurlResponse> SendBearerWithOneRetry(
        Message message,
        string game,
        Func<string, Task<IFlurlResponse>> send)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await GetRequiredToken(message, game);

            var response = await send(token);
            if (response.StatusCode != (int)HttpStatusCode.Unauthorized) return response;

            DivingFishTokenStore.RemoveToken(message.Sender.Id, game);
            if (attempt == 1) return response;
        }

        throw new InvalidOperationException("DivingFish OAuth retry loop exited unexpectedly");
    }

    private static bool IsOAuthError(int statusCode) =>
        statusCode is 400 or 401 or 403 or 429 or 503;

    private static HttpRequestException OAuthSelfOnly() =>
        new("水鱼 OAuth 只能读取发送者本人的完整成绩；查询用户名或 @ 他人仅支持公开成绩");

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
