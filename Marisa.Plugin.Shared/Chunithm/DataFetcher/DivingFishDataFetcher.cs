﻿using Flurl.Http;
using Marisa.Plugin.Shared.Configuration;
using Marisa.Plugin.Shared.Util.SongDb;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

public class DivingFishDataFetcher(SongDb<ChunithmSong> songDb) : DataFetcher(songDb)
{
    /// <summary>
    ///     中二节奏有一些如删的歌曲，即这些歌在游戏中已经删除，但在公众号中依然被保留，
    ///     这导致了op计算和rating计算不正确，
    ///     因此需要手动过滤掉
    /// </summary>
    private readonly HashSet<long> _deletedSongs =
    [
        1051, 1001, 1003, 1046, 1049, 1050, 1054, 2007, 2008, 2014, 2016, 2020, 2095, 343, 156
    ];
    private List<ChunithmSong>? _songList;

    public override List<ChunithmSong> GetSongList()
    {
        if (_songList != null) return _songList;

        var list = "https://www.diving-fish.com/api/chunithmprober/music_data"
            .GetJsonListAsync()
            .Result;

        _songList = list.Select(x => new ChunithmSong(x, true))
            .Where(x => !_deletedSongs.Contains(x.Id))
            .ToList();

        return _songList;
    }

    public override async Task<ChunithmRating> GetRating(Message message)
    {
        var scores = await GetScores(message, false);

        scores.Records.Best = scores.Records.Best.OrderByDescending(x => x.Rating).Take(30).ToArray();

        return scores;
    }

    public override async Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScores(Message message)
    {
        var scores = await GetScores(message, true);

        return scores.Records.Best
            .Where(x => !_deletedSongs.Contains(x.Id))
            .ToDictionary(x => (x.Id, (int)x.LevelIndex), x => x);
    }

    private async Task<ChunithmRating> GetScores(Message message, bool qqOnly)
    {
        var (username, qq) = AtOrSelf(message, qqOnly);

        var uri = username.IsWhiteSpace()
            ? $"https://www.diving-fish.com/api/chunithmprober/dev/player/records?qq={qq}"
            : $"https://www.diving-fish.com/api/chunithmprober/dev/player/records?username={username}";

        var response = await uri
            .WithHeader("Developer-Token", ConfigurationManager.Configuration.Chunithm.DevToken)
            .GetJsonAsync<ChunithmRating>();

        foreach (var r in response.Records.Best.Concat(response.Records.R10))
        {
            if (SongDb.SongIndexer.ContainsKey(r.Id)) continue;

            r.Id = SongDb.SongList.First(s => s.Title.Equals(r.Title, StringComparison.Ordinal)).Id;
        }

        response.Records.Best = response.Records.Best.Where(x => !_deletedSongs.Contains(x.Id)).ToArray();

        return response;
    }

    private static (ReadOnlyMemory<char>, long) AtOrSelf(Message message, bool qqOnly = false)
    {
        var username = message.Command;
        var qq       = message.Sender.Id;

        if (qqOnly) username = "".AsMemory();

        if (username.IsEmpty) return (username, qq);

        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);
        if (at != null)
        {
            qq = (at as MessageDataAt)?.Target ?? qq;
        }

        return (username, qq);
    }
}