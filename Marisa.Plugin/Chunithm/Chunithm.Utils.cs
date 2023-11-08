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

    private static async Task<MessageChain> GetB30Card(Message message, bool b50 = false)
    {
        var (username, qq) = AtOrSelf(message);

        return MessageChain.FromImageB64((await GetRating(username, qq)).Draw().ToB64());
    }

    public static async Task<ChunithmRating> GetRating(string? username, long? qq)
    {
        var response = await "https://www.diving-fish.com/api/maimaidxprober/chuni/query/player".PostJsonAsync(
            string.IsNullOrEmpty(username)
                ? new { qq }
                : new { username });
        return ChunithmRating.FromJson(await response.GetStringAsync());
    }
}