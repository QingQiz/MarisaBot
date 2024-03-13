using Flurl.Http;
using Marisa.Plugin.Shared.Chunithm;

namespace Marisa.Plugin.Chunithm;

public partial class Chunithm
{
    private static (string, long) AtOrSelf(Message message)
    {
        var username = message.Command;
        var qq       = message.Sender!.Id;

        if (string.IsNullOrWhiteSpace(username))
        {
            var at = message.MessageChain!.Messages.FirstOrDefault(m => m.Type == MessageDataType.At);
            if (at != null)
            {
                qq = (at as MessageDataAt)?.Target ?? qq;
            }
        }

        return (username, qq);
    }

    private List<ChunithmSong>? _songList;

    private IEnumerable<ChunithmSong> FilteredSongList
    {
        get
        {
            if (_songList != null) return _songList;

            var list = "https://www.diving-fish.com/api/chunithmprober/music_data"
                .GetJsonListAsync()
                .Result;

            _songList = list.Select(x => new ChunithmSong(x, true)).ToList();

            return _songList;
        }
    }

    private async Task<MessageChain> GetB30Card(Message message, bool b50 = false)
    {
        var (username, qq) = AtOrSelf(message);

        return MessageChain.FromImageB64((await GetRating(username, qq)).Draw().ToB64());
    }

    private async Task<ChunithmRating> GetRating(string? username, long? qq)
    {
        var response = await "https://www.diving-fish.com/api/maimaidxprober/chuni/query/player".PostJsonAsync(
            string.IsNullOrEmpty(username)
                ? new { qq }
                : new { username });
        var rating = ChunithmRating.FromJson(await response.GetStringAsync());

        foreach (var r in rating.Records.Best.Concat(rating.Records.R10))
        {
            if (_songDb.SongIndexer.ContainsKey(r.Id)) continue;

            r.Id = _songDb.SongList.First(s => s.Title == r.Title).Id;
        }

        return rating;
    }

    private async Task<Dictionary<(long Id, int LevelIdx), ChunithmScore>> GetAllSongScores(Message message)
    {
        var qq  = message.Sender!.Id;
        var ats = message.At().ToList();

        if (ats.Any())
        {
            qq = ats.First();
        }

        var response = await $"https://www.diving-fish.com/api/chunithmprober/dev/player/records?qq={qq}"
            .WithHeader("Developer-Token", ConfigurationManager.Configuration.Chunithm.DevToken)
            .GetJsonAsync<ChunithmRating>();

        return response.Records.Best.ToDictionary(x => (x.Id, (int)x.LevelIndex), x => x);
    }
}