#pragma warning disable CS8618
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.User;

public class Page
{
    [JsonProperty("html")]
    public string Html { get; set; }

    [JsonProperty("raw")]
    public string Raw { get; set; }
}