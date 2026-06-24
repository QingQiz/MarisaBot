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

        // 回落 dev token 两阶段抓取
        var token = ConfigurationManager.Configuration.Lxns.DevToken;

        // Step 0: QQ → friend_code
        var playerResponse = await $"{BaseUrl}/player/qq/{qq}"
            .WithHeader("Authorization", token)
            .AllowHttpStatus("400,401,403,404")
            .GetAsync();

        if (playerResponse.StatusCode is 400 or 401 or 403 or 404)
        {
            var errorJson = await playerResponse.GetStringAsync();
            using var errorDoc = JsonDocument.Parse(errorJson);
            var errorMessage = errorDoc.RootElement.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? "Player not found"
                : "Player not found";
            throw new HttpRequestException($"[Lxns] {playerResponse.StatusCode}: {errorMessage}");
        }

        var playerJson = await playerResponse.GetStringAsync();
        using var playerDoc = JsonDocument.Parse(playerJson);
        var data = playerDoc.RootElement.GetProperty("data");
        var friendCode = data.GetProperty("friend_code").GetInt64().ToString();

        // 检查缓存
        lock (ScoresCache)
        {
            if (ScoresCache.TryGetValue(friendCode, out var cached) && DateTime.UtcNow - cached.Time < CacheTtl)
            {
                return new Dictionary<(long, int), SongScore>(cached.Scores);
            }
        }

        // Step 1: GET /scores → SimpleScore 全量, 过滤 achievements >= 97.0% (rank >= S)
        var scoresResponse = await $"{BaseUrl}/player/{friendCode}/scores"
            .WithHeader("Authorization", token)
            .AllowHttpStatus("400,401,403,404")
            .GetAsync();

        if (scoresResponse.StatusCode is 400 or 401 or 403 or 404)
        {
            var errorJson = await scoresResponse.GetStringAsync();
            using var errorDoc = JsonDocument.Parse(errorJson);
            var errorMessage = errorDoc.RootElement.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? "Unknown error"
                : "Unknown error";
            throw new HttpRequestException($"[Lxns] {scoresResponse.StatusCode}: {errorMessage}");
        }

        var scoresJson = await scoresResponse.GetStringAsync();
        using var scoresDoc = JsonDocument.Parse(scoresJson);
        var scoresRoot = scoresDoc.RootElement.TryGetProperty("data", out var scoresData) ? scoresData : scoresDoc.RootElement;

        // /scores 可能嵌套在 "scores" 或 "records" 键内
        if (scoresRoot.ValueKind == JsonValueKind.Object)
        {
            if (scoresRoot.TryGetProperty("scores", out var scArr)) scoresRoot = scArr;
            else if (scoresRoot.TryGetProperty("records", out var recArr)) scoresRoot = recArr;
        }

        var candidates = new List<(long Id, int LevelIdx, string Type)>();
        foreach (var s in scoresRoot.EnumerateArray())
        {
            var ach = s.TryGetProperty("achievements", out var ac) ? ac.GetDouble() : 0;
            var lvlIdx = s.TryGetProperty("level_index", out var li) ? li.GetInt32() : -1;
            var type = s.TryGetProperty("type", out var tp) ? tp.GetString() ?? "standard" : "standard";
            if (ach >= 97.0 && lvlIdx >= 0)
            {
                candidates.Add((s.GetProperty("id").GetInt64(), lvlIdx, type));
            }
        }

        candidates = candidates.Distinct().ToList();

        // Step 2: 并发抓 /best
        var result = new Dictionary<(long Id, int LevelIdx), SongScore>();
        var semaphore = new SemaphoreSlim(4);
        var allTasks = candidates.Select(async c =>
        {
            await semaphore.WaitAsync();
            try
            {
                for (var retry = 0; retry < 3; retry++)
                {
                    try
                    {
                        var url = $"{BaseUrl}/player/{friendCode}/best?song_id={c.Id}&level_index={c.LevelIdx}";
                        
                        var bestResp = await url
                            .WithHeader("Authorization", token)
                            .AllowHttpStatus("404,429,500-599")
                            .GetAsync();

                        var statusCode = (int)bestResp.StatusCode;
                        if (statusCode == 404) return;
                        if (statusCode == 429 || statusCode >= 500)
                        {
                            await Task.Delay(500 * (int)Math.Pow(2, retry));
                            continue;
                        }

                        var bestJson = await bestResp.GetStringAsync();
                        using var bestDoc = JsonDocument.Parse(bestJson);
                        var bestRoot = bestDoc.RootElement.TryGetProperty("data", out var bestData) ? bestData : bestDoc.RootElement;

                        // /best 可能嵌套在 "score" 或 "best" 键内
                        if (bestRoot.ValueKind == JsonValueKind.Object)
                        {
                            if (bestRoot.TryGetProperty("score", out var sc)) bestRoot = sc;
                            else if (bestRoot.TryGetProperty("best", out var bst)) bestRoot = bst;
                        }

                        var rawId = bestRoot.GetProperty("id").GetInt32();
                        var type = bestRoot.TryGetProperty("type", out var bt) ? bt.GetString() ?? "standard" : "standard";
                        var songId = type == "dx" ? int.Parse("1" + rawId.ToString().PadLeft(4, '0')) : rawId;

                        var score = new SongScore
                        {
                            Id = songId,
                            Title = bestRoot.TryGetProperty("song_name", out var bn) ? bn.GetString() ?? "" : "",
                            LevelIdx = bestRoot.TryGetProperty("level_index", out var bli) ? bli.GetInt32() : c.LevelIdx,
                            Level = bestRoot.TryGetProperty("level", out var blv) ? blv.GetString() ?? "" : "",
                            Achievement = bestRoot.TryGetProperty("achievements", out var bac) ? bac.GetDouble() : 0,
                            DxScore = bestRoot.TryGetProperty("dx_score", out var bdx) ? bdx.GetInt32() : 0,
                            Fc = bestRoot.TryGetProperty("fc", out var bfc) ? bfc.GetString() ?? "" : "",
                            Fs = bestRoot.TryGetProperty("fs", out var bfs) ? bfs.GetString() ?? "" : "",
                            Type = type == "standard" ? "SD" : "DX",
                            Constant = 0
                        };

                        if (SongDb.SongIndexer.TryGetValue(score.Id, out var song) &&
                            score.LevelIdx >= 0 && score.LevelIdx < song.Constants.Count)
                        {
                            score.Constant = song.Constants[score.LevelIdx];
                        }

                        lock (result)
                        {
                            result[(songId, score.LevelIdx)] = score;
                        }
                        return;
                    }
                    catch
                    {
                        if (retry == 2) throw;
                        await Task.Delay(500 * (int)Math.Pow(2, retry));
                    }
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(allTasks);

        lock (ScoresCache)
        {
            ScoresCache[friendCode] = (DateTime.UtcNow, result);
        }

        return result;
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
