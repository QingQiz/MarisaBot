#pragma warning disable CS8618
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Marisa.Plugin.Shared.Osu.Entity.Score;

public partial class OsuScore
{
    [JsonProperty("weight")]
    public BpWeight? Weight { get; set; }

    [JsonProperty("accuracy", Required = Required.Always)]
    public double Accuracy { get; set; }

    [JsonProperty("best_id", Required = Required.AllowNull)]
    public long? BestId { get; set; }

    [JsonProperty("created_at", Required = Required.Always)]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonProperty("id", Required = Required.Always)]
    public long Id { get; set; }

    [JsonProperty("max_combo", Required = Required.Always)]
    public long MaxCombo { get; set; }

    [JsonProperty("mode", Required = Required.Always)]
    public string Mode { get; set; }

    [JsonProperty("mode_int", Required = Required.Always)]
    public int ModeInt { get; set; }

    [JsonProperty("mods", Required = Required.Always)]
    public string[] Mods { get; set; }

    [JsonProperty("passed", Required = Required.Always)]
    public bool Passed { get; set; }

    [JsonProperty("perfect", Required = Required.Always)]
    public bool Perfect { get; set; }

    [JsonProperty("pp", Required = Required.AllowNull)]
    public double? Pp { get; set; }

    [JsonProperty("rank", Required = Required.Always)]
    public string Rank { get; set; }

    [JsonProperty("replay", Required = Required.Always)]
    public bool Replay { get; set; }

    [JsonProperty("score", Required = Required.Always)]
    public long Score { get; set; }

    [JsonProperty("statistics", Required = Required.Always)]
    public Statistics Statistics { get; set; }

    [JsonProperty("user_id", Required = Required.Always)]
    public long UserId { get; set; }

    [JsonProperty("current_user_attributes", Required = Required.Always)]
    public CurrentUserAttributes CurrentUserAttributes { get; set; }

    [JsonProperty("beatmap", Required = Required.Always)]
    public Beatmap Beatmap { get; set; }

    [JsonProperty("beatmapset", Required = Required.Always)]
    public Beatmapset Beatmapset { get; set; }

    [JsonProperty("user", Required = Required.Always)]
    public User User { get; set; }
}

public partial class OsuScore
{
    internal static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new()
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling        = DateParseHandling.None,
            NullValueHandling        = NullValueHandling.Ignore,
            DefaultValueHandling     = DefaultValueHandling.Populate,
            Converters =
            {
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    public static OsuScore[]? FromJson(string json) =>
        JsonConvert.DeserializeObject<OsuScore[]>(json, Converter.Settings);

    public string ToJson() => JsonConvert.SerializeObject(this, Converter.Settings);
}