#pragma warning disable CS8618
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.User;

public class Rank
{
    [JsonProperty("country")]
    public long Region { get; set; }
}