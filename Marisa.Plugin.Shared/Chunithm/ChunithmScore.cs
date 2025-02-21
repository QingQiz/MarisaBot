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

    /**
     * 13+,14,...
     */
    [JsonProperty("level")]
    public string Level { get; set; }

    [JsonProperty("level_index")]
    public long LevelIndex { get; set; }

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
    public int Achievement { get; set; }

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