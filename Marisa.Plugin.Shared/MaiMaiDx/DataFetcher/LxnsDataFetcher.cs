using System.Net;
using System.Text.Json;
using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Lxns;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

public class LxnsDataFetcher(SongDb<MaiMaiSong> songDb) : DataFetcher(songDb)
{
    private const string BaseUrl = "https://maimai.lxns.net/api/v0/maimai";

    /// <summary>没有可用 OAuth 授权时的提示。vs 的本地预检也用这句，保证预检与取数失败时口径一致。</summary>
    public const string NotBoundHint = "[Lxns] 请先使用 bind → 选择 lxns 完成 OAuth 授权后再试";

    /// <summary>
    ///     B50：本人用本人 OAuth 票据，他人用 bot 的 dev token 查公开数据（落雪没有按账号名的接口）。
    /// </summary>
    public override async Task<DxRating> GetRating(ResolvedPlayer player)
    {
        if (player.IsSelf)
        {
            var token    = await GetRequiredOAuthToken(player.Qq);
            var scores   = await GetScoresViaOAuth(token, player.Qq);
            var nickname = await GetNicknameViaOAuth(token, player.Qq);
            return BuildRating(scores, nickname);
        }

        return await FetchScores(player.Qq);
    }

    /// <summary>
    ///     完整成绩：用该 QQ 自己的落雪 OAuth 票据读，不走 bot 的 dev token 公共查询——
    ///     后者要求对方公开成绩、QQ 已关联落雪，会让同一对玩家两个方向的可用性不一致。
    /// </summary>
    public override async Task<(string? Nickname, Dictionary<(long Id, int LevelIdx), SongScore> Scores)>
        GetScores(ResolvedPlayer player)
    {
        var token    = await GetRequiredOAuthToken(player.Qq);
        var scores   = await GetScoresViaOAuth(token, player.Qq);
        var nickname = await GetNicknameViaOAuth(token, player.Qq);

        return (nickname, scores);
    }

    private async Task<Dictionary<(long Id, int LevelIdx), SongScore>> GetScoresViaOAuth(LxnsToken oauthToken, long qq)
    {
        var response = await "https://maimai.lxns.net/api/v0/user/maimai/player/scores"
            .WithOAuthBearerToken(oauthToken.AccessToken)
            .AllowHttpStatus("400,401,403,404")
            .GetAsync();

        if (response.StatusCode is 400 or 401 or 403 or 404)
        {
            if (response.StatusCode is 401 or 403)
                LxnsTokenStore.RemoveToken(qq);

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

    /// <summary>
    ///     本人和他人一样：用对方自己的 OAuth 票据读完整成绩再筛这一首。昵称也一并拿到，
    ///     不走 dev token 的公共单曲接口——那条路要求对方公开成绩、QQ 已关联落雪。
    /// </summary>
    public override async Task<(string? Nickname, Dictionary<int, SongScore> Scores)> GetSongScore(ResolvedPlayer player, MaiMaiSong song)
    {
        var token       = await GetRequiredOAuthToken(player.Qq);
        var oauthScores = await GetScoresViaOAuth(token, player.Qq);
        var nickname    = await GetNicknameViaOAuth(token, player.Qq);

        return (nickname, oauthScores
            .Where(x => x.Key.Id == song.Id)
            .ToDictionary(x => x.Key.LevelIdx, x => x.Value));
    }

    private async Task<LxnsToken> GetRequiredOAuthToken(long qq)
    {
        try
        {
            return await LxnsTokenStore.GetValidToken(qq)
                   ?? throw new HttpRequestException(NotBoundHint);
        }
        catch (HttpRequestException e) when (e.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
        {
            throw new HttpRequestException("[Lxns] OAuth 授权已失效，请重新使用 bind → 选择 lxns 完成授权");
        }
    }

    /// <summary>
    ///     bind 时实测令牌是否可读：本地存有票据不代表服务端仍接受它（可能已被撤销），
    ///     只有玩家接口返回 401/403 之外的状态才算授权有效；401/403 时丢票重拉一次。
    /// </summary>
    public override async Task<bool> TestOAuthToken(long qq)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var token = await LxnsTokenStore.GetValidToken(qq);
            if (token == null) return false;

            var response = await "https://maimai.lxns.net/api/v0/user/maimai/player"
                .WithOAuthBearerToken(token.AccessToken)
                .AllowHttpStatus("400,401,403,404,429,503")
                .GetAsync();

            if (response.StatusCode is 401 or 403)
            {
                LxnsTokenStore.RemoveToken(qq);
                continue;
            }

            if (response.StatusCode is 429 or 503) throw new HttpRequestException("落雪服务器繁忙，请稍后再试");
            if (response.StatusCode is 400) throw new HttpRequestException("落雪授权验证失败（HTTP 400）");

            // 2xx；404 表示授权有效，只是尚未在落雪绑定该游戏账号
            return true;
        }

        return false;
    }

    private async Task<string> GetNicknameViaOAuth(LxnsToken token, long qq)
    {
        var response = await "https://maimai.lxns.net/api/v0/user/maimai/player"
            .WithOAuthBearerToken(token.AccessToken)
            .AllowHttpStatus("400,401,403,404")
            .GetAsync();

        if (response.StatusCode is 400 or 401 or 403 or 404)
        {
            if (response.StatusCode is 401 or 403)
                LxnsTokenStore.RemoveToken(qq);

            var body = await response.GetStringAsync();
            throw new HttpRequestException($"[Lxns OAuth] {response.StatusCode}: {ReadErrorMessage(body)}");
        }

        using var doc = JsonDocument.Parse(await response.GetStringAsync());
        var root = doc.RootElement.TryGetProperty("data", out var data) ? data : doc.RootElement;
        return root.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
            ? name.GetString() ?? ""
            : root.TryGetProperty("nickname", out var nickname) && nickname.ValueKind == JsonValueKind.String
                ? nickname.GetString() ?? ""
                : "";
    }

    private DxRating BuildRating(Dictionary<(long Id, int LevelIdx), SongScore> scores, string nickname)
    {
        var groups = scores.Values
            .Where(x => x.Id <= 100000 && SongDb.SongIndexer.ContainsKey(x.Id))
            .GroupBy(x => SongDb.SongIndexer[x.Id].Info.IsNew)
            .ToList();

        return new DxRating
        {
            Nickname = nickname,
            OldScores = TopScores(groups.FirstOrDefault(x => !x.Key), DivingFishDataFetcher.OldScoreLimit),
            NewScores = TopScores(groups.FirstOrDefault(x => x.Key), DivingFishDataFetcher.NewScoreLimit)
        };

        static List<SongScore> TopScores(IEnumerable<SongScore>? source, int limit) => source?
            .OrderByDescending(x => x.Rating)
            .ThenByDescending(x => x.Id)
            .Take(limit)
            .ToList() ?? [];
    }

    private static string ReadErrorMessage(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String
                ? message.GetString() ?? "Unknown error"
                : "Unknown error";
        }
        catch
        {
            return "Unknown error";
        }
    }

    private async Task<DxRating> FetchScores(long qq)
    {
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
