#pragma warning disable CS8618
using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Osu.Entity.Score;

public class Statistics
{
    private int? _count100;
    private int? _count300;
    private int? _count50;
    private int? _count300P;
    private int? _count200;
    private int? _countMiss;

    [JsonProperty("count_100", Required = Required.Default)]
    public int Count100
    {
        get
        {
            if (_count100 is { } explicitCount100 && (explicitCount100 != 0 || Ok is null or 0)) return explicitCount100;
            return Ok ?? _count100 ?? 0;
        }
        set => _count100 = value;
    }

    [JsonProperty("count_300", Required = Required.Default)]
    public int Count300
    {
        get
        {
            if (_count300 is { } explicitCount300 && (explicitCount300 != 0 || Great is null or 0)) return explicitCount300;
            return Great ?? _count300 ?? 0;
        }
        set => _count300 = value;
    }

    [JsonProperty("count_50", Required = Required.Default)]
    public int Count50
    {
        get
        {
            if (_count50 is { } explicitCount50 && (explicitCount50 != 0 || Meh is null or 0)) return explicitCount50;
            return Meh ?? _count50 ?? 0;
        }
        set => _count50 = value;
    }

    [JsonProperty("count_geki", Required = Required.Default)]
    public int Count300P
    {
        get
        {
            if (_count300P is { } explicitCount300P && (explicitCount300P != 0 || Perfect is null or 0)) return explicitCount300P;
            return Perfect ?? _count300P ?? 0;
        }
        set => _count300P = value;
    }

    [JsonProperty("count_katu", Required = Required.Default)]
    public int Count200
    {
        get
        {
            if (_count200 is { } explicitCount200 && (explicitCount200 != 0 || Good is null or 0)) return explicitCount200;
            return Good ?? _count200 ?? 0;
        }
        set => _count200 = value;
    }

    [JsonProperty("count_miss", Required = Required.Default)]
    public int CountMiss
    {
        get
        {
            if (_countMiss is { } explicitCountMiss && (explicitCountMiss != 0 || Miss is null or 0)) return explicitCountMiss;
            return Miss ?? _countMiss ?? 0;
        }
        set => _countMiss = value;
    }

    [JsonProperty("ok", Required = Required.Default)]
    public int? Ok { get; set; }

    [JsonProperty("great", Required = Required.Default)]
    public int? Great { get; set; }

    [JsonProperty("meh", Required = Required.Default)]
    public int? Meh { get; set; }

    [JsonProperty("perfect", Required = Required.Default)]
    public int? Perfect { get; set; }

    [JsonProperty("good", Required = Required.Default)]
    public int? Good { get; set; }

    [JsonProperty("miss", Required = Required.Default)]
    public int? Miss { get; set; }

    public bool ShouldSerializeOk() => false;

    public bool ShouldSerializeGreat() => false;

    public bool ShouldSerializeMeh() => false;

    public bool ShouldSerializePerfect() => false;

    public bool ShouldSerializeGood() => false;

    public bool ShouldSerializeMiss() => false;
}