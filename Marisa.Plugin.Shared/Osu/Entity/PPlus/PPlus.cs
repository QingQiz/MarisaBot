using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

#pragma warning disable CS8618
namespace Marisa.Plugin.Shared.Osu.Entity.PPlus;

public class PPlus
{
    private static class Converter
    {
        public static readonly JsonSerializerSettings Settings = new()
        {
            MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
            DateParseHandling        = DateParseHandling.None,
            Converters =
            {
                RankConverter.Singleton,
                new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
            },
        };
    }

    [JsonProperty("user_data")]
    public UserData UserData { get; set; }

    [JsonProperty("user_performances")]
    public UserPerformances UserPerformances { get; set; }

    public static string ToJson() => JsonConvert.SerializeObject(Converter.Settings);

    public static PPlus FromJson(string json) => JsonConvert.DeserializeObject<PPlus>(json, Converter.Settings)!;
}