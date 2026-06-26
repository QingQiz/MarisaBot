using System.Text.Json;
using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Lxns;
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

public class LxnsDataFetcher(SongDb<ChunithmSong> songDb) : DataFetcher(songDb), ICanReset
{
    private const string BaseUrl = "https://maimai.lxns.net/api/v0/chunithm";
    private static List<ChunithmSong>? _songList;

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

        // 无 OAuth token → 引导用户绑定
        throw new HttpRequestException("[Lxns] 请先使用 bind → 选择 lxns 完成 OAuth 授权后再试");

        // === 以下 dev token 两阶段抓取已废弃 ===
        /*
        // 回落 dev token 两阶段抓取
        var token = ConfigurationManager.Configuration.Lxns.DevToken;
        ...
        */
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
            if (lvlIdx < 0) continue;

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
            var body = await playerResponse.GetStringAsync();
            throw new HttpRequestException(ProberError.Lxns(playerResponse.StatusCode, body));
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
            throw new HttpRequestException(ProberError.Lxns(response.StatusCode, body));
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
