using Newtonsoft.Json;

#pragma warning disable CS8618

namespace Marisa.Plugin.Shared.Osu.Entity.PPlus;

public class UserData
{
    [JsonProperty("Rank")]
    public long Rank { get; set; }

    [JsonProperty("CountryRank")]
    public long CountryRank { get; set; }

    [JsonProperty("UserID")]
    public long UserId { get; set; }

    [JsonProperty("UserName")]
    public string UserName { get; set; }

    [JsonProperty("CountryCode")]
    public string CountryCode { get; set; }

    [JsonProperty("PerformanceTotal")]
    public double PerformanceTotal { get; set; }

    [JsonProperty("AimTotal")]
    public double AimTotal { get; set; }

    [JsonProperty("JumpAimTotal")]
    public double JumpAimTotal { get; set; }

    [JsonProperty("FlowAimTotal")]
    public double FlowAimTotal { get; set; }

    [JsonProperty("PrecisionTotal")]
    public double PrecisionTotal { get; set; }

    [JsonProperty("SpeedTotal")]
    public double SpeedTotal { get; set; }

    [JsonProperty("StaminaTotal")]
    public double StaminaTotal { get; set; }

    [JsonProperty("AccuracyTotal")]
    public double AccuracyTotal { get; set; }

    [JsonProperty("AccuracyPercentTotal")]
    public double AccuracyPercentTotal { get; set; }

    [JsonProperty("PlayCount")]
    public long PlayCount { get; set; }

    [JsonProperty("CountRankSS")]
    public long CountRankSs { get; set; }

    [JsonProperty("CountRankS")]
    public long CountRankS { get; set; }
}