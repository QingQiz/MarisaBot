using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.User;

public class Level
{
    [JsonProperty("current")]
    public int Current { get; set; }

    [JsonProperty("progress")]
    public int Progress { get; set; }
}