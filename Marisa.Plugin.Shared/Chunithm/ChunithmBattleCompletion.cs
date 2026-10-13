namespace Marisa.Plugin.Shared.Chunithm;

internal static class ChunithmBattleCompletion
{
    // 对战内部 fullchain=金、fullchain2=铂；落雪官方 API 的同名枚举恰好相反。
    // https://maimai.lxns.net/docs/api/chunithm#fullchaintype
    public static string NormalizeChain(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "fullchain" => "fullchain2",
        "fullchain2" => "fullchain",
        _ => value ?? ""
    };

    public static string? NormalizeClear(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "failed" => "failed", "clear" => "clear", "hard" => "hard", "brave" => "brave",
        "absolute" => "absolute", "catastrophy" => "catastrophy",
        _ => value
    };

    public static ChunithmScore FromDivingFish(ChunithmScore raw)
    {
        ValidateScore(raw);
        // 水鱼保存官网图标后缀；链枚举颜色依据同源图标命名与落雪官方 enum 交叉推断。
        // https://github.com/Diving-Fish/maimaidx-prober/blob/main/database/tools/page_parser.py
        var chain = NormalizeChain(raw.FullChain);
        var combo = raw.Fc?.StartsWith("fullchain", StringComparison.OrdinalIgnoreCase) == true ? "" : raw.FullCombo;
        return Copy(raw, combo, chain, null);
    }

    public static ChunithmScore FromLxns(ChunithmScore raw)
    {
        ValidateScore(raw);
        return Copy(raw, raw.FullCombo, NormalizeChain(raw.FullChain), NormalizeClear(raw.Clear));
    }

    public static ChunithmScore Merge(ChunithmScore left, ChunithmScore right)
    {
        var best = right.Achievement > left.Achievement ? right : left;
        return Copy(best, BestMark(left.FullCombo, right.FullCombo, ComboRank),
            BestMark(left.FullChain, right.FullChain, ChainRank),
            left.Clear is null && right.Clear is null ? null : BestMark(left.Clear, right.Clear, ClearRank));
    }

    private static ChunithmScore Copy(ChunithmScore raw, string combo, string chain, string? clear)
    {
        var score = new ChunithmScore
        {
            Id = raw.Id, Title = raw.Title, Level = raw.Level, LevelLabel = raw.LevelLabel,
            Achievement = raw.Achievement, Constant = raw.Constant, Fc = combo, FullChain = chain, Clear = clear
        };
        if (raw.HasLevelIndex) score.LevelIndex = raw.LevelIndex;
        return score;
    }

    private static void ValidateScore(ChunithmScore score)
    {
        if (!score.HasScore || score.Achievement is < 0 or > 1_010_000)
            throw new InvalidDataException("完整成绩响应包含无效分数，无法进行对战");
    }

    private static string BestMark(string? left, string? right, Func<string?, int> rank)
    {
        var a = rank(left);
        var b = rank(right);
        if (a != b) return (a > b ? left : right) ?? "";
        if (string.IsNullOrEmpty(left)) return right ?? "";
        if (string.IsNullOrEmpty(right)) return left;
        return left;
    }

    private static int ComboRank(string? value) => value switch
    {
        "alljusticecritical" => 3, "alljustice" => 2, "fullcombo" => 1, _ => 0
    };
    private static int ChainRank(string? value) => value switch
    {
        "fullchain2" => 2, "fullchain" => 1, _ => 0
    };
    private static int ClearRank(string? value) => value switch
    {
        "catastrophy" => 6, "absolute" => 5, "brave" => 4, "hard" => 3, "clear" => 2, "failed" => 1, _ => 0
    };
}
