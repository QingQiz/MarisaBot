using System.Text.Json;
using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Lxns;
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

public class LxnsDataFetcher(SongDb<MaiMaiSong> songDb) : DataFetcher(songDb)
{
    private const string BaseUrl = "https://maimai.lxns.net/api/v0/maimai";
    private static readonly Dictionary<string, (DateTime Time, Dictionary<(long, int), SongScore> Scores)> ScoresCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public override async Task<DxRating> GetRating(Message message)
    {
        return await FetchScores(message);
    }

    public override async Task<Dictionary<(long Id, int LevelIdx), SongScore>> GetScores(Message message)
    {
        var (_, qq) = Shared.Chunithm.DataFetcher.DataFetcher.AtOrSelf(message, true);

        // 优先 OAuth 个人 API (1 次请求拿全量带达成率)
        var oauthToken = await LxnsTokenStore.GetValidToken(qq);
        if (oauthToken != null)
        {
            return await GetScoresViaOAuth(oauthToken);
        }

        // 无 OAuth token → 引导用户绑定
        throw new HttpRequestException("[Lxns] 请先使用 bind → 选择 lxns 完成 OAuth 授权后再试");

        // === 以下 dev token 两阶段抓取已废弃 ===
        /*
        // 回落 dev token 两阶段抓取
        var token = ConfigurationManager.Configuration.Lxns.DevToken;
        ...
        */
    }

    private async Task<Dictionary<(long Id, int LevelIdx), SongScore>> GetScoresViaOAuth(LxnsToken oauthToken)
    {
        var response = await "https://maimai.lxns.net/api/v0/user/maimai/player/scores"
            .WithOAuthBearerToken(oauthToken.AccessToken)
            .AllowHttpStatus("400,401,403,404")
            .GetAsync();

        if (response.StatusCode is 400 or 401 or 403 or 404)
        {
            var errorJson = await response.GetStringAsync();
            using var errorDoc = JsonDocument.Parse(errorJson);
            var errorMessage = errorDoc.RootElement.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? "Unknown error"
                : "Unknown error";
            throw new HttpRequestException($"[Lxns OAuth] {response.StatusCode}: {errorMessage}");
        }

        var jsonString = await response.GetStringAsync();
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement.TryGetProperty("data", out var data) ? data : doc.RootElement;

        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("scores", out var scArr))
            root = scArr;

        var result = new Dictionary<(long Id, int LevelIdx), SongScore>();
        foreach (var s in root.EnumerateArray())
        {
            var lvlIdx = s.TryGetProperty("level_index", out var li) ? li.GetInt32() : -1;
            if (lvlIdx < 0) continue;

            var rawId = s.GetProperty("id").GetInt32();
            var type = s.TryGetProperty("type", out var tp) ? tp.GetString() ?? "standard" : "standard";
            var songId = type == "dx" ? int.Parse("1" + rawId.ToString().PadLeft(4, '0')) : rawId;

            var score = new SongScore
            {
                Id = songId,
                Title = s.TryGetProperty("song_name", out var sn) ? sn.GetString() ?? "" : "",
                LevelIdx = lvlIdx,
                Level = s.TryGetProperty("level", out var lv) ? lv.GetString() ?? "" : "",
                Achievement = s.TryGetProperty("achievements", out var ach) ? ach.GetDouble() : 0,
                DxScore = s.TryGetProperty("dx_score", out var dx) ? dx.GetInt32() : 0,
                Fc = s.TryGetProperty("fc", out var fc) ? fc.GetString() ?? "" : "",
                Fs = s.TryGetProperty("fs", out var fs) ? fs.GetString() ?? "" : "",
                Type = type == "standard" ? "SD" : "DX",
                Constant = 0
            };

            if (SongDb.SongIndexer.TryGetValue(score.Id, out var song) &&
                score.LevelIdx >= 0 && score.LevelIdx < song.Constants.Count)
            {
                score.Constant = song.Constants[score.LevelIdx];
            }

            result[(songId, lvlIdx)] = score;
        }

        return result;
    }

    private async Task<DxRating> FetchScores(Message message)
    {
        var (_, qq) = Shared.Chunithm.DataFetcher.DataFetcher.AtOrSelf(message, true);

        var playerResponse = await $"{BaseUrl}/player/qq/{qq}"
            .WithHeader("Authorization", ConfigurationManager.Configuration.Lxns.DevToken)
            .AllowHttpStatus("400,401,403,404")
            .GetAsync();

        if (playerResponse.StatusCode is 400 or 401 or 403 or 404)
        {
            var body = await playerResponse.GetStringAsync();
            throw new HttpRequestException(HttpRequestError.Unknown, ProberError.Lxns(playerResponse.StatusCode, body));
        }

        var playerJson = await playerResponse.GetStringAsync();
        using var playerDoc = JsonDocument.Parse(playerJson);
        var data = playerDoc.RootElement.GetProperty("data");
        var friendCode = data.GetProperty("friend_code").GetInt64().ToString();
        var playerName = data.GetProperty("name").GetString() ?? "";

        var response = await $"{BaseUrl}/player/{friendCode}/bests"
            .WithHeader("Authorization", ConfigurationManager.Configuration.Lxns.DevToken)
            .AllowHttpStatus("400,401,403,404")
            .GetAsync();

        if (response.StatusCode is 400 or 401 or 403 or 404)
        {
            var body = await response.GetStringAsync();
            throw new HttpRequestException(HttpRequestError.Unknown, ProberError.Lxns(response.StatusCode, body));
        }

        var jsonString = await response.GetStringAsync();
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        var responseData = root.TryGetProperty("data", out var dataElement) ? dataElement : root;

        var standardScores = responseData.TryGetProperty("standard", out var standard)
            ? ParseScores(standard, SongDb)
            : new List<SongScore>();
        var dxScores = responseData.TryGetProperty("dx", out var dx)
            ? ParseScores(dx, SongDb)
            : new List<SongScore>();

        return new DxRating
        {
            Nickname = playerName,
            OldScores = standardScores,
            NewScores = dxScores
        };
    }

    private static List<SongScore> ParseScores(JsonElement scoresElement, SongDb<MaiMaiSong> songDb)
    {
        var scores = new List<SongScore>();
        foreach (var s in scoresElement.EnumerateArray())
        {
            var rawId = s.GetProperty("id").GetInt32();
            var type = s.GetProperty("type").GetString();

            int songId = rawId;
            if (type == "dx")
            {
                songId = int.Parse("1" + rawId.ToString().PadLeft(4, '0'));
            }

            var score = new SongScore
            {
                Id = songId,
                Title = s.GetProperty("song_name").GetString() ?? "",
                LevelIdx = s.GetProperty("level_index").GetInt32(),
                Level = s.GetProperty("level").GetString() ?? "",
                Achievement = s.GetProperty("achievements").GetDouble(),
                DxScore = s.TryGetProperty("dx_score", out var dx) ? dx.GetInt32() : 0,
                Fc = s.TryGetProperty("fc", out var fc) ? fc.GetString() ?? "" : "",
                Fs = s.TryGetProperty("fs", out var fs) ? fs.GetString() ?? "" : "",
                Type = type == "standard" ? "SD" : "DX",
                Constant = 0
            };

            // 匹配歌曲并设置 Constant 值
            if (songDb.SongIndexer.TryGetValue(score.Id, out var song) && 
                score.LevelIdx >= 0 && score.LevelIdx < song.Constants.Count)
            {
                score.Constant = song.Constants[score.LevelIdx];
            }

            scores.Add(score);
        }
        return scores;
    }
}
