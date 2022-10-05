using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.PPlus;

internal class RankConverter : JsonConverter
{
    public override bool CanConvert(Type t) => t == typeof(Rank) || t == typeof(Rank?);

    public override object? ReadJson(JsonReader reader, Type t, object existingValue, JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null) return null;
        var value = serializer.Deserialize<string>(reader);
        return value switch
        {
            "A"  => Rank.A,
            "B"  => Rank.B,
            "C"  => Rank.C,
            "D"  => Rank.D,
            "S"  => Rank.S,
            "SH" => Rank.Sh,
            "X"  => Rank.X,
            "XH" => Rank.Xh,
            _    => throw new Exception("Cannot unmarshal type Rank")
        };
    }

    public override void WriteJson(JsonWriter writer, object? untypedValue, JsonSerializer serializer)
    {
        if (untypedValue == null)
        {
            serializer.Serialize(writer, null);
            return;
        }

        var value = (Rank)untypedValue;
        switch (value)
        {
            case Rank.A:
                serializer.Serialize(writer, "A");
                return;
            case Rank.B:
                serializer.Serialize(writer, "B");
                return;
            case Rank.C:
                serializer.Serialize(writer, "C");
                return;
            case Rank.S:
                serializer.Serialize(writer, "S");
                return;
            case Rank.Sh:
                serializer.Serialize(writer, "SH");
                return;
            case Rank.X:
                serializer.Serialize(writer, "X");
                return;
            case Rank.Xh:
                serializer.Serialize(writer, "XH");
                return;
        }

        throw new Exception("Cannot marshal type Rank");
    }

    public static readonly RankConverter Singleton = new RankConverter();
}