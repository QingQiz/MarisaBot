#pragma warning disable CS8618
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.Score;

public class CurrentUserAttributes
{
    [JsonProperty("pin")]
    public object? Pin { get; set; }
}