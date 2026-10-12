namespace Marisa.Plugin.Shared.Versus;

/// <summary>同一谱面的对位规则；空值仅表示没有游玩记录。</summary>
public static class VersusRules
{
    public static string Compare(double? left, double? right)
    {
        if (left is null && right is null) return "unplayed";
        if (left is null) return "right";
        if (right is null) return "left";
        if (left == right) return "draw";
        return left > right ? "left" : "right";
    }

    public static IReadOnlyList<VersusCell> ScoreChart(IReadOnlyList<double?> scores)
    {
        var played = scores.Count(s => s is not null);
        return scores.Select((score, index) => CreateCell(score, index)).ToArray();

        VersusCell CreateCell(double? score, int index)
        {
            if (score is not { } value) return new VersusCell(null, 0, played);

            var others = scores.Where((_, i) => i != index).ToArray();
            var points = others.Sum(other => other is not { } v || v < value ? 1 : v == value ? 0.5 : 0);
            return new VersusCell(1 + others.Count(s => s > value), points, others.Length);
        }
    }
}
