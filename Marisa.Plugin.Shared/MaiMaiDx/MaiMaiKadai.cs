using Newtonsoft.Json;
using NLog;

namespace Marisa.Plugin.Shared.MaiMaiDx;

/// <summary>课题目标；顺序与 kadai_difficulty.json 的 goals 一致。</summary>
public enum KadaiGoal { Sss, SssPlus, Ap, ApPlus, FiveStar }

public static class KadaiGoals
{
    public static readonly IReadOnlyList<(KadaiGoal Goal, string Name)> All =
    [
        (KadaiGoal.Sss, "SSS"), (KadaiGoal.SssPlus, "SSS+"), (KadaiGoal.Ap, "AP"), (KadaiGoal.ApPlus, "AP+"), (KadaiGoal.FiveStar, "五星"),
    ];

    public static string Name(this KadaiGoal goal) => All[(int)goal].Name;

    public static bool TryParse(string name, out KadaiGoal goal)
    {
        var hit = All.FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        goal = hit.Goal;
        return hit.Name != null;
    }

    public static bool Reached(this KadaiGoal goal, SongScore? score, long maxDx) => score != null && goal switch
    {
        KadaiGoal.Sss      => score.Achievement >= 100,
        KadaiGoal.SssPlus  => score.Achievement >= 100.5,
        KadaiGoal.Ap       => score.Fc is "ap" or "app",
        KadaiGoal.ApPlus   => score.Fc == "app",
        KadaiGoal.FiveStar => PlateData.DxScoreStar(score.DxScore, maxDx) >= 5,
        _                  => false
    };
}

/// <summary>
///     课题难度表：每个谱面每种目标的难度（达成率越低越难）与各 rating 段的达成率。
///     由水鱼脱敏成绩数据离线统计生成，只覆盖定数 ≥ MinConstant 的谱面；
///     表里没有的新谱用水鱼拟合定数按分段映射估算难度，定数低于覆盖范围的谱面不参与课题。
/// </summary>
public sealed class KadaiDifficultyCatalog
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly Lazy<KadaiDifficultyCatalog> DefaultCatalog = new(LoadDefault);

    private readonly Dictionary<(long SongId, int LevelIdx), Entry> _charts;
    private readonly double[][][] _fitKnots;
    private readonly int _ratingBin;
    private readonly double _minConstant;

    private KadaiDifficultyCatalog(Dictionary<(long, int), Entry> charts, double[][][] fitKnots, int ratingBin, double minConstant)
    {
        _charts      = charts;
        _fitKnots    = fitKnots;
        _ratingBin   = ratingBin;
        _minConstant = minConstant;
    }

    public static KadaiDifficultyCatalog Default => DefaultCatalog.Value;

    public static KadaiDifficultyCatalog FromJson(string json)
    {
        var raw = JsonConvert.DeserializeObject<RawTable>(json)!;
        var charts = raw.Charts
            .Where(x => long.TryParse(x.Key, out _))
            .SelectMany(x => x.Value.Select(c => (Key: (long.Parse(x.Key), c.LevelIdx), Entry: new Entry(c.Difficulty, c.Rates))))
            .ToDictionary(x => x.Key, x => x.Entry);
        var knots = raw.Goals.Select(g => raw.Fit.TryGetValue(g, out var k) ? k : []).ToArray();
        return new KadaiDifficultyCatalog(charts, knots, raw.RatingBin, raw.MinConstant);
    }

    /// <summary>目标难度；表中没有该谱面时用拟合定数估算。定数低于覆盖范围或无法估算时返回 false。</summary>
    public bool TryGetDifficulty(long songId, int levelIdx, KadaiGoal goal, double constant, double? fittedDifficulty, out double difficulty)
    {
        if (_charts.TryGetValue((songId, levelIdx), out var entry))
        {
            difficulty = entry.Difficulty[(int)goal];
            return true;
        }

        difficulty = 0;
        var knots = _fitKnots[(int)goal];
        // 只在分段映射的范围内插值，不外推：范围外的估计会被压到端点，难度失真
        if (constant < _minConstant || fittedDifficulty is not { } fit || knots.Length == 0 || fit < knots[0][0] || fit > knots[^1][0])
        {
            return false;
        }

        difficulty = Interpolate(knots, fit);
        return true;

        static double Interpolate(double[][] points, double x)
        {
            var i = Math.Max(1, Array.FindIndex(points, p => p[0] >= x));
            var (x0, y0, x1, y1) = (points[i - 1][0], points[i - 1][1], points[i][0], points[i][1]);
            return x1 == x0 ? y1 : y0 + (y1 - y0) * (x - x0) / (x1 - x0);
        }
    }

    /// <summary>该 rating 段玩家在此谱面达成目标的比例（0-1）；表中没有该谱面返回 null。</summary>
    public double? RateAt(long songId, int levelIdx, KadaiGoal goal, int rating)
    {
        if (!_charts.TryGetValue((songId, levelIdx), out var entry)) return null;
        var bins = entry.Rates[(int)goal];
        return bins[Math.Clamp(rating / _ratingBin, 0, bins.Length - 1)] / 10000.0;
    }

    private static KadaiDifficultyCatalog LoadDefault()
    {
        try
        {
            return FromJson(File.ReadAllText(Path.Join(ResourceManager.ResourcePath, "kadai_difficulty.json")));
        }
        catch (Exception e)
        {
            Logger.Warn(e, "Failed to load kadai difficulty table");
            return new KadaiDifficultyCatalog([], [[], [], [], [], []], 500, double.MaxValue);
        }
    }

    private sealed record Entry(double[] Difficulty, int[][] Rates);

    private sealed class RawTable
    {
        [JsonProperty("goals")] public List<string> Goals { get; set; } = [];
        [JsonProperty("rating_bin")] public int RatingBin { get; set; } = 500;
        [JsonProperty("min_ds")] public double MinConstant { get; set; }
        [JsonProperty("fit")] public Dictionary<string, double[][]> Fit { get; set; } = [];
        [JsonProperty("charts")] public Dictionary<string, List<RawChart>> Charts { get; set; } = [];
    }

    private sealed class RawChart
    {
        [JsonProperty("li")] public int LevelIdx { get; set; }
        [JsonProperty("d")] public double[] Difficulty { get; set; } = [];
        [JsonProperty("p")] public int[][] Rates { get; set; } = [];
    }
}

/// <summary>
///     以玩家已达成谱面中最难的一首为锚出三首难度递进的课题：
///     热身 = 比最难低 0.5~1.0，进阶 = 比最难低 0.5 以内，挑战 = 比最难高 0.6 以内；
///     已达成不足 5 首时按该 rating 段达成率从易到难取第 1、5、15 首左右。
///     同一档内优先没打过的谱面，其次是打过但成绩低的。分档参数用水鱼脱敏数据回测确定。
/// </summary>
public static class KadaiPicker
{
    public const int MinReached = 5;
    private const double WarmFrom = 1.0, OnFrom = 0.5, StretchRange = 0.6;

    public sealed record Chart(long SongId, int LevelIdx, double Difficulty, double? Rate, SongScore? Score);

    public sealed record Result(IReadOnlyList<Chart> Charts, bool ColdStart);

    /// <param name="reached">玩家已达成目标的全部谱面难度（不受筛选影响）。</param>
    /// <param name="candidates">筛选范围内、尚未达成目标的谱面。</param>
    public static Result? Pick(IReadOnlyList<double> reached, IReadOnlyList<Chart> candidates, Random random)
    {
        if (candidates.Count < 3) return null;

        if (reached.Count < MinReached)
        {
            var easy = candidates.Where(c => c.Rate.HasValue).OrderByDescending(c => c.Rate).ToList();
            if (easy.Count < 3) easy = candidates.OrderBy(c => c.Difficulty).ToList();
            var cold = new[] { (0, 2), (3, 7), (10, 19) }
                .Select(w => easy.Skip(w.Item1).Take(w.Item2 - w.Item1 + 1).ToList())
                .ToList();
            return new Result(PickInOrder(cold, easy), true);
        }

        var max = reached.Max();
        var bands = new (Func<double, bool> Contains, double Center)[]
        {
            (d => d >= max - WarmFrom && d < max - OnFrom, max - (WarmFrom + OnFrom) / 2),
            (d => d >= max - OnFrom && d <= max, max - OnFrom / 2),
            (d => d > max && d <= max + StretchRange, max + StretchRange / 2),
        };
        var pools = bands.Select(b => candidates.Where(c => b.Contains(c.Difficulty)).ToList()).ToList();
        return new Result(PickInOrder(pools, candidates, bands.Select(b => b.Center).ToArray()), false);

        // 每档按「没打过 → 打过但成绩低」挑；某档为空时取难度最接近该档中心（冷启动则按易到难）的剩余谱面。最后按难度升序。
        List<Chart> PickInOrder(List<List<Chart>> bandPools, IReadOnlyList<Chart> fallback, double[]? centers = null)
        {
            var chosen = new List<Chart>();
            for (var i = 0; i < bandPools.Count; i++)
            {
                var pool = bandPools[i].Except(chosen).ToList();
                if (pool.Count == 0)
                {
                    var rest = fallback.Except(chosen);
                    pool = (centers is null ? rest : rest.OrderBy(c => Math.Abs(c.Difficulty - centers[i]))).Take(1).ToList();
                }

                var unplayed = pool.Where(c => c.Score is null).ToList();
                var options = unplayed.Count > 0
                    ? unplayed
                    : pool.OrderBy(c => c.Score!.Achievement).Take((pool.Count + 1) / 2).ToList();
                chosen.Add(options[random.Next(options.Count)]);
            }

            return chosen.OrderBy(c => c.Difficulty).ToList();
        }
    }
}
