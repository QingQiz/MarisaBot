using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.User;

public class MonthlyPlayCount
{
    [JsonProperty("start_date")]
    public DateTimeOffset StartDate { get; set; }

    [JsonProperty("count")]
    public long Count { get; set; }
}