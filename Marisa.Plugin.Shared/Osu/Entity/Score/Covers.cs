#pragma warning disable CS8618
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.Score;

public class Covers
{
    [JsonProperty("cover", Required = Required.Always)]
    public Uri Cover { get; set; }

    [JsonProperty("cover@2x", Required = Required.Always)]
    public Uri Cover2X { get; set; }

    [JsonProperty("card", Required = Required.Always)]
    public Uri Card { get; set; }

    [JsonProperty("card@2x", Required = Required.Always)]
    public Uri Card2X { get; set; }

    [JsonProperty("list", Required = Required.Always)]
    public string List { get; set; }

    [JsonProperty("list@2x", Required = Required.Always)]
    public Uri List2X { get; set; }

    [JsonProperty("slimcover", Required = Required.Always)]
    public Uri Slimcover { get; set; }

    [JsonProperty("slimcover@2x", Required = Required.Always)]
    public Uri Slimcover2X { get; set; }
}