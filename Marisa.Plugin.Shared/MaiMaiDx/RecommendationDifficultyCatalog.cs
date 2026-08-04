using Newtonsoft.Json;
using NLog;

namespace Marisa.Plugin.Shared.MaiMaiDx;

public sealed record RecommendationDifficulty(
    string Kind,
    double Value,
    bool Personalized,
    int? Rank,
    int? Of);

/// <summary>推分推荐使用的拟合难度数据。数据文件与前端难度曲线共用同一份资源。</summary>
public sealed class RecommendationDifficultyCatalog
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly Lazy<RecommendationDifficultyCatalog> DefaultCatalog = new(LoadDefault);

    private readonly Dictionary<long, CurveSong> _songs;

    private RecommendationDifficultyCatalog(Dictionary<long, CurveSong> songs)
    {
        _songs = songs;
    }

    public static RecommendationDifficultyCatalog Default => DefaultCatalog.Value;

    public static RecommendationDifficultyCatalog Empty { get; } = new([]);

    public static RecommendationDifficultyCatalog FromJson(string json)
    {
        var raw = JsonConvert.DeserializeObject<Dictionary<string, CurveSong>>(json) ?? [];
        return new RecommendationDifficultyCatalog(raw
            .Where(x => long.TryParse(x.Key, out _))
            .ToDictionary(x => long.Parse(x.Key), x => x.Value));
    }

    public bool TryEvaluate(long songId, int levelIdx, int playerRating, out RecommendationDifficulty difficulty)
    {
        difficulty = null!;
        if (!_songs.TryGetValue(songId, out var song)) return false;

        var chart = song.Charts.FirstOrDefault(x => x.LevelIndex == levelIdx);
        if (chart == null || chart.Curve.Count == 0) return false;

        var personalized = playerRating >= chart.Curve[0][0] && playerRating <= chart.Curve[^1][0];
        double? value = personalized ? Interpolate(chart.Curve, playerRating) : chart.Kind switch
        {
            "fitted_ds" when chart.Pooled.HasValue => chart.Constant + chart.Pooled.Value,
            "band_pct"                             => chart.BandPercentile,
            _                                      => null
        };

        if (!value.HasValue) return false;

        difficulty = new RecommendationDifficulty(
            chart.Kind,
            value.Value,
            personalized,
            chart.ScoreRank?.Rank,
            chart.ScoreRank?.Of);
        return true;
    }

    private static double Interpolate(IReadOnlyList<double[]> curve, int rating)
    {
        for (var i = 1; i < curve.Count; i++)
        {
            if (rating > curve[i][0]) continue;

            var left  = curve[i - 1];
            var right = curve[i];
            if (Math.Abs(right[0] - left[0]) < double.Epsilon) return right[1];

            var t = (rating - left[0]) / (right[0] - left[0]);
            return left[1] + (right[1] - left[1]) * t;
        }

        return curve[^1][1];
    }

    private static RecommendationDifficultyCatalog LoadDefault()
    {
        try
        {
            var path = Path.Join(ResourceManager.ResourcePath, "difficulty_curves.json");
            return FromJson(File.ReadAllText(path));
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Failed to load maimai recommendation difficulty data");
            return Empty;
        }
    }

    private sealed class CurveSong
    {
        [JsonProperty("charts")]
        public List<CurveChart> Charts { get; set; } = [];
    }

    private sealed class CurveChart
    {
        [JsonProperty("li")]
        public int LevelIndex { get; set; }

        [JsonProperty("ds")]
        public double Constant { get; set; }

        [JsonProperty("kind")]
        public string Kind { get; set; } = "";

        [JsonProperty("curve")]
        public List<double[]> Curve { get; set; } = [];

        [JsonProperty("score_rank")]
        public CurveRank? ScoreRank { get; set; }

        [JsonProperty("pooled")]
        public double? Pooled { get; set; }

        [JsonProperty("band_pct")]
        public double? BandPercentile { get; set; }
    }

    private sealed class CurveRank
    {
        [JsonProperty("rank")]
        public int Rank { get; set; }

        [JsonProperty("of")]
        public int Of { get; set; }
    }
}
