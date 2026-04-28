using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

public class DivingFishDataFetcher(SongDb<ChunithmSong> songDb) : DataFetcher(songDb), ICanReset
{
    private static List<ChunithmSong>? _songList;
    private Dictionary<string, ChunithmSong>? _songTitleIndexer;

    private Dictionary<string, ChunithmSong> SongTitleIndexer => _songTitleIndexer ??= SongDb.SongList
        .GroupBy(song => song.Title, StringComparer.Ordinal)
        .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

    public override List<ChunithmSong> GetSongList()
    {
        if (_songList != null) return _songList;

        var list = "https://www.diving-fish.com/api/chunithmprober/music_data"
            .GetJsonListAsync()
            .Result;

        _songList = list.Select(x => new ChunithmSong(x, ChunithmSong.DataSource.DivingFish))
            .Where(x => !DeletedSongs.Contains(x.Id))
            .ToList();

        return _songList;
    }

    public override async Task<ChunithmRating> GetRating(Message message)
    {
        var scores = await GetScoresCore(message, false);

        scores.Records.Best = scores.Records.Best.OrderByDescending(x => x.Rating).Take(30).ToArray();

        return scores;
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

    protected virtual async Task<ChunithmRating> FetchScores(Message message, bool qqOnly)
    {
        var (username, qq) = AtOrSelf(message, qqOnly);

        var uri = username.IsWhiteSpace()
            ? $"https://www.diving-fish.com/api/chunithmprober/dev/player/records?qq={qq}"
            : $"https://www.diving-fish.com/api/chunithmprober/dev/player/records?username={username}";

        var response = await uri
            .WithHeader("Developer-Token", ConfigurationManager.Configuration.DivingFish.DevToken)
            .AllowHttpStatus("403")
            .GetAsync();

        if (response.StatusCode == 403)
        {
            var rep = await response.GetJsonAsync();
            throw new HttpRequestException(HttpRequestError.Unknown, "[DivingFish] 403: " + rep.message);
        }

        return await response.GetJsonAsync<ChunithmRating>();
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
        _songList         = null;
        _songTitleIndexer = null;
    }
}
