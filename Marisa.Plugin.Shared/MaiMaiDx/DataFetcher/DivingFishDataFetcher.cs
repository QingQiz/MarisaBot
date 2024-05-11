using Flurl.Http;
using Marisa.BotDriver.Entity.Message;
using Marisa.BotDriver.Entity.MessageData;
using Marisa.EntityFrameworkCore.Entity.Plugin.MaiMaiDx;
using Marisa.Plugin.Shared.Util.SongDb;
using Newtonsoft.Json.Linq;

namespace Marisa.Plugin.Shared.MaiMaiDx.DataFetcher;

using MaiSongDb = SongDb<MaiMaiSong, MaiMaiDxGuess>;

public class DivingFishDataFetcher : DataFetcher
{
    public DivingFishDataFetcher(MaiSongDb songDb) : base(songDb) {}

    public override async Task<DxRating> GetRating(Message message)
    {
        var (username, qq) = AtOrSelf(message);

        var rep = await "https://www.diving-fish.com/api/maimaidxprober/query/player".PostJsonAsync(
            string.IsNullOrEmpty(username)
                ? new { qq, b50       = true }
                : new { username, b50 = true });
        return DxRating.FromJson(await rep.GetStringAsync());
    }

    public override async Task<Dictionary<(long Id, int LevelIdx), SongScore>> GetScores(Message message)
    {
        var (_, qq) = AtOrSelf(message);

        var response = await "https://www.diving-fish.com/api/maimaidxprober/query/plate".PostJsonAsync(new
        {
            qq, version = MaiMaiSong.Plates
        });

        var res = await response.GetStringAsync();

        return JObject.Parse(res).SelectToken("$.verlist")!
            .Select(x => x.ToObject<SongScore>())
            .ToDictionary(ss => (ss!.Id, ss.LevelIdx))!;
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