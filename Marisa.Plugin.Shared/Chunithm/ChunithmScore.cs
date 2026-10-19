using Newtonsoft.Json;

namespace Marisa.Plugin.Shared.Chunithm;

#pragma warning disable CS8618
public class ChunithmScore
{
    private decimal? _rating;

    [JsonProperty("ds")]
    public decimal Constant { get; set; }

    [JsonProperty("fc")]
    public string Fc { get; set; }

    // 水鱼旧字段可能只有 chain；保留它，但不能因此虚构一个 FC/AJ。
    private string? _fullChain;

    [JsonProperty("full_chain")]
    public string FullChain
    {
        get => _fullChain ?? (Fc?.StartsWith("fullchain", StringComparison.OrdinalIgnoreCase) == true ? Fc : "");
        set => _fullChain = value ?? "";
    }

    [JsonIgnore]
    public string FullCombo => Fc?.ToLowerInvariant() switch
    {
        "fc" or "fullcombo" => "fullcombo",
        "aj" or "alljustice" => "alljustice",
        "ajc" or "alljusticecritical" => "alljusticecritical",
        "fullchain" or "fullchain2" => "",
        _ => Fc ?? ""
    };

    /// <summary>成就分上限。官方规定 1,010,000 为 AJC（全 JUSTICE）。</summary>
    public const int AchievementMax = 1_010_000;

    /// <summary>AJC。用分数判定，不依赖各查分器是否把 fc 归一化成了 alljusticecritical。</summary>
    public bool IsAllJusticeCritical => Achievement >= AchievementMax;

    /// <summary>AJ 及以上（含 AJC）。</summary>
    public bool IsAllJustice => IsAllJusticeCritical || FullCombo is "alljustice" or "alljusticecritical";

    [JsonProperty("clear")]
    public string? Clear { get; set; }

    /**
     * 13+,14,...
     */
    [JsonProperty("level")]
    public string Level { get; set; }

    [JsonProperty("level_index")]
    public long LevelIndex
    {
        get => _levelIndex;
        set { _levelIndex = value; HasLevelIndex = true; }
    }

    private long _levelIndex;
    internal bool HasLevelIndex { get; private set; }

    /**
     * Basic,Advanced,Expert,Master,...
     */
    [JsonProperty("level_label")]
    public string LevelLabel { get; set; }

    [JsonProperty("mid")]
    public long Id { get; set; }

    [JsonProperty("ra")]
    public decimal Rating
    {
        get => _rating ?? (decimal)(_rating = ChunithmSong.Ra(Achievement, Constant));
        // ReSharper disable once ValueParameterNotUsed
        set {}
    }

    [JsonProperty("score")]
    public int Achievement
    {
        get => _achievement;
        set { _achievement = value; HasScore = true; }
    }

    private int _achievement;
    internal bool HasScore { get; private set; }

    [JsonProperty("title")]
    public string Title { get; set; }

    public string Rank => GetRank(Achievement);

    public static string GetRank(int achievement)
    {
        return achievement switch
        {
            >= 100_9000 => "sssp",
            >= 100_7500 => "sss",
            >= 100_5000 => "ssp",
            >= 100_0000 => "ss",
            >= 99_0000  => "sp",
            >= 97_5000  => "s",
            >= 95_0000  => "aaa",
            >= 92_5000  => "aa",
            >= 90_0000  => "a",
            >= 80_0000  => "bbb",
            >= 70_0000  => "bb",
            >= 60_0000  => "b",
            >= 50_0000  => "c",
            _           => "d"
        };
    }
}
