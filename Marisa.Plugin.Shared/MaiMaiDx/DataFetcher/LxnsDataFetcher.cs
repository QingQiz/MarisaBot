using System.Text.Json;
using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

public class LxnsDataFetcher(SongDb<MaiMaiSong> songDb) : DataFetcher(songDb)
{
    private const string BaseUrl = "https://maimai.lxns.net/api/v0/maimai";

    public override async Task<DxRating> GetRating(Message message)
    {
        return await FetchScores(message);
    }

    public override Task<Dictionary<(long Id, int LevelIdx), SongScore>> GetScores(Message message)
    {
        throw new NotSupportedException("[Lxns] 落雪不支持获取所有分数，该功能已禁用");
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
            var errorJson = await playerResponse.GetStringAsync();
            using var errorDoc = JsonDocument.Parse(errorJson);
            var errorMessage = errorDoc.RootElement.TryGetProperty("message", out var msg)
                ? msg.GetString() ?? "Player not found"
                : "Player not found";
            throw new HttpRequestException(HttpRequestError.Unknown, $"[Lxns] {playerResponse.StatusCode}: {errorMessage}");
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
            throw new HttpRequestException(HttpRequestError.Unknown, $"[Lxns] {response.StatusCode}: {errorJson}");
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
