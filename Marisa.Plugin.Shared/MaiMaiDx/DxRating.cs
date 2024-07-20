using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

namespace Marisa.Plugin.Shared.MaiMaiDx;

public class DxRating
{
    private class Charts
    {
        [JsonProperty("dx")]
        public List<SongScore> NewScores { get; set; }

        [JsonProperty("sd")]
        public List<SongScore> OldScores { get; set; }
    }

    [JsonProperty("charts")]
    private Charts _charts { get; set; } = new();

    [JsonIgnore]
    public List<SongScore> NewScores
    {
        get => _charts.NewScores;
        set => _charts.NewScores = value;
    }

    [JsonIgnore]
    public List<SongScore> OldScores
    {
        get => _charts.OldScores;
        init => _charts.OldScores = value;
    }

    [JsonProperty("nickname")]
    public string Nickname { get; set; }

    [JsonProperty("rating")]
    public int Rating
    {
        get => NewScores.Sum(x => x.Rating) + OldScores.Sum(x => x.Rating);
        // ReSharper disable once ValueParameterNotUsed
        set {}
    }

    public static DxRating FromJson(string json) => JsonConvert.DeserializeObject<DxRating>(json, Converter.Settings)!;
    public string ToJson() => JsonConvert.SerializeObject(this, Converter.Settings);
}

internal static class Converter
{
    public static readonly JsonSerializerSettings Settings = new()
    {
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        DateParseHandling        = DateParseHandling.None,
        Converters =
        {
            new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
        },
    };
}