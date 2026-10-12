namespace Marisa.Plugin.Shared.Versus;

public static class VersusSampler
{
    /// <summary>优先覆盖更多玩家的候选池，加权不放回抽取，每多一人玩过权重翻倍。</summary>
    public static List<TChart> Pick<TChart, TScore>(
        IEnumerable<TChart> charts,
        IReadOnlyList<IReadOnlyDictionary<(long Id, int LevelIdx), TScore>> scores,
        Func<TChart, (long Id, int LevelIdx)> keySelector,
        Func<TChart, double> constantSelector,
        int count,
        Random random)
    {
        var candidates = charts
            .Select(chart => (Chart: chart, Played: scores.Count(s => s.ContainsKey(keySelector(chart)))))
            .Where(x => x.Played >= 2)
            .ToList();

        var poolSize = Math.Max(10, count * 2);
        var threshold = Enumerable.Range(2, Math.Max(0, scores.Count - 1)).Reverse()
            .FirstOrDefault(t => candidates.Count(x => x.Played >= t) >= poolSize, 2);

        return candidates
            .Where(x => x.Played >= threshold)
            .Select(x => (x.Chart, Key: Math.Log(1 - random.NextDouble()) / Math.Pow(2, x.Played - threshold)))
            .OrderByDescending(x => x.Key)
            .Take(count)
            .Select(x => x.Chart)
            .OrderByDescending(constantSelector)
            .ThenBy(x => keySelector(x).Id)
            .ToList();
    }
}
