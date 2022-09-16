using Newtonsoft.Json;

#pragma warning disable CS8618

namespace Marisa.Plugin.Shared.Osu.Entity.PPlus;

public class Accuracy
{
    [JsonProperty("SetID")]
    public long SetId { get; set; }

    [JsonProperty("Artist")]
    public string Artist { get; set; }

    [JsonProperty("Title")]
    public string Title { get; set; }

    [JsonProperty("Version")]
    public string Version { get; set; }

    [JsonProperty("MaxCombo")]
    public long MaxCombo { get; set; }

    [JsonProperty("UserID")]
    public long UserId { get; set; }

    [JsonProperty("BeatmapID")]
    public long BeatmapId { get; set; }

    [JsonProperty("Total")]
    public double Total { get; set; }

    [JsonProperty("Aim")]
    public double Aim { get; set; }

    [JsonProperty("JumpAim")]
    public double JumpAim { get; set; }

    [JsonProperty("FlowAim")]
    public double FlowAim { get; set; }

    [JsonProperty("Precision")]
    public double Precision { get; set; }

    [JsonProperty("Speed")]
    public double Speed { get; set; }

    [JsonProperty("Stamina")]
    public double Stamina { get; set; }

    [JsonProperty("HigherSpeed")]
    public double HigherSpeed { get; set; }

    [JsonProperty("Accuracy")]
    public double AccuracyAccuracy { get; set; }

    [JsonProperty("Count300")]
    public long Count300 { get; set; }

    [JsonProperty("Count100")]
    public long Count100 { get; set; }

    [JsonProperty("Count50")]
    public long Count50 { get; set; }

    [JsonProperty("Misses")]
    public long Misses { get; set; }

    [JsonProperty("AccuracyPercent")]
    public double AccuracyPercent { get; set; }

    [JsonProperty("Combo")]
    public long Combo { get; set; }

    [JsonProperty("EnabledMods")]
    public long EnabledMods { get; set; }

    [JsonProperty("Rank")]
    public Rank Rank { get; set; }

    [JsonProperty("Date")]
    public DateTimeOffset Date { get; set; }
}