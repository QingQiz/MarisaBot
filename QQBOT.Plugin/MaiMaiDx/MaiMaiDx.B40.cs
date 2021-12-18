using Flurl.Http;
using QQBot.Plugin.Shared.MaiMaiDx;

namespace QQBot.Plugin.MaiMaiDx
{
    public partial class MaiMaiDx
    {
        private static async Task<DxRating> MaiB40(object sender)
        {
            var response = await "https://www.diving-fish.com/api/maimaidxprober/query/player".PostJsonAsync(sender);
            var json     = await response.GetJsonAsync();

            var rating = new DxRating(json);

            return rating;
        }
    }
}