#pragma warning disable CS8618
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.Score;

public class Statistics
{
    [JsonProperty("count_100", Required = Required.Always)]
    public long Count100 { get; set; }

    [JsonProperty("count_300", Required = Required.Always)]
    public long Count300 { get; set; }

    [JsonProperty("count_50", Required = Required.Always)]
    public long Count50 { get; set; }

    [JsonProperty("count_geki", Required = Required.Always)]
    public long CountGeki { get; set; }

    [JsonProperty("count_katu", Required = Required.Always)]
    public long CountKatu { get; set; }

    [JsonProperty("count_miss", Required = Required.Always)]
    public long CountMiss { get; set; }
}