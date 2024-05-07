using Flurl.Http;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.Plugin.Shared.Configuration;

namespace Marisa.Plugin.Shared.Chunithm.DataFetcher;

using ChunithmSongDb =
    Util.SongDb.SongDb<ChunithmSong, Marisa.EntityFrameworkCore.Entity.Plugin.Chunithm.ChunithmGuess>;

public class DivingFishDataFetcher : DataFetcher
{
    public DivingFishDataFetcher(ChunithmSongDb songDb) : base(songDb) {}

    private List<ChunithmSong>? _songList;

    public override List<ChunithmSong> GetSongList()
    {
        if (_songList != null) return _songList;

        var list = "https://www.diving-fish.com/api/chunithmprober/music_data"
            .GetJsonListAsync()
            .Result;

        _songList = list.Select(x => new ChunithmSong(x, true)).ToList();

        return _songList;
    }

    public override async Task<ChunithmRating> GetRating(Message message)
    {
        var (username, qq) = AtOrSelf(message);

        var response = await "https://www.diving-fish.com/api/maimaidxprober/chuni/query/player".PostJsonAsync(
            string.IsNullOrEmpty(username)
                ? new { qq }
                : new { username });
        var rating = ChunithmRating.FromJson(await response.GetStringAsync());

        foreach (var r in rating.Records.Best.Concat(rating.Records.R10))
        {
            if (SongDb.SongIndexer.ContainsKey(r.Id)) continue;

            r.Id = SongDb.SongList.First(s => s.Title == r.Title).Id;
        }

        return rating;
    }

    public override async Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetScores(Message message)
    {
        var (_, qq) = AtOrSelf(message, true);

        var response = await $"https://www.diving-fish.com/api/chunithmprober/dev/player/records?qq={qq}"
            .WithHeader("Developer-Token", ConfigurationManager.Configuration.Chunithm.DevToken)
            .GetJsonAsync<ChunithmRating>();

        return response.Records.Best.ToDictionary(x => (x.Id, (int)x.LevelIndex), x => x);
    }

    private static (string, long) AtOrSelf(Message message, bool qqOnly = false)
    {
        var username = message.Command;
        var qq       = message.Sender!.Id;

        if (qqOnly) username = "";

        if (!string.IsNullOrWhiteSpace(username)) return (username, qq);

        var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);
        if (at != null)
        {
            qq = (at as MessageDataAt)?.Target ?? qq;
        }

        return (username, qq);
    }
}