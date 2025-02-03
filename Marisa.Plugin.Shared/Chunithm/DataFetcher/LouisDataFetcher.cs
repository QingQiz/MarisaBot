using Flurl.Http;
using Marisa.Plugin.Shared.Chunithm.DataFetcher.Entities;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json.Linq;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

public class LouisDataFetcher(SongDb<ChunithmSong> songDb) : DataFetcher(songDb), ICanReset
{
    private const string Uri = "http://43.139.107.206:8998/api";
    private static string MusicListUri => $"{Uri}/resource/chunithm/song-list";
    private static string RatingUri => $"{Uri}/open/chunithm/basic-info";
    private static string ScoresUri => $"{Uri}/open/chunithm/filtered-info";
    private static string Token => ConfigurationManager.Configuration.Chunithm.TokenLouis;
    private static List<ChunithmSong>? _songList;
    private readonly object _songListLocker = new();

    public override List<ChunithmSong> GetSongList()
    {
        lock (_songListLocker)
        {
            if (_songList != null) return _songList;

            var list = MusicListUri
                .GetJsonListAsync()
                .Result
                .Select(x => new ChunithmSong(x, ChunithmSong.DataSource.Louis));

            return _songList = list.Where(x => !DeletedSongs.Contains(x.Id)).ToList();
        }
    }

    public override async Task<ChunithmRating> GetRating(Message message)
    {
        var (username, qq) = AtOrSelf(message);

        return await ReqRating(username, qq);
    }

    public override async Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScores(Message message)
    {
        var (username, qq) = AtOrSelf(message, true);

        return await ReqScores(username.IsWhiteSpace()
            ? new { qq, constant       = "0-16" }
            : new { username, constant = "0-16" });
    }

    public async Task<ChunithmRating> ReqRating(ReadOnlyMemory<char> username, long qq)
    {
        var response = await RatingUri
            .AllowHttpStatus("400")
            .WithOAuthBearerToken(Token)
            .PostJsonAsync(username.IsWhiteSpace()
                ? new { qq }
                : new { username });

        if (response.StatusCode == 400)
        {
            var rep = await response.GetJsonAsync();
            throw new HttpRequestException(HttpRequestError.Unknown, "[Louis] 400: " + rep.message);
        }

        var json = JObject.Parse(await response.GetStringAsync());
        var data = new ChunithmRating
        {
            Username = json["nickname"]?.Value<string>() ?? "",
            Records = new Records
            {
#pragma warning disable CS8602
#pragma warning disable CS8604
                B30 = json["records"]["b30"].ToObject<BestScoreLouis[]>()
                    .Select(x => x.ToChunithmScore(SongDb)).ToArray(),
                R10 = json["records"]["r10"].ToObject<RecentScoreLouis[]>()
                    .Select(x => x.ToChunithmScore(SongDb)).ToArray()
#pragma warning restore CS8604
#pragma warning restore CS8602
            }
        };

        foreach (var r in data.Records.Best.Concat(data.Records.R10))
        {
            if (SongDb.SongIndexer.ContainsKey(r.Id)) continue;

            r.Id = SongDb.SongList.First(s => s.Title.Equals(r.Title, StringComparison.Ordinal)).Id;
        }

        data.Records.Best = data.Records.Best.Where(x => !DeletedSongs.Contains(x.Id)).ToArray();

        return data;
    }

    public async Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> ReqScores(object req)
    {
        var response = await ScoresUri
            .AllowHttpStatus("400")
            .WithOAuthBearerToken(Token)
            .PostJsonAsync(req);

        if (response.StatusCode == 400)
        {
            var rep = await response.GetJsonAsync();
            throw new HttpRequestException(HttpRequestError.Unknown, "[Louis] 400: " + rep.message);
        }

        var json = await response.GetJsonAsync<BestScoreLouis[]>();

        return json.Select(x => x.ToChunithmScore(SongDb))
            .DistinctBy(x => (x.Id, (int)x.LevelIndex))
            .ToDictionary(x => (x.Id, (int)x.LevelIndex), x => x);
    }

    public void Reset()
    {
        lock (_songListLocker)
        {
            _songList = null;
        }
    }
}