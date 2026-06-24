using System.Text.Json;
using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Lxns;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

public class LxnsDataFetcher(SongDb<ChunithmSong> songDb) : DataFetcher(songDb), ICanReset
{
    private const string BaseUrl = "https://maimai.lxns.net/api/v0/chunithm";
    private static List<ChunithmSong>? _songList;
    private static readonly Dictionary<string, (DateTime Time, Dictionary<(long, int), ChunithmScore> Scores)> ScoresCache = new();
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    internal static List<ChunithmSong> GetSharedSongList()
    {
        if (_songList != null) return _songList;

        var response = "https://maimai.lxns.net/api/v0/chunithm/song/list?notes=true"
            .GetJsonAsync().Result;

        var versionMap = new Dictionary<int, string>();
        foreach (var v in response.versions)
        {
            versionMap[(int)v.version] = (string)v.title;
        }

        var allSongs = ((IEnumerable<dynamic>)response.songs).ToList();

        var normalData = new List<dynamic>();
        var weSongData = new List<dynamic>();
        var weOriginIds = new List<int>();

        foreach (var song in allSongs)
        {
            if (song.difficulties.Count > 0 && song.difficulties[0].difficulty == 5)
            {
                weSongData.Add(song);
                weOriginIds.Add((int)song.difficulties[0].origin_id);
            }
            else
            {
                normalData.Add(song);
            }
        }

        _songList = normalData
            .Select(x => new ChunithmSong(x, ChunithmSong.DataSource.Lxns))
            .Where(x => !DeletedSongs.Contains(x.Id))
            .ToList();

        for (var i = 0; i < weSongData.Count; i++)
        {
            var weSong = weSongData[i];
            var originId = weOriginIds[i];

            var parent = _songList.FirstOrDefault(s => s.Id == originId);
            if (parent == null) continue;

            var weChart = weSong.difficulties[0];

            parent.AddDifficulty(
                level: (string)weChart.level,
                constant: (double)weChart.level_value,
                charter: (string)weChart.note_designer,
                diffName: ChunithmSong.LevelLabel[5],
                maxCombo: weChart.notes?.total ?? 0,
                chartName: "",
                bpm: weSong.bpm.ToString()
            );
        }

        foreach (var song in _songList)
        {
            if (int.TryParse(song.Version, out var versionId) && versionMap.TryGetValue(versionId, out var versionName))
            {
                song.Version = versionName;
            }
        }

        return _songList;
    }

    public override List<ChunithmSong> GetSongList()
    {
        return GetSharedSongList();
    }

    public override async Task<ChunithmRating> GetRating(Message message)
    {
        return await FetchScores(message);
    }

    public override async Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScores(Message message)
    {
        var (_, qq) = AtOrSelf(message, true);

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
                return new Dictionary<(long, int), ChunithmScore>(cached.Scores);
            }
        }

        // Step 1: GET /scores → SimpleScore 全量, 过滤 level_index ∈ {3,4} && score >= 975000
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

        var candidates = new List<(long Id, int LevelIdx)>();
        foreach (var s in scoresRoot.EnumerateArray())
        {
            var scoreVal = s.TryGetProperty("score", out var sc) ? sc.GetInt32() : 0;
            var lvlIdx = s.TryGetProperty("level_index", out var li) ? li.GetInt32() : -1;
            if (scoreVal >= 975000 && (lvlIdx == 3 || lvlIdx == 4))
            {
                candidates.Add((s.GetProperty("id").GetInt64(), lvlIdx));
            }
        }

        // 去重 (同一谱面可能有多条成绩? 保险)
        candidates = candidates.Distinct().ToList();

        // 构建歌曲匹配索引 (复用 FetchScores 逻辑)
        var lxnsSongs = GetSongList();
        var lxnsSongById = lxnsSongs.ToDictionary(s => s.Id, s => s);
        var lxnsSongByTitle = lxnsSongs.DistinctBy(s => s.Title).ToDictionary(s => s.Title, s => s);

        // Step 2: 并发抓 /best
        var result = new Dictionary<(long Id, int LevelIdx), ChunithmScore>();
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
                        if (statusCode == 404) return; // 没打过, 跳过
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

                        var score = new ChunithmScore
                        {
                            Id = bestRoot.GetProperty("id").GetInt32(),
                            Title = bestRoot.GetProperty("song_name").GetString() ?? "",
                            LevelIndex = bestRoot.TryGetProperty("level_index", out var bli) ? bli.GetInt32() : c.LevelIdx,
                            Level = bestRoot.TryGetProperty("level", out var blv) ? blv.GetString() ?? "" : "",
                            Achievement = bestRoot.TryGetProperty("score", out var bsc) ? bsc.GetInt32() : 0,
                            Fc = bestRoot.TryGetProperty("full_combo", out var bfc) ? bfc.GetString() ?? "" : "",
                            Constant = 0
                        };

                        // 匹配歌曲 → 修正 ID + 定数
                        var song = FindMatchingSong(score, SongDb, lxnsSongById, lxnsSongByTitle);
                        if (song != null)
                        {
                            score.Id = song.Id;
                            var li = (int)score.LevelIndex;
                            if (li >= 0 && li < song.Constants.Count)
                                score.Constant = (decimal)song.Constants[li];
                        }

                        lock (result)
                        {
                            result[(c.Id, c.LevelIdx)] = score;
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

    private async Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScoresViaOAuth(LxnsToken oauthToken)
    {
        var response = await "https://maimai.lxns.net/api/v0/user/chunithm/player/scores"
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

        // 可能嵌套在 "scores" 键内
        if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("scores", out var scArr))
            root = scArr;

        var lxnsSongs = GetSongList();
        var lxnsSongById = lxnsSongs.ToDictionary(s => s.Id, s => s);
        var lxnsSongByTitle = lxnsSongs.DistinctBy(s => s.Title).ToDictionary(s => s.Title, s => s);

        var result = new Dictionary<(long Id, int LevelIdx), ChunithmScore>();
        foreach (var s in root.EnumerateArray())
        {
            var lvlIdx = s.TryGetProperty("level_index", out var li) ? li.GetInt32() : -1;
            // 只收 MASTER / ULTIMA
            if (lvlIdx != 3 && lvlIdx != 4) continue;

            var score = new ChunithmScore
            {
                Id = s.GetProperty("id").GetInt32(),
                Title = s.GetProperty("song_name").GetString() ?? "",
                LevelIndex = lvlIdx,
                Level = s.TryGetProperty("level", out var lv) ? lv.GetString() ?? "" : "",
                Achievement = s.TryGetProperty("score", out var ach) ? ach.GetInt32() : 0,
                Fc = s.TryGetProperty("full_combo", out var fc) ? fc.GetString() ?? "" : "",
                Constant = 0
            };

            var song = FindMatchingSong(score, SongDb, lxnsSongById, lxnsSongByTitle);
            if (song != null)
            {
                score.Id = song.Id;
                if (lvlIdx >= 0 && lvlIdx < song.Constants.Count)
                    score.Constant = (decimal)song.Constants[lvlIdx];
            }

            result[(score.Id, lvlIdx)] = score;
        }

        return result;
    }

    private async Task<ChunithmRating> FetchScores(Message message)
    {
        var (_, qq) = AtOrSelf(message, true);

        var playerResponse = await $"{BaseUrl}/player/qq/{qq}"
            .WithHeader("Authorization", ConfigurationManager.Configuration.Lxns.DevToken)
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
        var playerName = data.GetProperty("name").GetString() ?? "";

        var response = await $"{BaseUrl}/player/{friendCode}/bests"
            .WithHeader("Authorization", ConfigurationManager.Configuration.Lxns.DevToken)
            .AllowHttpStatus("400,401,403,404")
            .GetAsync();

        if (response.StatusCode is 400 or 401 or 403 or 404)
        {
            var errorJson = await response.GetStringAsync();
            using var errorDoc = JsonDocument.Parse(errorJson);
            var errorMessage = errorDoc.RootElement.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? "Unknown error"
                : "Unknown error";
            throw new HttpRequestException($"[Lxns] {response.StatusCode}: {errorMessage}");
        }

        var jsonString = await response.GetStringAsync();
        using var doc = JsonDocument.Parse(jsonString);
        var root = doc.RootElement;

        var responseData = root.TryGetProperty("data", out var dataElement) ? dataElement : root;

        // 获取 Lxns 的歌曲列表用于匹配
        var lxnsSongs = GetSongList();
        var lxnsSongById = lxnsSongs.ToDictionary(s => s.Id, s => s);
        var lxnsSongByTitle = lxnsSongs.DistinctBy(s => s.Title).ToDictionary(s => s.Title, s => s);

        var bestScores = responseData.TryGetProperty("bests", out var bests)
            ? ParseScores(bests, SongDb, lxnsSongById, lxnsSongByTitle)
            : [];
        var recentScores = responseData.TryGetProperty("new_bests", out var newBests)
            ? ParseScores(newBests, SongDb, lxnsSongById, lxnsSongByTitle)
            : [];

        return new ChunithmRating
        {
            DataSource = "Lxns",
            Username = playerName,
            Records = new Records
            {
                Best = bestScores.OrderByDescending(x => x.Rating).Take(30).ToArray(),
                Recent = recentScores.OrderByDescending(x => x.Rating).Take(20).ToArray()
            }
        };
    }

    private static List<ChunithmScore> ParseScores(JsonElement scoresElement, SongDb<ChunithmSong> songDb, Dictionary<long, ChunithmSong> lxnsSongById, Dictionary<string, ChunithmSong> lxnsSongByTitle)
    {
        var scores = new List<ChunithmScore>();
        foreach (var s in scoresElement.EnumerateArray())
        {
            var score = new ChunithmScore
            {
                Id = s.GetProperty("id").GetInt32(),
                Title = s.GetProperty("song_name").GetString() ?? "",
                LevelIndex = s.GetProperty("level_index").GetInt32(),
                Level = s.GetProperty("level").GetString() ?? "",
                Achievement = s.GetProperty("score").GetInt32(),
                Fc = s.TryGetProperty("full_combo", out var fc) ? fc.GetString() ?? "" : "",
                Constant = 0
            };

            // 匹配歌曲
            var song = FindMatchingSong(score, songDb, lxnsSongById, lxnsSongByTitle);

            if (song != null)
            {
                score.Id = song.Id;
                var levelIdx = (int)score.LevelIndex;
                if (levelIdx >= 0 && levelIdx < song.Constants.Count)
                {
                    score.Constant = (decimal)song.Constants[levelIdx];
                }
            }

            // 不管有没有匹配到歌曲都添加（避免数据消失）
            scores.Add(score);
        }
        return scores;
    }

    private static ChunithmSong? FindMatchingSong(ChunithmScore score, SongDb<ChunithmSong> songDb, Dictionary<long, ChunithmSong> lxnsSongById, Dictionary<string, ChunithmSong> lxnsSongByTitle)
    {
        // 从 Lxns 歌曲列表按 ID 查找
        if (lxnsSongById.TryGetValue(score.Id, out var song))
        {
            return song;
        }

        // 从 Lxns 歌曲列表按标题查找
        if (lxnsSongByTitle.TryGetValue(score.Title, out song))
        {
            return song;
        }

        // 从本地 SongDb 按 ID 查找
        if (songDb.SongIndexer.TryGetValue(score.Id, out song))
        {
            return song;
        }

        // 从本地 SongDb 按标题查找
        song = songDb.SongList.FirstOrDefault(x => x.Title.Equals(score.Title, StringComparison.OrdinalIgnoreCase));
        if (song != null)
        {
            return song;
        }

        return null;
    }

    public void Reset()
    {
        _songList = null;
    }
}
