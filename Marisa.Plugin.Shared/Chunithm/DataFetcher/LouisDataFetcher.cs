using Flurl.Http;
using Marisa.Plugin.Shared.Chunithm.DataFetcher.Entities;
using Marisa.Configuration;
using Marisa.Plugin.Shared.Interface;
using Marisa.Plugin.Shared.Util;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json.Linq;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

using IndexerT = Dictionary<long, ChunithmSong>;

public class LouisDataFetcher(SongDb<ChunithmSong> songDb) : DataFetcher(songDb), ICanReset
{
    private const string Uri = "http://43.139.107.206:8998/api";
    private static string RatingUri => $"{Uri}/open/chunithm/basic-info";
    private static string ScoresUri => $"{Uri}/open/chunithm/filtered-info";
    private static string SongListUri => $"{Uri}/resource/chunithm/song-list";
    private static string Token => ConfigurationManager.Configuration.Chunithm.TokenLouis;

    private static List<ChunithmSong>? _cachedSongList;
    private IndexerT? _indexer;

    private IndexerT Indexer => _indexer ??= GetSongList().ToDictionary(s => s.Id);

    public override List<ChunithmSong> GetSongList()
    {
        if (_cachedSongList != null) return _cachedSongList;

        try
        {
            var data = SongListUri.GetJsonListAsync().Result;

            _cachedSongList = data.Select(d => new ChunithmSong(d, ChunithmSong.DataSource.Louis)).ToList();

            return _cachedSongList;
        }
        catch
        {
            return base.GetSongList();
        }
    }

    public override async Task<ChunithmRating> GetRating(Message message)
    {
        var (username, qq) = AtOrSelf(message);
        var scores = await ReqScores(username.IsWhiteSpace()
            ? new { qq, constant = "0-16" }
            : new { username, constant = "0-16" });

        var songList = GetSongList();
        var versionMap = songList.ToDictionary(s => s.Id, s => s.Version);

        var newest = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CHUNITHM LUMINOUS PLUS", "CHUNITHM VERSE"
        };

        var div = scores.Values
            .GroupBy(x => newest.Contains(versionMap.GetValueOrDefault(x.Id, "")))
            .ToList();

        return new ChunithmRating
        {
            DataSource = "Louis",
            Username = username.IsWhiteSpace() ? "" : username.ToString(),
            Records = new Records
            {
                Best = div.FirstOrDefault(x => !x.Key)?.OrderByDescending(x => x.Rating).Take(30).ToArray() ?? [],
                Recent = div.FirstOrDefault(x => x.Key)?.OrderByDescending(x => x.Rating).Take(20).ToArray() ?? []
            }
        };
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
            Username = json["nickname"]?.Value<string>()?.ToHalfWidth() ?? "",
            Records = new Records
            {
#pragma warning disable CS8602
#pragma warning disable CS8604
                B30 = json["records"]["b30"].ToObject<BestScoreLouis[]>()
                    .Select(x => x.ToChunithmScore(Indexer)).ToArray(),
                Recent = json["records"]["r10"].ToObject<RecentScoreLouis[]>()
                    .Select(x => x.ToChunithmScore(Indexer)).ToArray()
#pragma warning restore CS8604
#pragma warning restore CS8602
            },
            DataSource = "Louis"
        };

        foreach (var r in data.Records.Best.Concat(data.Records.Recent))
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

        return json.Select(x => x.ToChunithmScore(Indexer))
            .DistinctBy(x => (x.Id, (int)x.LevelIndex))
            .ToDictionary(x => (x.Id, (int)x.LevelIndex), x => x);
    }

    public void Reset()
    {
        _cachedSongList = null;
        _indexer = null;
    }
}
