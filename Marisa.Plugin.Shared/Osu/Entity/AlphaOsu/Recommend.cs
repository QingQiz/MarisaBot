#pragma warning disable CS8618
namespace Marisa.Plugin.Shared.Osu.Entity.AlphaOsu;

using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

public partial class AlphaOsuResponse
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("code")]
    public long Code { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("data")]
    public AlphaOsuData AlphaOsuData { get; set; }
}

public partial class AlphaOsuData
{
    [JsonProperty("prev")]
    public long Prev { get; set; }

    [JsonProperty("next")]
    public long Next { get; set; }

    [JsonProperty("total")]
    public long Total { get; set; }

    [JsonProperty("list")]
    public List<AlphaOsuRecommend> Recommends { get; set; }
}

public partial class AlphaOsuRecommend
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("mapName")]
    public string MapName { get; set; }

    [JsonProperty("mapLink")]
    public Uri MapLink { get; set; }

    [JsonProperty("mapCoverUrl")]
    public Uri MapCoverUrl { get; set; }

    [JsonProperty("mod")]
    public List<AlphaOsuMod> Mod { get; set; }

    [JsonProperty("difficulty")]
    public double Difficulty { get; set; }

    [JsonProperty("keyCount")]
    public long KeyCount { get; set; }

    [JsonProperty("currentAccuracy")]
    public double? CurrentAccuracy { get; set; }

    [JsonProperty("currentSpeed")]
    public long? CurrentSpeed { get; set; }

    [JsonProperty("currentMod")]
    public List<string> CurrentMod { get; set; }

    [JsonProperty("currentAccuracyLink")]
    public Uri CurrentAccuracyLink { get; set; }

    [JsonProperty("currentPP")]
    public double? CurrentPp { get; set; }

    [JsonProperty("predictAccuracy")]
    public double PredictAccuracy { get; set; }

    [JsonProperty("predictPP")]
    public double PredictPp { get; set; }

    [JsonProperty("newRecordPercent")]
    public double NewRecordPercent { get; set; }

    [JsonProperty("ppIncrement")]
    public double PpIncrement { get; set; }

    [JsonProperty("passPercent")]
    public double PassPercent { get; set; }

    [JsonProperty("ppIncrementExpect")]
    public double PpIncrementExpect { get; set; }

    [JsonProperty("accurate")]
    public bool Accurate { get; set; }
}

public enum AlphaOsuMod
{
    Dt,
    Ht,
    Nm
};

public partial class AlphaOsuResponse
{
    public static AlphaOsuResponse FromJson(string json) => JsonConvert.DeserializeObject<AlphaOsuResponse>(json, Converter.Settings)!;
}

public static class Serialize
{
    public static string ToJson(this AlphaOsuResponse self) => JsonConvert.SerializeObject(self, Converter.Settings);
}

internal static class Converter
{
    public static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
    {
        MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
        DateParseHandling        = DateParseHandling.None,
        Converters =
        {
            ModConverter.Singleton,
            new IsoDateTimeConverter { DateTimeStyles = DateTimeStyles.AssumeUniversal }
        },
    };
}

internal class ModConverter : JsonConverter
{
    public override bool CanConvert(Type t) => t == typeof(AlphaOsuMod) || t == typeof(AlphaOsuMod?);

    public override object? ReadJson(JsonReader reader, Type t, object? existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        var value = serializer.Deserialize<string>(reader);
        switch (value)
        {
            case "DT":
                return AlphaOsuMod.Dt;
            case "HT":
                return AlphaOsuMod.Ht;
            case "NM":
                return AlphaOsuMod.Nm;
        }

        throw new Exception("Cannot unmarshal type Mod");
    }

    public override void WriteJson(JsonWriter writer, object? untypedValue, JsonSerializer serializer)
    {
        if (untypedValue == null)
        {
            serializer.Serialize(writer, null);
            return;
        }

        var value = (AlphaOsuMod)untypedValue;
        switch (value)
        {
            case AlphaOsuMod.Dt:
                serializer.Serialize(writer, "DT");
                return;
            case AlphaOsuMod.Ht:
                serializer.Serialize(writer, "HT");
                return;
            case AlphaOsuMod.Nm:
                serializer.Serialize(writer, "NM");
                return;
        }

        throw new Exception("Cannot marshal type Mod");
    }

    public static readonly ModConverter Singleton = new();
}