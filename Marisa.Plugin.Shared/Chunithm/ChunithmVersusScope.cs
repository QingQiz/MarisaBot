using System.Globalization;
using System.Text.RegularExpressions;

namespace Marisa.Plugin.Shared.Chunithm;

public static class ChunithmVersusScope
{
    public static readonly IReadOnlyDictionary<string, int> DifficultyAliases = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["BASIC"] = 0, ["BAS"] = 0, ["BSC"] = 0, ["绿谱"] = 0,
        ["ADVANCED"] = 1, ["ADV"] = 1, ["黄谱"] = 1,
        ["EXPERT"] = 2, ["EXP"] = 2, ["红谱"] = 2,
        ["MASTER"] = 3, ["MAS"] = 3, ["MST"] = 3, ["紫谱"] = 3,
        ["ULTIMA"] = 4, ["ULT"] = 4, ["黑谱"] = 4
    };

    public sealed record Query(
        IReadOnlyList<int> LevelIndexes,
        string? SelectedVersion,
        IReadOnlyList<string> Genres,
        string? MinimumLevel,
        string? MaximumLevel,
        double? MinimumConstant,
        double? MaximumConstant)
    {
        public string Version => SelectedVersion ?? "";
        public string SortLabel => MinimumConstant.HasValue ? "歌曲 ID 升序" : "定数降序";
    }

    public static bool TryParseDifficulty(string input, out int levelIndex) =>
        DifficultyAliases.TryGetValue(input.Trim(), out levelIndex);

    public static bool TryStripDifficultyAffix(ReadOnlyMemory<char> input, out int levelIndex, out ReadOnlyMemory<char> rest)
    {
        levelIndex = -1;
        rest = input;
        foreach (var (alias, level) in DifficultyAliases.OrderByDescending(pair => pair.Key.Length))
        {
            if (input.Length <= alias.Length) continue;
            if (input.Span.StartsWith(alias, StringComparison.OrdinalIgnoreCase) &&
                (!IsAscii(alias[^1]) || !IsAscii(input.Span[alias.Length])))
            {
                rest = input[alias.Length..].Trim();
                levelIndex = level;
                return !rest.IsEmpty;
            }
            if (input.Span.EndsWith(alias, StringComparison.OrdinalIgnoreCase) &&
                (!IsAscii(alias[0]) || !IsAscii(input.Span[input.Length - alias.Length - 1])))
            {
                rest = input[..^alias.Length].Trim();
                levelIndex = level;
                return !rest.IsEmpty;
            }
        }
        return false;
    }

    public static bool TryParse(string input, IReadOnlyList<ChunithmSong> catalog, out Query? query, out string? error)
    {
        query = null;
        error = null;
        var remaining = input.Trim();
        if (remaining.Length == 0) return Fail("请输入版本、类别、等级或定数范围", out error);
        if (Regex.IsMatch(remaining, @"WORLD'?S\s*END|(?<![A-Za-z])WE(?![A-Za-z])|世终|世終", RegexOptions.IgnoreCase))
            return Fail("chu vs 暂不支持 WORLD'S END", out error);

        var levels = new HashSet<int>();
        foreach (var (alias, level) in DifficultyAliases.OrderByDescending(pair => pair.Key.Length))
        {
            var match = TokenMatch(remaining, alias);
            if (!match.Success) continue;
            levels.Add(level);
            remaining = Remove(remaining, match);
        }

        string? version = null;
        var versionAliases = catalog.Select(song => song.Version).Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(full => new[] { full, Regex.Replace(full, @"^CHUNITHM\s*", "", RegexOptions.IgnoreCase) }
                .SelectMany(alias => new[] { (Alias: alias, Version: full), (Alias: alias.Replace("-", ""), Version: full) }))
            .Where(pair => pair.Alias.Length > 0).Distinct().OrderByDescending(pair => pair.Alias.Length);
        foreach (var pair in versionAliases)
        {
            var match = VersionMatch(remaining, pair.Alias);
            if (!match.Success) continue;
            if (version is not null) return Fail("请只指定一个版本", out error);
            version = pair.Version;
            remaining = Remove(remaining, match);
        }

        var genres = new List<string>();
        var genreAliases = catalog.Select(song => song.Genre).Distinct(StringComparer.OrdinalIgnoreCase)
            .SelectMany(genre => GenreAliases(genre).Select(alias => (Alias: alias, Genre: genre)))
            .Where(pair => pair.Alias.Length > 0)
            .OrderByDescending(pair => pair.Alias.Length).ToArray();
        foreach (var pair in genreAliases)
        {
            var match = TokenMatch(remaining, pair.Alias);
            if (!match.Success) continue;
            genres.Add(pair.Genre);
            remaining = Remove(remaining, match);
        }

        double? minConstant = null, maxConstant = null;
        string? minLevel = null, maxLevel = null;
        var numberMatches = Regex.Matches(remaining, @"(?<![\d.])(?<lo>\d{1,2}(?:\.\d)?\+?)(?:\s*[-～~至]\s*(?<hi>\d{1,2}(?:\.\d)?\+?))?(?![\d.])");
        if (numberMatches.Count > 1) return Fail("请只指定一个等级或定数范围", out error);
        if (numberMatches.Count == 1)
        {
            var match = numberMatches[0];
            var low = match.Groups["lo"].Value;
            var high = match.Groups["hi"].Success ? match.Groups["hi"].Value : low;
            if (low.Contains('.') || high.Contains('.'))
            {
                if (low.EndsWith('+') || high.EndsWith('+') || !double.TryParse(low, CultureInfo.InvariantCulture, out var a) ||
                    !double.TryParse(high, CultureInfo.InvariantCulture, out var b) || a < 1 || b > 16 || a > b)
                    return Fail("定数范围须为 1.0～16.0，且下限不高于上限", out error);
                minConstant = a;
                maxConstant = b;
            }
            else
            {
                if (LevelNumber(low) < 1 || LevelNumber(high) > 16 || LevelNumber(low) > LevelNumber(high))
                    return Fail("等级范围须为 1～16，且下限不高于上限", out error);
                minLevel = low;
                maxLevel = high;
            }
            remaining = Remove(remaining, match);
        }

        if (remaining.Trim(' ', '\t', '　', ',', '，', '&', '与', '與').Length > 0)
            return Fail("无法解析范围：" + remaining, out error);
        if (version is null && genres.Count == 0 && minLevel is null && minConstant is null && levels.Count == 0)
            return Fail("请输入版本、类别、等级或定数范围", out error);
        int[] selectedLevels = levels.Count > 0 ? levels.Order().ToArray()
            : minLevel is not null || minConstant.HasValue ? [0, 1, 2, 3, 4] : [3];
        query = new Query(selectedLevels, version, genres.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), minLevel, maxLevel, minConstant, maxConstant);
        return true;
    }

    public static IReadOnlyList<ChunithmVersusChart> SelectCharts(Query query, IEnumerable<ChunithmSong> catalog)
    {
        var charts = ChunithmVersusCatalog.StandardCharts(catalog)
            .Where(chart => query.LevelIndexes.Contains(chart.LevelIndex))
            .Where(chart => query.SelectedVersion is null || NormalizeVersion(chart.Song.Version) == NormalizeVersion(query.SelectedVersion))
            .Where(chart => query.Genres.Count == 0 || query.Genres.Contains(chart.Song.Genre, StringComparer.OrdinalIgnoreCase))
            .Where(chart => !query.MinimumConstant.HasValue || chart.Constant >= query.MinimumConstant.Value - 0.001 && chart.Constant <= query.MaximumConstant!.Value + 0.001)
            .Where(chart => query.MinimumLevel is null || LevelNumber(chart.Level) >= LevelNumber(query.MinimumLevel) && LevelNumber(chart.Level) <= LevelNumber(query.MaximumLevel!));
        return (query.MinimumConstant.HasValue
                ? charts.OrderBy(chart => chart.Id).ThenBy(chart => chart.LevelIndex)
                : charts.OrderByDescending(chart => chart.Constant).ThenBy(chart => chart.Id).ThenBy(chart => chart.LevelIndex))
            .ToArray();
    }

    private static bool Fail(string message, out string? error)
    {
        error = message;
        return false;
    }

    private static bool IsAscii(char value) => value is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
    private static Match VersionMatch(string text, string token)
    {
        var compact = token.Replace("-", "");
        var pattern = Regex.Replace(Regex.Escape(compact), @"(?<=[A-Za-z])(?=[A-Za-z])", "-?");
        return Regex.Match(text, (IsAscii(compact[0]) ? @"(?<![A-Za-z])" : "") + pattern +
            (IsAscii(compact[^1]) ? @"(?![A-Za-z])" : ""), RegexOptions.IgnoreCase);
    }
    private static Match TokenMatch(string text, string token) => Regex.Match(text,
        (IsAscii(token[0]) ? @"(?<![A-Za-z])" : "") + Regex.Escape(token) + (IsAscii(token[^1]) ? @"(?![A-Za-z])" : ""), RegexOptions.IgnoreCase);
    private static string Remove(string text, Match match) => (text[..match.Index] + " " + text[(match.Index + match.Length)..]).Trim();
    private static string NormalizeVersion(string version) => Regex.Replace(version, @"[-\s]", "").ToUpperInvariant();
    private static double LevelNumber(string level) => double.TryParse(level.TrimEnd('+'), CultureInfo.InvariantCulture, out var number) ? number + (level.EndsWith('+') ? 0.5 : 0) : -1;

    private static IEnumerable<string> GenreAliases(string genre)
    {
        yield return genre;
        if (genre.Contains("niconico", StringComparison.OrdinalIgnoreCase) || genre.Contains("VOCALOID", StringComparison.OrdinalIgnoreCase))
        {
            yield return "术力口"; yield return "V家"; yield return "VOCALOID";
        }
        if (genre.Contains("東方") || genre.Contains("东方")) yield return "东方";
        if (genre.Contains("ORIGINAL", StringComparison.OrdinalIgnoreCase)) { yield return "原创"; yield return "原创曲"; }
        if (genre.Contains("VARIETY", StringComparison.OrdinalIgnoreCase)) { yield return "其他"; yield return "其他游戏"; }
        if (genre.Contains("POPS", StringComparison.OrdinalIgnoreCase) || genre.Contains("ANIME", StringComparison.OrdinalIgnoreCase)) { yield return "流行"; yield return "动漫"; }
        if (genre.Contains("イロドリミドリ")) yield return "彩绿";
        if (genre.Contains("ゲキマイ")) yield return "击中";
    }
}
