using Marisa.Plugin.Shared.Osu.Entity.Score;
using Marisa.Plugin.Shared.Osu.Entity.User;

namespace Marisa.Plugin.Shared.Osu;

public static class PerformanceCalculator
{
    public static (double ppMax, double length, double multiplier) ManiaPpChart(string beatmapPath, string[] gameMods, int totalHits)
    {
        var starRating = StarRatingCalculator.Calculate(File.ReadAllText(beatmapPath), FilterStarRatingMods(gameMods));
        return ManiaPerformanceCalculator.Calculate(gameMods, starRating, totalHits);
    }

    public static double GetStarRating(long beatmapsetId, string beatmapChecksum, long beatmapId, int modeInt, string[] mods)
    {
        var path = OsuApi.GetBeatmapPath(beatmapsetId, beatmapChecksum, beatmapId);
        return StarRatingCalculator.Calculate(File.ReadAllText(path), FilterStarRatingMods(mods));
    }

    public static double StarRating(this OsuScore score)
    {
        var starRatingMods = FilterStarRatingMods(score.Mods);

        if (starRatingMods.Length == 0)
        {
            return score.Beatmap.StarRating;
        }

        string path;
        try
        {
            path = OsuApi.GetBeatmapPath(score.Beatmap);
        }
        catch (Exception e) when (e is FileNotFoundException or HttpRequestException)
        {
            return score.Beatmap.StarRating;
        }

        return StarRatingCalculator.Calculate(File.ReadAllText(path), starRatingMods);
    }

    public static double GetPerformancePoint(
        long beatmapsetId, string beatmapChecksum, long beatmapId, int modeInt, string[] mods, double acc, int maxCombo, int cMax, int c300, int c200, int c100,
        int c50, int cMiss, long score)
    {
        var path = OsuApi.GetBeatmapPath(beatmapsetId, beatmapChecksum, beatmapId);
        var beatmapText = File.ReadAllText(path);
        var starRating = StarRatingCalculator.Calculate(beatmapText, FilterStarRatingMods(mods));
        var beatmap = BeatmapPerformanceInfo.Parse(beatmapText);
        var statistics = new ScoreStatistics(cMax, c300, c200, c100, c50, cMiss);

        return CalculatePerformancePoint(modeInt, mods, starRating, NormalizeAccuracy(acc), maxCombo, score, statistics, beatmap);
    }

    public static double PerformancePoint(this OsuScore score)
    {
        if (score.Pp != null)
        {
            return (double)score.Pp;
        }

        try
        {
            return GetPerformancePoint(score.Beatmapset.Id, score.Beatmap.Checksum, score.Beatmap.Id, score.ModeInt, score.Mods,
                score.Accuracy, score.MaxCombo,
                score.Statistics.Count300P, score.Statistics.Count300, score.Statistics.Count200, score.Statistics.Count100, score.Statistics.Count50,
                score.Statistics.CountMiss, score.Score);
        }
        catch (Exception e) when (e is FileNotFoundException or HttpRequestException)
        {
            return 0;
        }
    }

    public static (double scorePp, double bonusPp, long rankedScores) BonusPp(this OsuUserInfo info, IEnumerable<OsuScore> scores)
    {
        var scorePp = scores.Sum(s => s.Weight!.Pp);
        var bonusPp = info.Statistics.Pp - scorePp;

        var totalScores =
            info.Statistics.GradeCounts["a"] +
            info.Statistics.GradeCounts["s"] +
            info.Statistics.GradeCounts["sh"] +
            info.Statistics.GradeCounts["ss"] +
            info.Statistics.GradeCounts["ssh"];

        if (!double.IsNaN(scorePp) && !double.IsNaN(bonusPp))
        {
            return (scorePp, bonusPp, totalScores);
        }

        return (0, 0, 0);
    }

    private static double CalculatePerformancePoint(int modeInt, string[] mods, double starRating, double accuracy, int maxCombo, long score, ScoreStatistics statistics, BeatmapPerformanceInfo beatmap)
    {
        var totalHits = Math.Max(statistics.TotalHits, beatmap.TotalHits);
        var mapMaxCombo = Math.Max(Math.Max(beatmap.MaxCombo, totalHits), maxCombo);
        var comboRatio = mapMaxCombo <= 0 ? 1 : Math.Clamp(maxCombo / (double)mapMaxCombo, 0, 1);
        var missPenalty = Math.Pow(0.97, statistics.Misses);

        return modeInt switch
        {
            1 => CalculateTaikoPerformance(mods, starRating, accuracy, comboRatio, totalHits, statistics.Misses),
            2 => CalculateCatchPerformance(mods, starRating, accuracy, comboRatio, totalHits, statistics.Misses),
            3 => CalculateManiaPerformance(mods, starRating, accuracy, totalHits, score, statistics),
            _ => CalculateOsuPerformance(mods, starRating, accuracy, comboRatio, totalHits, statistics.Misses, beatmap)
        } * ModMultiplier(mods, modeInt);
    }

    private static double CalculateOsuPerformance(string[] mods, double starRating, double accuracy, double comboRatio, int totalHits, int misses, BeatmapPerformanceInfo beatmap)
    {
        var lengthBonus = 0.95 + 0.4 * Math.Min(1, totalHits / 2000.0) + (totalHits > 2000 ? Math.Log10(totalHits / 2000.0) * 0.5 : 0);
        var missPenalty = Math.Pow(0.97, misses);
        var comboPenalty = Math.Pow(comboRatio, 0.8);
        var basePerformance = Math.Pow(Math.Max(starRating, 0.05), 3.0) * 1.18;
        var accuracyPerformance = Math.Pow(accuracy, 9) * Math.Pow(beatmap.OverallDifficulty, 0.85) * 2.5;

        if (HasMod(mods, "HD"))
            basePerformance *= 1.03 + Math.Min(0.1, (10 - beatmap.ApproachRate) / 100);

        if (HasMod(mods, "FL"))
            basePerformance *= 1.08 + Math.Min(0.25, totalHits / 2400.0);

        return Math.Pow(Math.Pow(basePerformance * lengthBonus * missPenalty * comboPenalty * Math.Pow(accuracy, 2.5), 1.1) + Math.Pow(accuracyPerformance, 1.1), 1 / 1.1);
    }

    private static double CalculateTaikoPerformance(string[] mods, double starRating, double accuracy, double comboRatio, int totalHits, int misses)
    {
        var lengthBonus = 1 + 0.1 * Math.Min(1, totalHits / 1500.0);
        var missPenalty = Math.Pow(0.985, misses);
        var basePerformance = Math.Pow(Math.Max(starRating, 0.05), 2.35) * 14.5;

        if (HasMod(mods, "HD"))
            basePerformance *= 1.075;

        if (HasMod(mods, "FL"))
            basePerformance *= 1.05 + Math.Min(0.15, totalHits / 3000.0);

        return basePerformance * lengthBonus * missPenalty * Math.Pow(comboRatio, 0.5) * Math.Pow(accuracy, 5.5);
    }

    private static double CalculateCatchPerformance(string[] mods, double starRating, double accuracy, double comboRatio, int totalHits, int misses)
    {
        var lengthBonus = 0.95 + 0.3 * Math.Min(1, totalHits / 2500.0);
        var missPenalty = Math.Pow(0.98, misses);
        var basePerformance = Math.Pow(Math.Max(starRating, 0.05), 2.2) * 12.2;

        if (HasMod(mods, "HD"))
            basePerformance *= 1.05;

        if (HasMod(mods, "FL"))
            basePerformance *= 1.12;

        return basePerformance * lengthBonus * missPenalty * Math.Pow(comboRatio, 0.8) * Math.Pow(accuracy, 5);
    }

    private static double CalculateManiaPerformance(string[] mods, double starRating, double accuracy, int totalHits, long score, ScoreStatistics statistics)
    {
        var (ppMax, length, multiplier) = ManiaPerformanceCalculator.Calculate(mods, starRating, totalHits);
        var scoreRatio = score > 0 ? Math.Clamp(score / 1_000_000.0, 0, 1) : 0;
        var accuracyRatio = Math.Pow(accuracy, 7.5);

        if (statistics.TotalHits > 0)
            accuracyRatio *= Math.Pow(1 - statistics.Misses / (double)statistics.TotalHits, 0.5);

        return ppMax * length * multiplier * Math.Max(accuracyRatio, scoreRatio * scoreRatio);
    }

    private static double ModMultiplier(string[] mods, int modeInt)
    {
        var multiplier = 1.0;

        if (HasMod(mods, "NF"))
            multiplier *= modeInt == 3 ? 0.75 : 0.90;

        if (HasMod(mods, "EZ"))
            multiplier *= modeInt == 3 ? 0.50 : 0.95;

        if (HasMod(mods, "SO"))
            multiplier *= 0.95;

        if (HasMod(mods, "RX") || HasMod(mods, "AP"))
            multiplier *= 0;

        return multiplier;
    }

    private static double NormalizeAccuracy(double accuracy)
    {
        if (accuracy > 1)
            accuracy /= 100;

        return Math.Clamp(accuracy, 0, 1);
    }

    private static string[] FilterStarRatingMods(IEnumerable<string> mods)
    {
        var result = new List<string>();

        foreach (var acronym in ExpandModAcronyms(mods, keepRateSettings: true))
        {
            if (acronym is "NM" or "CL" or "DT" or "NC" or "HT" or "DC")
                result.Add(acronym);

            if (acronym.Length > 2 && acronym[..2] is "DT" or "NC" or "HT" or "DC")
                result.Add(acronym);
        }

        return result.ToArray();
    }

    private static bool HasMod(IEnumerable<string> mods, string mod)
        => ExpandModAcronyms(mods, keepRateSettings: false).Any(m => m.Equals(mod, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> ExpandModAcronyms(IEnumerable<string> mods, bool keepRateSettings)
    {
        foreach (var rawMod in mods)
        {
            foreach (var token in rawMod.Split(new[] { ',', '+', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var upper = token.ToUpperInvariant();

                if (keepRateSettings && upper.Length > 2 && upper[..2] is "DT" or "NC" or "HT" or "DC" && (char.IsDigit(upper[2]) || upper[2] is '=' or ':' or '@'))
                {
                    yield return upper;
                    continue;
                }

                if (upper.Length <= 2)
                {
                    yield return upper;
                    continue;
                }

                if (upper.Length % 2 != 0)
                    continue;

                for (var i = 0; i < upper.Length; i += 2)
                    yield return upper.Substring(i, 2);
            }
        }
    }

    private readonly record struct ScoreStatistics(int Perfect, int Great, int Good, int Ok, int Meh, int Misses)
    {
        public int TotalHits => Perfect + Great + Good + Ok + Meh + Misses;
    }

    private sealed record BeatmapPerformanceInfo(int Mode, int TotalHits, int MaxCombo, double OverallDifficulty, double ApproachRate)
    {
        public static BeatmapPerformanceInfo Parse(string text)
        {
            var mode = 0;
            var overallDifficulty = 5d;
            var approachRate = 5d;
            var hasApproachRate = false;
            var totalHits = 0;
            var maxCombo = 0;
            var section = string.Empty;

            foreach (var rawLine in text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                    continue;

                if (line.StartsWith('[') && line.EndsWith(']'))
                {
                    section = line[1..^1];
                    continue;
                }

                switch (section)
                {
                    case "General":
                        if (TrySplitKeyValue(line, out var key, out var value) && key == "Mode" && int.TryParse(value, out var parsedMode))
                            mode = parsedMode;
                        break;

                    case "Difficulty":
                        if (!TrySplitKeyValue(line, out key, out value))
                            break;

                        if (key == "OverallDifficulty" && double.TryParse(value, out var parsedOd))
                        {
                            overallDifficulty = parsedOd;
                            if (!hasApproachRate)
                                approachRate = parsedOd;
                        }
                        else if (key == "ApproachRate" && double.TryParse(value, out var parsedAr))
                        {
                            approachRate = parsedAr;
                            hasApproachRate = true;
                        }

                        break;

                    case "HitObjects":
                        if (line.Split(',') is { Length: >= 4 } split && int.TryParse(split[3], out var type))
                        {
                            totalHits++;
                            maxCombo += EstimateComboContribution(type, split, mode);
                        }

                        break;
                }
            }

            return new BeatmapPerformanceInfo(mode, totalHits, Math.Max(maxCombo, totalHits), Math.Clamp(overallDifficulty, 0, 10), Math.Clamp(approachRate, 0, 10));
        }

        private static bool TrySplitKeyValue(string line, out string key, out string value)
        {
            var index = line.IndexOf(':');
            if (index < 0)
            {
                key = string.Empty;
                value = string.Empty;
                return false;
            }

            key = line[..index].Trim();
            value = line[(index + 1)..].Trim();
            return true;
        }

        private static int EstimateComboContribution(int type, string[] split, int mode)
        {
            if (mode == 3)
                return 1;

            if ((type & 2) == 0)
                return 1;

            if (split.Length < 7 || !int.TryParse(split[6], out var repeatCount))
                return 1;

            return Math.Max(1, repeatCount + 1);
        }
    }
}

public static class ManiaPerformanceCalculator
{
    public static (double ppMax, double length, double multiplier) Calculate(string[] mods, double starRating, int totalHits)
    {
        var multiplier = 8.0;

        if (mods.Any(m => m.Equals("nf", StringComparison.OrdinalIgnoreCase))) multiplier *= 0.75;
        if (mods.Any(m => m.Equals("ez", StringComparison.OrdinalIgnoreCase))) multiplier *= 0.5;

        return (
            Math.Pow(Math.Max(starRating - 0.15, 0.05), 2.2),
            1 + 0.1 * Math.Min(1, totalHits / 1500.0),
            multiplier);
    }
}
