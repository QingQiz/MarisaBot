using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.User;

public class Kudosu
{
    [JsonProperty("total")]
    public long Total { get; set; }

    [JsonProperty("available")]
    public long Available { get; set; }
}