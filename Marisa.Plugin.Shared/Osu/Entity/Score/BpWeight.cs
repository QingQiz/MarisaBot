using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.Score;

public class BpWeight
{
    [JsonProperty("percentage")]
    public double Percentage { get; set; }
    
    [JsonProperty("pp")]
    public double Pp { get; set; }
}