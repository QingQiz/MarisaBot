#pragma warning disable CS8618
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.Score;

public class Statistics
{
    [JsonProperty("count_100", Required = Required.Always)]
    public int Count100 { get; set; }

    [JsonProperty("count_300", Required = Required.Always)]
    public int Count300 { get; set; }

    [JsonProperty("count_50", Required = Required.Always)]
    public int Count50 { get; set; }

    [JsonProperty("count_geki", Required = Required.Always)]
    public int Count300P { get; set; }

    [JsonProperty("count_katu", Required = Required.Always)]
    public int Count200 { get; set; }

    [JsonProperty("count_miss", Required = Required.Always)]
    public int CountMiss { get; set; }
}