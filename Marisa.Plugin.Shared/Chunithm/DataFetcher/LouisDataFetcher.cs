﻿using Flurl.Http;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

public class LouisDataFetcher(SongDb<ChunithmSong> songDb) : DataFetcher(songDb)
{
    private static List<ChunithmSong>? _songList;

    public override List<ChunithmSong> GetSongList()
    {
        if (_songList != null) return _songList;

        var list = "http://43.139.107.206:8083/api/chunithm/music_data"
            .GetJsonListAsync()
            .Result
            .Select(x => new ChunithmSong(x, ChunithmSong.DataSource.Louis));

        return _songList = list.Where(x => !DeletedSongs.Contains(x.Id)).ToList();
    }

    public override async Task<ChunithmRating> GetRating(Message message)
    {
        var (username, qq) = AtOrSelf(message);

        var response = await "http://43.139.107.206:8083/api/chunithm/basic_info"
            .AllowHttpStatus("400")
            .PostJsonAsync(username.IsWhiteSpace()
                ? new { qq }
                : new { username });

        if (response.StatusCode == 400)
        {
            var rep = await response.GetJsonAsync();
            throw new HttpRequestException(HttpRequestError.Unknown, "[Louis] 400: " + rep.message);
        }

        var json = await response.GetJsonAsync<ChunithmRating>();
        foreach (var r in json.Records.Best.Concat(json.Records.R10))
        {
            if (SongDb.SongIndexer.ContainsKey(r.Id)) continue;

            r.Id = SongDb.SongList.First(s => s.Title.Equals(r.Title, StringComparison.Ordinal)).Id;
        }

        json.Records.Best = json.Records.Best.Where(x => !DeletedSongs.Contains(x.Id)).ToArray();

        return json;
    }

    public override async Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScores(Message message)
    {
        var (username, qq) = AtOrSelf(message, true);

        return await ReqScores(username.IsWhiteSpace()
            ? new { qq, constant       = "0-16" }
            : new { username, constant = "0-16" });
    }

    public async Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> ReqScores(object req)
    {
        var response = await "http://43.139.107.206:8083/api/chunithm/filtered_info"
            .AllowHttpStatus("400")
            .PostJsonAsync(req);

        if (response.StatusCode == 400)
        {
            var rep = await response.GetJsonAsync();
            throw new HttpRequestException(HttpRequestError.Unknown, "[Louis] 400: " + rep.message);
        }

        // EXAMPLE RESPONSE:
        // [{
        //      "id":69,
        //      "title":"",
        //      "level_index":3,
        //      "highscore":1006508,
        //      "rank_index":11,
        //      "clear":"clear",
        //      "full_combo":"",
        //      "full_chain":"",
        //      "idx":"2140",
        //      "createdAt":"2023-05-06 12:56:31.680 +00:00",
        //      "updatedAt":"2023-05-06 12:56:31.680 +00:00"
        // }]
        var json = await response.GetJsonListAsync();

        var data = json.Select(x =>
        {
            ChunithmSong song  = SongDb.SongIndexer[long.Parse(x.idx)];
            var          level = (int)x.level_index;
            return new ChunithmScore
            {
                CId         = 0,
                Constant    = (decimal)song.Constants[level],
                Fc          = x.full_combo == "alljustice" ? x.full_combo : x.full_chain != "" ? x.full_chain : x.full_combo,
                Level       = song.LevelName[level],
                LevelIndex  = level,
                LevelLabel  = ChunithmSong.LevelLabel[level],
                Id          = song.Id,
                Achievement = (int)x.highscore,
                Title       = song.Title
            };
        });

        return data
            .DistinctBy(x => (x.Id, (int)x.LevelIndex))
            .ToDictionary(x => (x.Id, (int)x.LevelIndex), x => x);
    }
}