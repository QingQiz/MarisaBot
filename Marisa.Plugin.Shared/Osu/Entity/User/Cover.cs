#pragma warning disable CS8618
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.User;

public class Cover
{
    [JsonProperty("custom_url")]
    public string? CustomUrl { get; set; }

    [JsonProperty("url")]
    public Uri Url { get; set; }

    [JsonProperty("id")]
    public long? Id { get; set; }
}