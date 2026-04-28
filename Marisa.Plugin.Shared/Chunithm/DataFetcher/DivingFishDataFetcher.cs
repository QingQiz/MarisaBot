using Flurl.Http;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

public class DivingFishDataFetcher(SongDb<ChunithmSong> songDb) : DataFetcher(songDb), ICanReset
{
    private static List<ChunithmSong>? _songList;

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
        var scores = await FetchScores(message, false);

        scores.Records.Best = scores.Records.Best.OrderByDescending(x => x.Rating).Take(30).ToArray();

        return scores;
    }

    public override async Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScores(Message message)
    {
        var scores = await FetchScores(message, true);

        return scores.Records.Best
            .Where(x => !DeletedSongs.Contains(x.Id))
            .ToDictionary(x => (x.Id, (int)x.LevelIndex), x => x);
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

        var json = await response.GetJsonAsync<ChunithmRating>();
        json.DataSource    = "DivingFish";
        json.Records.Best  = NormalizeRecords(json.Records.Best).Where(x => !DeletedSongs.Contains(x.Id)).ToArray();
        json.Records.Recent = NormalizeRecords(json.Records.Recent).ToArray();

        return json;
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

            var matchedSong = SongDb.SongList.FirstOrDefault(s => s.Title.Equals(record.Title, StringComparison.Ordinal));
            if (matchedSong == null) continue;

            record.Id = matchedSong.Id;
            yield return record;
        }
    }

    public void Reset()
    {
        _songList = null;
    }
}
