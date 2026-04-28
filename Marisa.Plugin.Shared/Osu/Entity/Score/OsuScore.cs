#pragma warning disable CS8618
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Marisa.Plugin.Shared.Osu.Entity.Score;

public partial class OsuScore
{
    private long? _score;
    private string? _mode;
    private int? _modeInt;
    private bool _perfect;
    private bool _replay;
    private DateTimeOffset? _createdAt;

    [JsonProperty("weight")]
    public BpWeight? Weight { get; set; }

    [JsonProperty("accuracy", Required = Required.Always)]
    public double Accuracy { get; set; }

    [JsonProperty("best_id", Required = Required.AllowNull)]
    public long? BestId { get; set; }

    [JsonProperty("created_at", Required = Required.Default)]
    public DateTimeOffset CreatedAt
    {
        get
        {
            if (_createdAt is { } explicitCreatedAt && (explicitCreatedAt != default || EndedAt is null)) return explicitCreatedAt;
            return EndedAt ?? StartedAt ?? _createdAt ?? default;
        }
        set => _createdAt = value;
    }

    [JsonProperty("ended_at", Required = Required.Default)]
    public DateTimeOffset? EndedAt { get; set; }

    [JsonProperty("started_at", Required = Required.Default)]
    public DateTimeOffset? StartedAt { get; set; }

    [JsonProperty("id", Required = Required.Always)]
    public long Id { get; set; }

    [JsonProperty("max_combo", Required = Required.Always)]
    public int MaxCombo { get; set; }

    [JsonProperty("mode", Required = Required.Default)]
    public string Mode
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(_mode)) return _mode;

            var modeInt = RulesetId ?? _modeInt;
            return modeInt switch
            {
                0 => "osu",
                1 => "taiko",
                2 => "fruits",
                3 => "mania",
                _ => string.Empty
            };
        }
        set => _mode = value;
    }

    [JsonProperty("mode_int", Required = Required.Default)]
    public int ModeInt
    {
        get
        {
            if (_modeInt is { } explicitModeInt && (explicitModeInt != 0 || RulesetId is null or 0)) return explicitModeInt;
            return RulesetId ?? _modeInt ?? 0;
        }
        set => _modeInt = value;
    }

    [JsonProperty("ruleset_id", Required = Required.Default)]
    public int? RulesetId { get; set; }

    [JsonProperty("mods", Required = Required.Always)]
    [JsonConverter(typeof(OsuModArrayConverter))]
    public string[] Mods { get; set; }

    [JsonProperty("passed", Required = Required.Always)]
    public bool Passed { get; set; }

    [JsonProperty("perfect", Required = Required.Default)]
    public bool Perfect
    {
        get => _perfect || LegacyPerfect || IsPerfectCombo;
        set => _perfect = value;
    }

    [JsonProperty("legacy_perfect", Required = Required.Default)]
    public bool LegacyPerfect { get; set; }

    [JsonProperty("is_perfect_combo", Required = Required.Default)]
    public bool IsPerfectCombo { get; set; }

    [JsonProperty("pp", Required = Required.AllowNull)]
    public double? Pp { get; set; }

    [JsonProperty("rank", Required = Required.Always)]
    public string Rank { get; set; }

    [JsonProperty("replay", Required = Required.Default)]
    public bool Replay
    {
        get => _replay || HasReplay;
        set => _replay = value;
    }

    [JsonProperty("has_replay", Required = Required.Default)]
    public bool HasReplay { get; set; }

    [JsonProperty("score", Required = Required.Default)]
    public long Score
    {
        get
        {
            if (_score is { } explicitScore && explicitScore != 0) return explicitScore;
            if (LegacyTotalScore is > 0) return LegacyTotalScore.Value;
            if (ClassicTotalScore is > 0) return ClassicTotalScore.Value;
            if (TotalScore is > 0) return TotalScore.Value;
            return _score ?? TotalScore ?? 0;
        }
        set => _score = value;
    }

    [JsonProperty("classic_total_score", Required = Required.Default)]
    public long? ClassicTotalScore { get; set; }

    [JsonProperty("legacy_total_score", Required = Required.Default)]
    public long? LegacyTotalScore { get; set; }

    [JsonProperty("total_score", Required = Required.Default)]
    public long? TotalScore { get; set; }

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

    public bool ShouldSerializeClassicTotalScore() => false;

    public bool ShouldSerializeLegacyTotalScore() => false;

    public bool ShouldSerializeRulesetId() => false;

    public bool ShouldSerializeEndedAt() => false;

    public bool ShouldSerializeStartedAt() => false;

    public bool ShouldSerializeLegacyPerfect() => false;

    public bool ShouldSerializeIsPerfectCombo() => false;

    public bool ShouldSerializeHasReplay() => false;

    public bool ShouldSerializeTotalScore() => false;
}

internal class OsuModArrayConverter : JsonConverter<string[]>
{
    public override string[]? ReadJson(JsonReader reader, Type objectType, string[]? existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return [];

        var token = Newtonsoft.Json.Linq.JToken.Load(reader);
        if (token.Type != Newtonsoft.Json.Linq.JTokenType.Array) return [];

        return token
            .Children()
            .Select(item => item.Type switch
            {
                Newtonsoft.Json.Linq.JTokenType.String => item.ToObject<string>(),
                Newtonsoft.Json.Linq.JTokenType.Object => item["acronym"]?.ToObject<string>(),
                _ => null
            })
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .ToArray();
    }

    public override void WriteJson(JsonWriter writer, string[]? value, JsonSerializer serializer)
    {
        serializer.Serialize(writer, value ?? []);
    }
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

    [JsonIgnore]
    public double PpAccuracy => ModeInt == 3
        ? (320 * Statistics.Count300P + 300 * Statistics.Count300 + 200 * Statistics.Count200 + 100 * Statistics.Count100 + 50 * Statistics.Count50) /
          (double)(320 * (Statistics.Count300P + Statistics.Count300 + Statistics.Count200 + Statistics.Count100 + Statistics.Count50 + Statistics.CountMiss))
        : throw new NotSupportedException("Only MANIA is supported");
}