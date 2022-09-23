#pragma warning disable CS8618
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.User;

public class Variant
{
    [JsonProperty("mode")]
    public string Mode { get; set; }

    [JsonProperty("variant")]
    public string Name { get; set; }

    [JsonProperty("country_rank")]
    public long? RegionRank { get; set; }

    [JsonProperty("global_rank")]
    public long? GlobalRank { get; set; }

    [JsonProperty("pp")]
    public double? Pp { get; set; }
}