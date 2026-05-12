using System.Text.Json;
using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Interface;
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

        _songList = ((IEnumerable<dynamic>)response.songs)
            .Select(x => new ChunithmSong(x, ChunithmSong.DataSource.Lxns))
            .Where(x => !DeletedSongs.Contains(x.Id))
            .ToList();

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
        var scores = await FetchScores(message);

        // 合并 best 和 new_best，去重（按歌曲ID和难度），排序后取前30
        var allScores = scores.Records.Best
            .Concat(scores.Records.Recent)
            .GroupBy(x => new { x.Id, x.LevelIndex })
            .Select(g => g.OrderByDescending(x => x.Achievement).First())
            .OrderByDescending(x => x.Rating)
            .Take(30)
            .ToArray();

        scores.Records.Best = allScores;
        scores.Records.Recent = [];

        return scores;
    }

    public override Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScores(Message message)
    {
        throw new NotSupportedException("[Lxns] 不支持的操作");
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
        var lxnsSongByTitle = lxnsSongs.ToDictionary(s => s.Title, s => s);

        var bestScores = responseData.TryGetProperty("bests", out var bests)
            ? ParseScores(bests, SongDb, lxnsSongById, lxnsSongByTitle)
            : new List<ChunithmScore>();
        var recentScores = responseData.TryGetProperty("new_bests", out var newBests)
            ? ParseScores(newBests, SongDb, lxnsSongById, lxnsSongByTitle)
            : new List<ChunithmScore>();

        return new ChunithmRating
        {
            DataSource = "Lxns",
            Username = playerName,
            Records = new Records
            {
                Best = bestScores.ToArray(),
                Recent = recentScores.ToArray()
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
        // 1. 优先从本地 SongDb 按 ID 查找
        if (songDb.SongIndexer.TryGetValue(score.Id, out var song))
        {
            return song;
        }

        // 2. 从本地 SongDb 按标题查找
        song = songDb.SongList.FirstOrDefault(x => x.Title.Equals(score.Title, StringComparison.OrdinalIgnoreCase));
        if (song != null)
        {
            return song;
        }

        // 3. 从 Lxns 歌曲列表按 ID 查找
        if (lxnsSongById.TryGetValue(score.Id, out song))
        {
            return song;
        }

        // 4. 从 Lxns 歌曲列表按标题查找
        if (lxnsSongByTitle.TryGetValue(score.Title, out song))
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
