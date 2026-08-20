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
        var (username, _) = AtOrSelf(message, false);

        // OAuth 模式：指定了 username 时走公开 /query/player（b30+n20，无需验证）
        if (DivingFishOAuth.IsConfigured && !username.IsWhiteSpace())
        {
            var raw = await FetchScoresByUsername(username);
            raw.DataSource = "DivingFish";
            raw.Records.Best = NormalizeRecords(raw.Records.Best).Where(x => !DeletedSongs.Contains(x.Id)).ToArray();
            raw.Records.Recent = NormalizeRecords(raw.Records.Recent).ToArray();
            return raw;
        }

        var json = await FetchScores(message, false);
        json.DataSource = "DivingFish";
        json.Records.Best = NormalizeRecords(json.Records.Best).Where(x => !DeletedSongs.Contains(x.Id)).ToArray();
        json.Records.Recent = NormalizeRecords(json.Records.Recent).ToArray();

        return json;
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
    ///     公开端点 /query/player：按用户名查 b30+n20（无需验证）
    /// </summary>
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
            throw new HttpRequestException(ProberError.DivingFish(response.StatusCode, body));
        }

        return await response.GetJsonAsync<ChunithmRating>();
    }

    protected virtual async Task<ChunithmRating> FetchScores(Message message, bool qqOnly)
    {
        var (username, qq) = AtOrSelf(message, qqOnly);

        // OAuth 模式：查询对象由 token 决定，URL 不带 qq/username
        if (DivingFishOAuth.IsConfigured)
        {
            var token = await GetTokenOrReply(message, qq);
            if (token == null) return new ChunithmRating();

            var response = await "https://www.diving-fish.com/api/chunithmprober/player/records"
                .WithHeader("Authorization", $"Bearer {token}")
                .AllowHttpStatus("400,401,403,429")
                .GetAsync();

            if (response.StatusCode is 400 or 401 or 403 or 429)
            {
                if (response.StatusCode == 401) DivingFishTokenStore.RemoveToken(qq);
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
    ///     获取 OAuth token；未绑定时回复绑定提示并返回 null
    /// </summary>
    private static async Task<string?> GetTokenOrReply(Message message, long qq)
    {
        var token = await DivingFishTokenStore.GetValidToken(qq);
        if (token != null) return token.AccessToken;

        string url;
        try
        {
            url = await DivingFishOAuth.StartBinding(qq.ToString());
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
